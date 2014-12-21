using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ModuleManager
{
    [KSPAddon(KSPAddon.Startup.Instantly, false)]
    public class ModuleManager : MonoBehaviour
    {
        #region state

        private bool inRnDCenter;

        private bool reloading;

        public bool showUI = false;

        private Rect windowPos = new Rect(80f, 60f, 240f, 40f);

        private string version = "";

        private Texture2D tex;
        private Texture2D tex2;
        private int activePos = 0;

        #endregion state

        #region Top Level - Update

        private static bool loadedInScene;

        internal void OnRnDCenterSpawn()
        {
            inRnDCenter = true;
        }

        internal void OnRnDCenterDeSpawn()
        {
            inRnDCenter = false;
        }

        public static void log(String s)
        {
            print("[ModuleManager] " + s);
        }

        internal void Awake()
        {
            // Ensure that only one copy of the service is run per scene change.
            if (loadedInScene || !ElectionAndCheck())
            {
                Assembly currentAssembly = Assembly.GetExecutingAssembly();
                log("Multiple copies of current version. Using the first copy. Version: " +
                    currentAssembly.GetName().Version);
                Destroy(gameObject);
                return;
            }
            DontDestroyOnLoad(gameObject);

            System.Version v = Assembly.GetExecutingAssembly().GetName().Version;
            version = v.Major + "." + v.Minor + "." + v.Build;

            // Subscribe to the RnD center spawn/deSpawn events
            GameEvents.onGUIRnDComplexSpawn.Add(OnRnDCenterSpawn);
            GameEvents.onGUIRnDComplexDespawn.Add(this.OnRnDCenterDeSpawn);

            // This code is the one that should allow to plugin into the stock loading bar
            // but it can't insert the LoadingSystembefore PartLoader ie too late
            // So for now we keep using a blocking update

            LoadingScreen screen = FindObjectOfType<LoadingScreen>();
            if (screen == null)
            {
                log("Can't find LoadingScreen type. Aborting ModuleManager execution");
                return;
            }
            List<LoadingSystem> list = LoadingScreen.Instance.loaders;

            if (list != null)
            {
                // So you can insert a LoadingSystem object in this list at any point.
                // GameDatabase is first in the list, and PartLoader is second
                // We could insert ModuleManager after GameDatabase to get it to run there
                // and SaveGameFixer after PartLoader.

                GameObject aGameObject = new GameObject();
                MMPatchLoader loader = aGameObject.AddComponent<MMPatchLoader>();

                log(string.Format("Adding ModuleManager to the loading screen {0}", list.Count));
                list.Insert(1, loader);
            }
            else
                Debug.LogWarning("Can't find the LoadingSystem list. Aborting ModuleManager execution");

            tex = new Texture2D(33, 20, TextureFormat.ARGB32, false);
            tex.LoadImage(Properties.Resources.cat);
            Color[] pix = tex.GetPixels(0, 0, 1, tex.height);
            tex2 = new Texture2D(1, 20, TextureFormat.ARGB32, false);
            tex2.SetPixels(pix);
            tex2.Apply();

            loadedInScene = true;
        }

        // Unsubscribe from events when the behavior dies
        internal void OnDestroy()
        {
            GameEvents.onGUIRnDComplexSpawn.Remove(OnRnDCenterSpawn);
            GameEvents.onGUIRnDComplexDespawn.Remove(this.OnRnDCenterDeSpawn);
        }

        internal void Update()
        {
            if (GameSettings.MODIFIER_KEY.GetKey() && Input.GetKeyDown(KeyCode.F11))
                showUI = !showUI;

            if (reloading)
            {
                float percent = 0;
                if (!GameDatabase.Instance.IsReady())
                    percent = GameDatabase.Instance.ProgressFraction();
                else if (!MMPatchLoader.Instance.IsReady())
                    percent = 1f + MMPatchLoader.Instance.ProgressFraction();
                else if (!PartLoader.Instance.IsReady())
                    percent = 2f + PartLoader.Instance.ProgressFraction();

                int intPercent = Mathf.CeilToInt(percent * 100f / 3f);
                ScreenMessages.PostScreenMessage("Database reloading " + intPercent + "%", Time.deltaTime,
                    ScreenMessageStyle.UPPER_CENTER);
            }
        }

        #region GUI stuff.

        public void OnGUI()
        {
            if (HighLogic.LoadedScene == GameScenes.LOADING && MMPatchLoader.Instance != null)
            {
                float offsetY = Mathf.FloorToInt(0.8f * Screen.height);

                if (IsWin64())
                {
                    var centeredWarningStyle = new GUIStyle(GUI.skin.GetStyle("Label"))
                    {
                        alignment = TextAnchor.UpperCenter,
                        fontSize = 16,
                        normal = { textColor = Color.yellow }
                    };
                    const string warning = "You are using 64-bit KSP on Windows. This version of KSP is known to cause crashes unrelated to mods.";
                    Vector2 sizeOfWarningLabel = centeredWarningStyle.CalcSize(new GUIContent(warning));

                    GUI.Label(new Rect(Screen.width / 2f - (sizeOfWarningLabel.x / 2f), offsetY, sizeOfWarningLabel.x, sizeOfWarningLabel.y), warning, centeredWarningStyle);
                    offsetY += sizeOfWarningLabel.y;
                }

                var centeredStyle = new GUIStyle(GUI.skin.GetStyle("Label"))
                {
                    alignment = TextAnchor.UpperCenter,
                    fontSize = 16
                };
                Vector2 sizeOfLabel = centeredStyle.CalcSize(new GUIContent(MMPatchLoader.Instance.status));
                GUI.Label(new Rect(Screen.width / 2f - (sizeOfLabel.x / 2f), offsetY, sizeOfLabel.x, sizeOfLabel.y), MMPatchLoader.Instance.status, centeredStyle);
                offsetY += sizeOfLabel.y;

                if (MMPatchLoader.Instance.errorCount > 0)
                {
                    var errorStyle = new GUIStyle(GUI.skin.GetStyle("Label"))
                    {
                        alignment = TextAnchor.UpperLeft,
                        fontSize = 16
                    };
                    Vector2 sizeOfError = errorStyle.CalcSize(new GUIContent(MMPatchLoader.Instance.errors));
                    GUI.Label(new Rect(Screen.width / 2f - (sizeOfLabel.x / 2), offsetY, sizeOfError.x, sizeOfError.y), MMPatchLoader.Instance.errors, errorStyle);
                    offsetY += sizeOfError.y;
                }


                if (IsWin64())
                {
                    GUI.color = Color.white;
                    int scale = 1;
                    if (Screen.height >= 1080)
                        scale = 2;
                    if (Screen.height > 1440)
                        scale = 3;

                    int trailLength = 8 * tex.width * scale;
                    int totalLenth = trailLength + tex.width * scale;
                    int startPos = activePos - totalLenth;

                    Color guiColor = Color.white;
                    int currentOffset = 0;
                    int heightOffset = 0;
                    while (currentOffset < trailLength)
                    {
                        guiColor.a = (float)currentOffset / trailLength;
                        GUI.color = guiColor;

                        heightOffset = Mathf.RoundToInt(1f + Mathf.Sin(2f * Mathf.PI * (startPos + currentOffset) / (Screen.width / 6f)) * (tex.height * scale / 6f));

                        GUI.DrawTexture(new Rect(startPos + currentOffset, heightOffset + offsetY, tex2.width, tex2.height * scale), tex2);
                        currentOffset++;
                    }
                    GUI.DrawTexture(new Rect(startPos + currentOffset, heightOffset + offsetY, tex.width * scale, tex.height * scale), tex);

                    activePos = (activePos + 3) % (Screen.width + totalLenth);
                    GUI.color = Color.white;
                }
            }


            if (showUI &&
                (HighLogic.LoadedScene == GameScenes.SPACECENTER || HighLogic.LoadedScene == GameScenes.MAINMENU) &&
                !inRnDCenter)
            {
                windowPos = GUILayout.Window(
                    GetType().FullName.GetHashCode(),
                    windowPos,
                    WindowGUI,
                    "ModuleManager " + version,
                    GUILayout.Width(200),
                    GUILayout.Height(20));
            }
        }

        public static bool IsWin64()
        {
            return (IntPtr.Size == 8) && (Environment.OSVersion.Platform == PlatformID.Win32NT);
        }

        protected void WindowGUI(int windowID)
        {
            GUILayout.BeginVertical();

            if (GUILayout.Button("Reload Database"))
                StartCoroutine(DataBaseReloadWithMM());
            if (GUILayout.Button("Dump Database to File"))
                StartCoroutine(DataBaseReloadWithMM(true));
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private IEnumerator DataBaseReloadWithMM(bool dump = false)
        {
            reloading = true;

            ScreenMessages.PostScreenMessage("Database reloading started", 1, ScreenMessageStyle.UPPER_CENTER);
            yield return null;

            GameDatabase.Instance.Recompile = true;
            GameDatabase.Instance.StartLoad();

            // wait for it to finish
            while (!GameDatabase.Instance.IsReady())
                yield return null;

            MMPatchLoader.Instance.StartLoad();

            while (!MMPatchLoader.Instance.IsReady())
                yield return null;

            log("DB Reload OK with patchCount=" + MMPatchLoader.Instance.patchedNodeCount + " errorCount=" +
                MMPatchLoader.Instance.errorCount + " needsUnsatisfiedCount=" +
                MMPatchLoader.Instance.needsUnsatisfiedCount);

            PartResourceLibrary.Instance.LoadDefinitions();

            if (dump)
                OutputAllConfigs();

            PartLoader.Instance.StartLoad();

            while (!PartLoader.Instance.IsReady())
                yield return null;

            // Needs more work.
            //ConfigNode game = HighLogic.CurrentGame.config.GetNode("GAME");

            //if (game != null && ResearchAndDevelopment.Instance != null)
            //{
            //    ScreenMessages.PostScreenMessage("GAME found");
            //    ConfigNode scenario = game.GetNodes("SCENARIO").FirstOrDefault((ConfigNode n) => n.name == "ResearchAndDevelopment");
            //    if (scenario != null)
            //    {
            //        ScreenMessages.PostScreenMessage("SCENARIO found");
            //        ResearchAndDevelopment.Instance.OnLoad(scenario);
            //    }
            //}

            reloading = false;
            ScreenMessages.PostScreenMessage("Database reloading finished", 1, ScreenMessageStyle.UPPER_CENTER);
        }

        private static void OutputAllConfigs()
        {
            string path = KSPUtil.ApplicationRootPath + Path.DirectorySeparatorChar + "_MMCfgOutput"
                          + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(path);

            foreach (var d in GameDatabase.Instance.root.AllConfigs)
                File.WriteAllText(path + d.url.Replace('/', '.') + ".cfg", d.config.ToString());
        }

        #endregion GUI stuff.

        public bool ElectionAndCheck()
        {
            #region Type election

            // TODO : Move the old version check in a process that call Update.

            // Check for old version and MMSarbianExt
            var oldMM =
                AssemblyLoader.loadedAssemblies.Where(
                    a => a.assembly.GetName().Name == Assembly.GetExecutingAssembly().GetName().Name)
                    .Where(a => a.assembly.GetName().Version.CompareTo(new System.Version(1, 5, 0)) == -1);
            var oldAssemblies =
                oldMM.Concat(AssemblyLoader.loadedAssemblies.Where(a => a.assembly.GetName().Name == "MMSarbianExt"));
            if (oldAssemblies.Any())
            {
                var badPaths =
                    oldAssemblies.Select(a => a.path)
                        .Select(
                            p =>
                                Uri.UnescapeDataString(
                                    new Uri(Path.GetFullPath(KSPUtil.ApplicationRootPath)).MakeRelativeUri(new Uri(p))
                                        .ToString()
                                        .Replace('/', Path.DirectorySeparatorChar)));
                string status =
                    "You have old versions of Module Manager (older than 1.5) or MMSarbianExt.\nYou will need to remove them for Module Manager and the mods using it to work\nExit KSP and delete those files :\n" +
                    String.Join("\n", badPaths.ToArray());
                PopupDialog.SpawnPopupDialog("Old versions of Module Manager", status, "OK", false, HighLogic.Skin);
                log("Old version of Module Manager present. Stopping");
                return false;
            }

            Assembly currentAssembly = Assembly.GetExecutingAssembly();
            var eligible = from a in AssemblyLoader.loadedAssemblies
                           let ass = a.assembly
                           where ass.GetName().Name == currentAssembly.GetName().Name
                           orderby ass.GetName().Version descending, a.path ascending
                           select a;

            // Elect the newest loaded version of MM to process all patch files.
            // If there is a newer version loaded then don't do anything
            // If there is a same version but earlier in the list, don't do anything either.
            if (eligible.First().assembly != currentAssembly)
            {
                //loaded = true;
                log("version " + currentAssembly.GetName().Version + " at " + currentAssembly.Location +
                    " lost the election");
                Destroy(gameObject);
                return false;
            }
            string candidates = "";
            foreach (AssemblyLoader.LoadedAssembly a in eligible)
            {
                if (currentAssembly.Location != a.path)
                    candidates += "Version " + a.assembly.GetName().Version + " " + a.path + " " + "\n";
            }
            if (candidates.Length > 0)
            {
                log("version " + currentAssembly.GetName().Version + " at " + currentAssembly.Location +
                    " won the election against\n" + candidates);
            }

            #endregion Type election

            return true;
        }
    }

    public delegate void ModuleManagerPostPatchCallback();

    public class MMPatchLoader : LoadingSystem
    {
        public int totalPatchCount = 0;

        public int appliedPatchCount = 0;

        public int patchedNodeCount = 0;

        public int errorCount = 0;

        public int needsUnsatisfiedCount = 0;

        private Dictionary<String, int> errorFiles;

        private List<AssemblyName> mods;

        public string status = "";

        public string errors = "";

        public string activity = "Module Manager";

        private static Dictionary<string, Regex> regexCache = new Dictionary<string, Regex>();

        private static Stack<ConfigNode> nodeStack = new Stack<ConfigNode>();

        private static ConfigNode topNode;

        private static string cachePath = KSPUtil.ApplicationRootPath + Path.DirectorySeparatorChar + "GameData"
                          + Path.DirectorySeparatorChar + "ModuleManager.ConfigCache";

        private static string shaPath = KSPUtil.ApplicationRootPath + Path.DirectorySeparatorChar + "GameData"
                          + Path.DirectorySeparatorChar + "ModuleManager.ConfigSHA";

        private static string configSha;

        private static bool useCache = false;

        private static Stopwatch patchSw = new Stopwatch();

        private static List<ModuleManagerPostPatchCallback> postPatchCallbacks =
            new List<ModuleManagerPostPatchCallback>();

        public static MMPatchLoader Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null)
            {
                DestroyImmediate(this);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private bool ready;

        public override bool IsReady()
        {
            if (ready)
            {
                patchSw.Stop();
                log("Ran in " + ((float)patchSw.ElapsedMilliseconds / 1000).ToString("F3") + "s");

            }
            return ready;
        }

        public override float ProgressFraction()
        {
            if (totalPatchCount > 0)
                return (appliedPatchCount + needsUnsatisfiedCount) / (float)totalPatchCount;
            return 0;
        }

        public override string ProgressTitle()
        {
            return activity;
        }

        public override void StartLoad()
        {
            StartLoad(false);
        }

        public void Update()
        {
            if (appliedPatchCount > 0)
                StatusUpdate();
        }

        public void StartLoad(bool blocking)
        {
            patchSw.Reset();
            patchSw.Start();

            totalPatchCount = 0;
            appliedPatchCount = 0;
            patchedNodeCount = 0;
            errorCount = 0;
            needsUnsatisfiedCount = 0;
            errorFiles = new Dictionary<string, int>();

        #endregion Top Level - Update

            ready = false;

            if (blocking)
            {
                IEnumerator fib = ProcessPatch();
                while (fib.MoveNext())
                {
                }
            }
            else
                StartCoroutine(ProcessPatch());
        }

        public static void addPostPatchCallback(ModuleManagerPostPatchCallback callback)
        {
            if (!postPatchCallbacks.Contains(callback))
                postPatchCallbacks.Add(callback);
        }
        private List<String> PrePatchInit()
        {
            #region Excluding directories

            // Build a list of subdirectory that won't be processed
            List<String> excludePaths = new List<string>();

            foreach (UrlDir.UrlConfig mod in GameDatabase.Instance.root.AllConfigs)
            {
                if (mod.name == "MODULEMANAGER[LOCAL]")
                {
                    string fullpath = mod.url.Substring(0, mod.url.LastIndexOf('/'));
                    string excludepath = fullpath.Substring(0, fullpath.LastIndexOf('/'));
                    excludePaths.Add(excludepath);
                    log("excludepath: " + excludepath);
                }
            }
            if (excludePaths.Any())
                log("will not process patch in these subdirectories:\n" + String.Join("\n", excludePaths.ToArray()));

            #endregion Excluding directories

            #region List of mods

            List<AssemblyName> modsWithDup =
                AssemblyLoader.loadedAssemblies.Select(a => (a.assembly.GetName())).ToList();

            mods = new List<AssemblyName>();

            foreach (AssemblyName a in modsWithDup)
            {
                if (!mods.Any(m => m.Name == a.Name))
                    mods.Add(a);
            }

            string modlist = "compiling list of loaded mods...\nMod DLLs found:\n";
            foreach (AssemblyName mod in mods)
                modlist += "  " + mod.Name + " v" + mod.Version + "\n";
            modlist += "Non-DLL mods added:\n";
            foreach (UrlDir.UrlConfig cfgmod in GameDatabase.Instance.root.AllConfigs)
            {
                string name;
                if (ParseCommand(cfgmod.type, out name) != Command.Insert)
                {
                    totalPatchCount++;
                    if (name.Contains(":FOR["))
                    {
                        name = RemoveWS(name);

                        // check for FOR[] blocks that don't match loaded DLLs and add them to the pass list
                        try
                        {
                            string dependency = name.Substring(name.IndexOf(":FOR[") + 5);
                            dependency = dependency.Substring(0, dependency.IndexOf(']'));
                            if (mods.Find(a => RemoveWS(a.Name.ToUpper()).Equals(RemoveWS(dependency.ToUpper())))
                                == null)
                            {
                                // found one, now add it to the list.
                                AssemblyName newMod = new AssemblyName(dependency);
                                newMod.Name = dependency;
                                mods.Add(newMod);
                                modlist += "  " + dependency + "\n";
                            }
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            log("Skipping :FOR init for line " + name +
                                ". The line most likely contain a space that should be removed");
                        }
                    }
                }
            }
            modlist += "Mods by directory (sub directories of GameData):\n";
            string gameData = Path.Combine(Path.GetFullPath(KSPUtil.ApplicationRootPath), "GameData");
            foreach (string subdir in Directory.GetDirectories(gameData))
            {
                string name = Path.GetFileName(subdir);
                string upperName = RemoveWS(name.ToUpper());
                if (mods.Find(a => RemoveWS(a.Name.ToUpper()) == upperName) == null)
                {
                    AssemblyName newMod = new AssemblyName(name);
                    newMod.Name = name;
                    mods.Add(newMod);
                    modlist += "  " + name + "\n";
                }
            }
            log(modlist);

            return excludePaths;

            #endregion List of mods
        }

        private IEnumerator ProcessPatch()
        {
            IsCacheUpToDate();
            yield return null;


            if (!useCache)
            {
                #region Check Needs

                List<String> excludePaths = PrePatchInit();

                yield return null;

                // Do filtering with NEEDS
                log("Checking NEEDS.");

                CheckNeeds(excludePaths);

                #endregion Check Needs

                yield return null;

                #region Applying patches

                log("Applying patches");

                // :First node
                yield return StartCoroutine(ApplyPatch(excludePaths, ":FIRST"));

                // any node without a :pass
                yield return StartCoroutine(ApplyPatch(excludePaths, ":LEGACY"));

                foreach (AssemblyName mod in mods)
                {
                    string upperModName = mod.Name.ToUpper();
                    yield return StartCoroutine(ApplyPatch(excludePaths, ":BEFORE[" + upperModName + "]"));
                    yield return StartCoroutine(ApplyPatch(excludePaths, ":FOR[" + upperModName + "]"));
                    yield return StartCoroutine(ApplyPatch(excludePaths, ":AFTER[" + upperModName + "]"));
                }

                // :Final node
                yield return StartCoroutine(ApplyPatch(excludePaths, ":FINAL"));

                PurgeUnused(excludePaths);

                #endregion Applying patches

                #region Logging

                if (errorCount > 0)
                {
                    foreach (String file in errorFiles.Keys)
                    {
                        errors += errorFiles[file] + " error" + (errorFiles[file] > 1 ? "s" : "") + " in GameData/" + file
                                  + "\n";
                    }
                }

                CreateCache();
            }
            else
            {
                log("Loading from Cache");
                LoadCache();
            }

            StatusUpdate();

            log(status + "\n" + errors);

                #endregion Logging

#if DEBUG
            RunTestCases();
#endif

            // TODO : Remove if we ever get a way to load sooner
            log("Reloading ressources definitions");
            PartResourceLibrary.Instance.LoadDefinitions();

            foreach (var callback in postPatchCallbacks)
            {
                try
                {
                    callback();
                }
                catch (Exception e)
                {
                    log("Exception while running a post patch callback\n" + e);
                }
                yield return null;
            }
            yield return null;

            ready = true;
        }

        private void IsCacheUpToDate()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create();
            UrlDir.UrlFile[] files = GameDatabase.Instance.root.AllConfigFiles.ToArray();
            for (int i = 0; i < files.Length; i++)
            {
                // Hash the file path so the checksum change if files are moved
                byte[] pathBytes = Encoding.UTF8.GetBytes(files[i].url);
                sha.TransformBlock(pathBytes, 0, pathBytes.Length, pathBytes, 0);

                // hash the file content
                byte[] contentBytes = File.ReadAllBytes(files[i].fullPath);
                if (i == files.Length - 1)
                    sha.TransformFinalBlock(contentBytes, 0, contentBytes.Length);
                else
                    sha.TransformBlock(contentBytes, 0, contentBytes.Length, contentBytes, 0);
            }

            configSha = BitConverter.ToString(sha.Hash);

            sw.Stop();

            log("SHA generated in " + ((float)sw.ElapsedMilliseconds / 1000).ToString("F3") + "s");
            log("      SHA = " + configSha);

            useCache = false;
            if (File.Exists(shaPath))
            {
                ConfigNode shaConfigNode = ConfigNode.Load(shaPath);
                if (shaConfigNode != null && shaConfigNode.HasValue("SHA") && shaConfigNode.HasValue("version"))
                {
                    string storedSHA = shaConfigNode.GetValue("SHA");
                    string version = shaConfigNode.GetValue("version");
                    useCache = storedSHA.Equals(configSha);
                    useCache = useCache && version.Equals(Assembly.GetExecutingAssembly().GetName().Version.ToString());
                    useCache = useCache && File.Exists(cachePath);
                    log("Cache SHA = " + storedSHA);
                    log("useCache = " + useCache);
                }
            }
        }

        private void CreateCache()
        {
            ConfigNode shaConfigNode = new ConfigNode();
            shaConfigNode.AddValue("SHA", configSha);
            shaConfigNode.AddValue("version", Assembly.GetExecutingAssembly().GetName().Version.ToString());
            shaConfigNode.Save(shaPath);

            ConfigNode cache = new ConfigNode();

            cache.AddValue("patchedNodeCount", patchedNodeCount.ToString());

            foreach (UrlDir.UrlConfig config in GameDatabase.Instance.root.AllConfigs)
            {
                ConfigNode node = cache.AddNode("UrlConfig");
                node.AddValue("name", config.name);
                node.AddValue("type", config.type);
                node.AddValue("parentUrl", config.parent.url);
                node.AddValue("url", config.url);
                node.AddNode(config.config);
            }

            cache.Save(cachePath);
        }

        private void LoadCache()
        {
            // Clear the config DB
            foreach (var files in GameDatabase.Instance.root.AllConfigFiles)
            {
                files.configs.Clear();
            }

            // And then load all the cached configs
            ConfigNode cache = ConfigNode.Load(cachePath);

            if (cache.HasValue("patchedNodeCount"))
                int.TryParse(cache.GetValue("patchedNodeCount"), out patchedNodeCount);

            foreach (ConfigNode node in cache.nodes)
            {
                string name = node.GetValue("name");
                string type = node.GetValue("type");
                string parentUrl = node.GetValue("parentUrl");
                string url = node.GetValue("url");

                UrlDir.UrlFile parent = GameDatabase.Instance.root.AllConfigFiles.FirstOrDefault(f => f.url == parentUrl);
                if (parent != null)
                {
                    parent.AddConfig(node.nodes[0]);
                }
                else
                {
                    log("Parent null for " + parentUrl);
                }
            }
            log("Cache Loaded");
        }

        private void StatusUpdate()
        {
            status = "ModuleManager: " + patchedNodeCount + " patch" + (patchedNodeCount != 1 ? "es" : "") + (useCache ? " loaded from cache" : " applied");

            if (errorCount > 0)
                status += ", found " + errorCount + " error" + (errorCount != 1 ? "s" : "");
        }

        #region Needs checking

        private void CheckNeeds(List<String> excludePaths)
        {
            UrlDir.UrlConfig[] allConfigs = GameDatabase.Instance.root.AllConfigs.ToArray();

            // Check the NEEDS parts first.
            foreach (UrlDir.UrlConfig mod in allConfigs)
            {
                try
                {
                    if (IsPathInList(mod.url, excludePaths))
                        continue;

                    if (mod.type.Contains(":NEEDS["))
                    {
                        mod.parent.configs.Remove(mod);
                        string type = mod.type;

                        if (!CheckNeeds(ref type))
                        {
                            log("Deleting Node in file " + mod.parent.url + " subnode: " + mod.type +
                                " as it can't satisfy its NEEDS");
                            needsUnsatisfiedCount++;
                            continue;
                        }

                        ConfigNode copy = new ConfigNode(type);
                        ShallowCopy(mod.config, copy);
                        mod.parent.configs.Add(new UrlDir.UrlConfig(mod.parent, copy));
                    }

                    // Recursively check the contents
                    CheckNeeds(mod.config, mod.parent.url, new List<string> { mod.type });
                }
                catch (Exception ex)
                {
                    log("Exception while checking needs : " + mod.url + "\n" + ex);
                }
            }
        }

        private void CheckNeeds(ConfigNode subMod, string url, List<string> path)
        {
            try
            {
                path.Add(subMod.name + "[" + subMod.GetValue("name") + "]");

                bool needsCopy = false;
                ConfigNode copy = new ConfigNode();
                for (int i = 0; i < subMod.values.Count; ++i)
                {
                    ConfigNode.Value val = subMod.values[i];
                    string name = val.name;
                    if (CheckNeeds(ref name))
                        copy.AddValue(name, val.value);
                    else
                    {
                        needsCopy = true;
                        log("Deleting value in file: " + url + " subnode: " + string.Join("/", path.ToArray()) +
                            " value: " + val.name + " = " + val.value + " as it can't satisfy its NEEDS");
                        needsUnsatisfiedCount++;
                    }
                }

                for (int i = 0; i < subMod.nodes.Count; ++i)
                {
                    ConfigNode node = subMod.nodes[i];
                    string name = node.name;
                    if (CheckNeeds(ref name))
                    {
                        node.name = name;
                        CheckNeeds(node, url, path);
                        copy.AddNode(node);
                    }
                    else
                    {
                        needsCopy = true;
                        log("Deleting node in file: " + url + " subnode: " + string.Join("/", path.ToArray()) + "/" +
                            node.name + " as it can't satisfy its NEEDS");
                        needsUnsatisfiedCount++;
                    }
                }

                if (needsCopy)
                    ShallowCopy(copy, subMod);
            }
            finally
            {
                path.RemoveAt(path.Count - 1);
            }
        }

        /// <summary>
        /// Returns true if needs are satisfied.
        /// </summary>
        private bool CheckNeeds(ref string name)
        {
            if (name == null)
                return true;

            int idxStart = name.IndexOf(":NEEDS[");
            if (idxStart < 0)
                return true;
            int idxEnd = name.IndexOf(']', idxStart + 7);
            string needsString = name.Substring(idxStart + 7, idxEnd - idxStart - 7).ToUpper();

            name = name.Substring(0, idxStart) + name.Substring(idxEnd + 1);

            // Check to see if all the needed dependencies are present.
            foreach (string andDependencies in needsString.Split(',', '&'))
            {
                bool orMatch = false;
                foreach (string orDependency in andDependencies.Split('|'))
                {
                    if (orDependency.Length == 0)
                        continue;

                    bool not = orDependency[0] == '!';
                    string toFind = not ? orDependency.Substring(1) : orDependency;
                    bool found = mods.Find(a => a.Name.ToUpper() == toFind) != null;

                    if (not == !found)
                    {
                        orMatch = true;
                        break;
                    }
                }
                if (!orMatch)
                    return false;
            }

            return true;
        }

        private void PurgeUnused(List<string> excludePaths)
        {
            foreach (UrlDir.UrlConfig mod in GameDatabase.Instance.root.AllConfigs.ToArray())
            {
                if (IsPathInList(mod.url, excludePaths))
                    continue;

                string name = RemoveWS(mod.type);

                if (ParseCommand(name, out name) != Command.Insert)
                    mod.parent.configs.Remove(mod);
            }
        }

        #endregion Needs checking

        #region Applying Patches

        // Apply patch to all relevent nodes
        public IEnumerator ApplyPatch(List<String> excludePaths, string Stage)
        {
            log(Stage + (Stage == ":LEGACY" ? " (default) pass" : " pass"));

            activity = "ModuleManager " + Stage;

            UrlDir.UrlConfig[] allConfigs = GameDatabase.Instance.root.AllConfigs.ToArray();

            int yieldRate = Math.Max(allConfigs.Length / 4, 10);

            for (int modsIndex = 0; modsIndex < allConfigs.Length; modsIndex++)
            {
                UrlDir.UrlConfig mod = allConfigs[modsIndex];
                int lastErrorCount = errorCount;
                try
                {
                    string name = RemoveWS(mod.type);
                    string tmp;
                    Command cmd = ParseCommand(name, out tmp);

                    if (cmd != Command.Insert)
                    {
                        if (!IsBracketBalanced(mod.type))
                        {
                            log(
                                "Skipping a patch with unbalanced square brackets or a space (replace them with a '?') :\n" +
                                mod.name + "\n");
                            errorCount++;

                            // And remove it so it's not tried anymore
                            mod.parent.configs.Remove(mod);
                            continue;
                        }

                        // Ensure the stage is correct
                        string upperName = name.ToUpper();

                        int stageIdx = upperName.IndexOf(Stage);
                        if (stageIdx >= 0)
                            name = name.Substring(0, stageIdx) + name.Substring(stageIdx + Stage.Length);
                        else if (
                            !((upperName.Contains(":FIRST") || Stage == ":LEGACY")
                              && !upperName.Contains(":BEFORE[") && !upperName.Contains(":FOR[")
                              && !upperName.Contains(":AFTER[") && !upperName.Contains(":FINAL")))
                            continue;

                        // TODO: do we want to ensure there's only one phase specifier?

                        try
                        {
                            char[] sep = { '[', ']' };
                            string condition = "";

                            if (upperName.Contains(":HAS["))
                            {
                                int start = upperName.IndexOf(":HAS[");
                                condition = name.Substring(start + 5, name.LastIndexOf(']') - start - 5);
                                name = name.Substring(0, start);
                            }

                            string[] splits = name.Split(sep, 3);
                            string[] patterns = splits.Length > 1 ? splits[1].Split(',', '|') : new string[] { null };
                            string type = splits[0].Substring(1);

                            foreach (UrlDir.UrlConfig url in GameDatabase.Instance.root.AllConfigs.ToArray())
                            {
                                foreach (string pattern in patterns)
                                {
                                    if (url.type == type && WildcardMatch(url.name, pattern)
                                        && CheckConstraints(url.config, condition) && !IsPathInList(mod.url, excludePaths))
                                    {
                                        nodeStack.Clear();
                                        switch (cmd)
                                        {
                                            case Command.Edit:
                                                log("Applying node " + mod.url + " to " + url.url);
                                                patchedNodeCount++;
                                                url.config = ModifyNode(url.config, mod.config);
                                                break;

                                            case Command.Copy:
                                                ConfigNode clone = ModifyNode(url.config, mod.config);
                                                if (url.config.name != mod.name)
                                                {
                                                    log("Copying Node " + url.config.name + " into " + clone.name);
                                                    url.parent.configs.Add(new UrlDir.UrlConfig(url.parent, clone));
                                                }
                                                else
                                                {
                                                    errorCount++;
                                                    log("Error while processing " + mod.config.name +
                                                        " the copy needs to have a different name than the parent (use @name = xxx)");
                                                }
                                                break;

                                            case Command.Delete:
                                                log("Deleting Node " + url.config.name);
                                                url.parent.configs.Remove(url);
                                                break;

                                            case Command.Replace:

                                                // TODO: do something sensible here.
                                                break;
                                        }
                                    }
                                }
                            }
                        }
                        finally
                        {
                            // The patch was either run or has failed, in any case let's remove it from the database
                            appliedPatchCount++;
                            mod.parent.configs.Remove(mod);
                        }
                    }
                }
                catch (Exception e)
                {
                    log("Exception while processing node : " + mod.url + "\n" + e);
                    mod.parent.configs.Remove(mod);
                }
                finally
                {
                    if (lastErrorCount < errorCount)
                        addErrorFiles(mod.parent, errorCount - lastErrorCount);
                }
                if (modsIndex % yieldRate == yieldRate - 1)
                    yield return null;
            }
        }

        // Name is group 1, index is group 2, operator is group 3
        private static Regex parseValue = new Regex(@"([\w\?\*\.]*)(?:,(-?[0-9]+))?(?:\s([+\-*/^!]))?");

        // ModifyNode applies the ConfigNode mod as a 'patch' to ConfigNode original, then returns the patched ConfigNode.
        // it uses FindConfigNodeIn(src, nodeType, nodeName, nodeTag) to recurse.
        public ConfigNode ModifyNode(ConfigNode original, ConfigNode mod)
        {
            ConfigNode newNode = DeepCopy(original);

            if (nodeStack.Count == 0)
                topNode = newNode;

            nodeStack.Push(newNode);

            #region Values

            string vals = "[ModuleManager] modding values";
            foreach (ConfigNode.Value modVal in mod.values)
            {
                vals += "\n   " + modVal.name + "= " + modVal.value;

                string valName;
                Command cmd = ParseCommand(modVal.name, out valName);

                Match match = parseValue.Match(valName);
                if (!match.Success)
                {
                    log("Cannot parse value modifying command: " + valName);
                    errorCount++;
                    continue;
                }

                // Get the bits and pieces from the regexp
                valName = match.Groups[1].Value;

                // In this case insert the value at position index (with the same node names)
                int index = 0;
                if (match.Groups[2].Success)
                {
                    // can have "node,n *" (for *= ect)
                    if (!int.TryParse(match.Groups[2].Value, out index))
                    {
                        Debug.LogError("Unable to parse number as number. Very odd.");
                        errorCount++;
                        continue;
                    }
                }

                char op = ' ';
                if (match.Groups[3].Success)
                    op = match.Groups[3].Value[0];

                string varValue;
                switch (cmd)
                {
                    case Command.Insert:
                        if (match.Groups[3].Success)
                        {
                            log("Cannot use operators with insert value: " + mod.name);
                            errorCount++;
                        }
                        else
                        {
                            // Insert at the end by default
                            varValue = ProcessVariableSearch(modVal.value, newNode);
                            if (varValue != null)
                                InsertValue(newNode, match.Groups[2].Success ? index : int.MaxValue, valName, varValue);
                            else
                            {
                                log("Cannot parse variable search when inserting new key " + valName + " = " +
                                    modVal.value);
                                errorCount++;
                            }
                        }
                        break;

                    case Command.Replace:
                        if (match.Groups[2].Success || match.Groups[3].Success || valName.Contains('*')
                            || valName.Contains('?'))
                        {
                            if (match.Groups[2].Success)
                                log("Cannot use index with replace (%) value: " + mod.name);
                            if (match.Groups[3].Success)
                                log("Cannot use operators with replace (%) value: " + mod.name);
                            if (valName.Contains('*') || valName.Contains('?'))
                                log("Cannot use wildcards (* or ?) with replace (%) value: " + mod.name);
                            errorCount++;
                        }
                        else
                        {
                            varValue = ProcessVariableSearch(modVal.value, newNode);
                            if (varValue != null)
                            {
                                newNode.RemoveValues(valName);
                                newNode.AddValue(valName, varValue);
                            }
                            else
                            {
                                log("Cannot parse variable search when replacing (%) key " + valName + " = " +
                                    modVal.value);
                                errorCount++;
                            }
                        }
                        break;

                    case Command.Edit:
                    case Command.Copy:

                        // Format is @key = value or @key *= value or @key += value or @key -= value
                        // or @key,index = value or @key,index *= value or @key,index += value or @key,index -= value

                        varValue = ProcessVariableSearch(modVal.value, newNode);

                        if (varValue != null)
                        {
                            ConfigNode.Value origVal;
                            string value = FindAndReplaceValue(mod, ref valName, varValue, newNode, op, index,
                                out origVal);

                            if (value != null)
                            {
                                if (origVal.value != value)
                                    vals += ": " + origVal.value + " -> " + value;

                                if (cmd != Command.Copy)
                                    origVal.value = value;
                                else
                                    newNode.AddValue(valName, value);
                            }
                        }
                        else
                        {
                            log("Cannot parse variable search when editing key " + valName + " = " + modVal.value);
                            errorCount++;
                        }
                        break;

                    case Command.Delete:
                        if (match.Groups[3].Success)
                        {
                            log("Cannot use operators with delete (- or !) value: " + mod.name);
                            errorCount++;
                        }
                        else if (match.Groups[2].Success)
                        {
                            // If there is an index, use it.
                            ConfigNode.Value v = FindValueIn(newNode, valName, index);
                            if (v != null)
                                newNode.values.Remove(v);
                        }
                        else if (valName.Contains('*') || valName.Contains('?'))
                        {
                            // Delete all matching wildcard
                            ConfigNode.Value last = null;
                            while (true)
                            {
                                ConfigNode.Value v = FindValueIn(newNode, valName, index++);
                                if (v == last)
                                    break;
                                last = v;
                                newNode.values.Remove(v);
                            }
                        }
                        else
                        {
                            // Default is to delete ALL values that match. (backwards compatibility)
                            newNode.RemoveValues(valName);
                        }
                        break;

                    case Command.Rename:
                        if (nodeStack.Count == 1)
                        {
                            log("Renaming nodes does not work on top nodes");
                            errorCount++;
                            break;
                        }
                        newNode.name = modVal.value;
                        break;
                }
            }
            //log(vals);

            #endregion Values

            #region Nodes

            foreach (ConfigNode subMod in mod.nodes)
            {
                subMod.name = RemoveWS(subMod.name);

                if (!IsBracketBalanced(subMod.name))
                {
                    log(
                        "Skipping a patch subnode with unbalanced square brackets or a space (replace them with a '?') in "
                        + mod.name + " : \n" + subMod.name + "\n");
                    errorCount++;
                    continue;
                }

                string subName = subMod.name;
                string tmp;
                Command command = ParseCommand(subName, out tmp);

                if (command == Command.Insert)
                {
                    ConfigNode newSubMod = new ConfigNode(subMod.name);
                    newSubMod = ModifyNode(newSubMod, subMod);
                    int index;
                    if (subName.Contains(",") && int.TryParse(subName.Split(',')[1], out index))
                    {
                        // In this case insert the value at position index (with the same node names)
                        subMod.name = subName.Split(',')[0];
                        InsertNode(newNode, newSubMod, index);
                    }
                    else
                        newNode.AddNode(newSubMod);
                }
                else
                {
                    string constraints = "";
                    string tag = "";
                    string nodeType, nodeName;
                    int index = 0;
                    string msg = "";

                    List<ConfigNode> subNodes = new List<ConfigNode>();

                    // three ways to specify:
                    // NODE,n will match the nth node (NODE is the same as NODE,0)
                    // NODE,* will match ALL nodes
                    // NODE:HAS[condition] will match ALL nodes with condition
                    if (subName.Contains(":HAS["))
                    {
                        int start = subName.IndexOf(":HAS[");
                        constraints = subName.Substring(start + 5, subName.LastIndexOf(']') - start - 5);
                        subName = subName.Substring(0, start);
                    }
                    else if (subName.Contains(","))
                    {
                        tag = subName.Split(',')[1];
                        subName = subName.Split(',')[0];
                        int.TryParse(tag, out index);
                    }

                    if (subName.Contains("["))
                    {
                        // format @NODETYPE[Name] {...}
                        // or @NODETYPE[Name, index] {...}
                        nodeType = subName.Substring(1).Split('[')[0];
                        nodeName = subName.Split('[')[1].Replace("]", "");
                    }
                    else
                    {
                        // format @NODETYPE {...} or ! instead of @
                        nodeType = subName.Substring(1);
                        nodeName = null;
                    }

                    if (tag == "*" || constraints.Length > 0)
                    {
                        // get ALL nodes
                        if (command == Command.Replace)
                            msg += "  cannot wildcard a % node: " + subMod.name + "\n";
                        else
                        {
                            ConfigNode n, last = null;
                            while (true)
                            {
                                n = FindConfigNodeIn(newNode, nodeType, nodeName, index++);
                                if (n == last || n == null)
                                    break;
                                if (CheckConstraints(n, constraints))
                                    subNodes.Add(n);
                                last = n;
                            }
                        }
                    }
                    else
                    {
                        // just get one node
                        ConfigNode n = FindConfigNodeIn(newNode, nodeType, nodeName, index);
                        if (n != null)
                            subNodes.Add(n);
                    }

                    if (command != Command.Replace)
                    {
                        // find each original subnode to modify, modify it and add the modified.
                        if (subNodes.Count == 0) // no nodes to modify!
                            msg += "  Could not find node(s) to modify: " + subMod.name + "\n";

                        foreach (ConfigNode subNode in subNodes)
                        {
                            msg += "  Applying subnode " + subMod.name + "\n";
                            ConfigNode newSubNode;
                            switch (command)
                            {
                                case Command.Edit:

                                    // Edit in place
                                    newSubNode = ModifyNode(subNode, subMod);
                                    subNode.ClearData();
                                    newSubNode.CopyTo(subNode, newSubNode.name);
                                    break;

                                case Command.Delete:

                                    // Delete the node
                                    newNode.nodes.Remove(subNode);
                                    break;

                                case Command.Copy:

                                    // Copy the node
                                    newSubNode = ModifyNode(subNode, subMod);
                                    newNode.nodes.Add(newSubNode);
                                    break;
                            }
                        }
                    }
                    else // command == Command.Replace
                    {
                        // if the original exists modify it
                        if (subNodes.Count > 0)
                        {
                            msg += "  Applying subnode " + subMod.name + "\n";
                            ConfigNode newSubNode = ModifyNode(subNodes[0], subMod);
                            subNodes[0].ClearData();
                            newSubNode.CopyTo(subNodes[0], newSubNode.name);
                        }
                        else
                        {
                            // if not add the mod node without the % in its name
                            msg += "  Adding subnode " + subMod.name + "\n";

                            ConfigNode copy = new ConfigNode(nodeType);

                            if (nodeName != null)
                                copy.AddValue("name", nodeName);

                            ConfigNode newSubNode = ModifyNode(copy, subMod);
                            newNode.nodes.Add(newSubNode);
                        }
                    }

                    //print(msg);
                }
            }

            #endregion Nodes

            nodeStack.Pop();

            return newNode;
        }

        // KeyName is group 1, index is group 2, value indexis  group 3, value separator is group 4
        private static readonly Regex parseVarKey = new Regex(@"(\w+)(?:,((?:[0-9]+)+))?(?:\[((?:[0-9]+)+)(?:,(.))?\])?");

        // Search for a value by a path alike string
        private static string RecurseVariableSearch(string path, ConfigNode currentNode)
        {
            //log("path:" + path);
            if (path[0] == '/')
                return RecurseVariableSearch(path.Substring(1), topNode);
            int nextSep = path.IndexOf('/');

            // make sure we don't stop on a ",/" which would be a value separator
            // it's a hack that should be replaced with a proper regex for the whole node search
            while (nextSep > 0 && path[nextSep - 1] == ',')
                nextSep = path.IndexOf('/', nextSep + 1);

            if (path[0] == '@')
            {
                if (nextSep < 2)
                    return null;

                string subName = path.Substring(1, nextSep - 1);
                string nodeType, nodeName;
                ConfigNode target = null;

                if (subName.Contains("["))
                {
                    // @NODETYPE[Name]/
                    nodeType = subName.Split('[')[0];
                    nodeName = subName.Split('[')[1].Replace("]", "");
                }
                else
                {
                    // @NODETYPE/
                    nodeType = subName;
                    nodeName = string.Empty;
                }

                ConfigNode[] list = GameDatabase.Instance.GetConfigNodes(nodeType);
                if (list.Length == 0)
                {
                    log("Can't find nodeType:" + nodeType);
                    return null;
                }

                if (nodeName == string.Empty)
                {
                    target = list[0];
                }
                else
                {
                    for (int i = 0; i < list.Length; i++)
                    {
                        if (list[i].HasValue("name") && WildcardMatch(list[i].GetValue("name"), nodeName))
                        {
                            target = list[i];
                            break;
                        }
                    }
                }
                return target != null ? RecurseVariableSearch(path.Substring(nextSep + 1), target) : null;
            }
            if (path.StartsWith("../"))
            {
                if (nodeStack.Count == 1)
                    return null;
                string result;
                ConfigNode top = nodeStack.Pop();
                try
                {
                    result = RecurseVariableSearch(path.Substring(3), nodeStack.Peek());
                }
                finally
                {
                    nodeStack.Push(top);
                }
                return result;
            }

            // Node search
            if (nextSep > 0 && path[nextSep - 1] != ',')
            {
                // Big case of code duplication here ...
                // TODO : replace with a regex

                string subName = path.Substring(0, nextSep);
                string constraint = "";
                string nodeType, nodeName;
                int index = 0;
                if (subName.Contains(":HAS["))
                {
                    int start = subName.IndexOf(":HAS[");
                    constraint = subName.Substring(start + 5, subName.LastIndexOf(']') - start - 5);
                    subName = subName.Substring(0, start);
                }
                else if (subName.Contains(","))
                {
                    string tag = subName.Split(',')[1];
                    subName = subName.Split(',')[0];
                    int.TryParse(tag, out index);
                }

                if (subName.Contains("["))
                {
                    // format NODETYPE[Name] {...}
                    // or NODETYPE[Name, index] {...}
                    nodeType = subName.Split('[')[0];
                    nodeName = subName.Split('[')[1].Replace("]", "");
                }
                else
                {
                    // format NODETYPE {...}
                    nodeType = subName;
                    nodeName = null;
                }

                if (constraint.Length > 0)
                {
                    // get the first one matching
                    ConfigNode n, last = null;
                    while (true)
                    {
                        n = FindConfigNodeIn(currentNode, nodeType, nodeName, index++);
                        if (n == last || n == null)
                            break;
                        if (CheckConstraints(n, constraint))
                            return RecurseVariableSearch(path.Substring(nextSep + 1), n);
                        last = n;
                    }
                    return null;
                }
                else
                {
                    // just get one node
                    ConfigNode n = FindConfigNodeIn(currentNode, nodeType, nodeName, index);
                    if (n != null)
                        return RecurseVariableSearch(path.Substring(nextSep + 1), n);
                    return null;
                }
            }

            // Value search

            Match match = parseVarKey.Match(path);
            if (!match.Success)
            {
                log("Cannot parse variable search command: " + path);
                return null;
            }

            string valName = match.Groups[1].Value;

            int idx = 0;
            if (match.Groups[2].Success)
                int.TryParse(match.Groups[2].Value, out idx);

            ConfigNode.Value cVal = FindValueIn(currentNode, valName, idx);
            if (cVal == null)
            {
                log("Cannot find key " + valName + " in " + currentNode.name);
                return null;
            }
            string value = cVal.value;

            if (match.Groups[3].Success)
            {
                int splitIdx = 0;
                int.TryParse(match.Groups[3].Value, out splitIdx);

                char sep = ',';
                if (match.Groups[4].Success)
                    sep = match.Groups[4].Value[0];
                string[] split = value.Split(sep);
                if (splitIdx < split.Length)
                    value = split[splitIdx];
            }
            return value;
        }

        private static string ProcessVariableSearch(string value, ConfigNode node)
        {
            // value = #xxxx$yyyyy$zzzzz$aaaa$bbbb
            // There is 2 or more '$'
            if (value[0] == '#' && value.IndexOf('$') != value.LastIndexOf('$'))
            {
                //log("variable search input : =\"" + value + "\"");
                string[] split = value.Split('$');

                if (split.Length % 2 != 1)
                    return null;

                StringBuilder builder = new StringBuilder();
                builder.Append(split[0].Substring(1));

                for (int i = 1; i < split.Length - 1; i = i + 2)
                {
                    string result = RecurseVariableSearch(split[i], node);
                    if (result == null)
                        return null;
                    builder.Append(result);
                    builder.Append(split[i + 1]);
                }
                value = builder.ToString();
                //log("variable search output : =\"" + value + "\"");
            }
            return value;
        }

        private static string FindAndReplaceValue(
            ConfigNode mod,
            ref string valName,
            string value,
            ConfigNode newNode,
            char op,
            int index,
            out ConfigNode.Value origVal)
        {
            origVal = FindValueIn(newNode, valName, index);
            if (origVal == null)
                return null;
            string oValue = origVal.value;

            if (op != ' ')
            {
                double s, os;
                if (op == '^')
                {
                    try
                    {
                        string[] split = value.Split(value[0]);

                        Regex replace;
                        if (regexCache.ContainsKey(split[1]))
                            replace = regexCache[split[1]];
                        else
                        {
                            replace = new Regex(split[1], RegexOptions.None);
                            regexCache.Add(split[1], replace);
                        }

                        value = replace.Replace(oValue, split[2]);
                    }
                    catch (Exception ex)
                    {
                        log("Failed to do a regexp replacement: " + mod.name + " : original value=\"" + oValue +
                            "\" regexp=\"" + value +
                            "\" \nNote - to use regexp, the first char is used to subdivide the string (much like sed)\n" +
                            ex);
                        return null;
                    }
                }
                else if (double.TryParse(value, out s) && double.TryParse(oValue, out os))
                {
                    switch (op)
                    {
                        case '*':
                            value = (os * s).ToString();
                            break;

                        case '/':
                            value = (os / s).ToString();
                            break;

                        case '+':
                            value = (os + s).ToString();
                            break;

                        case '-':
                            value = (os - s).ToString();
                            break;

                        case '!':
                            value = Math.Pow(os, s).ToString();
                            break;
                    }
                }
                else
                {
                    log("Failed to do a maths replacement: " + mod.name + " : original value=\"" + oValue +
                        "\" operator=" + op + " mod value=\"" + value + "\"");
                    return null;
                }
            }
            return value;
        }

        #endregion Applying Patches

        #region Command Parsing

        private enum Command
        {
            Insert,

            Delete,

            Edit,

            Replace,

            Copy,

            Rename
        }

        private static Command ParseCommand(string name, out string valueName)
        {
            if (name.Length == 0)
            {
                valueName = string.Empty;
                return Command.Insert;
            }
            Command ret;
            switch (name[0])
            {
                case '@':
                    ret = Command.Edit;
                    break;

                case '%':
                    ret = Command.Replace;
                    break;

                case '-':
                case '!':
                    ret = Command.Delete;
                    break;

                case '+':
                case '$':
                    ret = Command.Copy;
                    break;

                case '|':
                    ret = Command.Rename;
                    break;

                default:
                    valueName = name;
                    return Command.Insert;
            }
            valueName = name.Substring(1);
            return ret;
        }

        #endregion Command Parsing

        #region Sanity checking & Utility functions

        public static bool IsBracketBalanced(String str)
        {
            Stack<char> stack = new Stack<char>();

            char c;
            for (int i = 0; i < str.Length; i++)
            {
                c = str[i];
                if (c == '[')
                    stack.Push(c);
                else if (c == ']')
                {
                    if (stack.Count == 0)
                        return false;
                    if (stack.Peek() == '[')
                        stack.Pop();
                    else
                        return false;
                }
            }
            return stack.Count == 0;
        }

        public static string RemoveWS(string withWhite)
        {
            // Removes ALL whitespace of a string.
            return new string(withWhite.ToCharArray().Where(c => !Char.IsWhiteSpace(c)).ToArray());
        }

        public bool IsPathInList(string modPath, List<String> pathList)
        {
            return pathList.Any(modPath.StartsWith);
        }

        #endregion Sanity checking & Utility functions

        #region Condition checking

        // Split condiction while not getting lost in embeded brackets
        public static List<string> SplitConstraints(string condition)
        {
            condition = RemoveWS(condition) + ",";
            List<string> conditions = new List<string>();
            int start = 0;
            int level = 0;
            for (int end = 0; end < condition.Length; end++)
            {
                if ((condition[end] == ',' || condition[end] == '&') && level == 0)
                {
                    conditions.Add(condition.Substring(start, end - start));
                    start = end + 1;
                }
                else if (condition[end] == '[')
                    level++;
                else if (condition[end] == ']')
                    level--;
            }
            return conditions;
        }

        public static bool CheckConstraints(ConfigNode node, string constraints)
        {
            constraints = RemoveWS(constraints);
            if (constraints.Length == 0)
                return true;

            List<string> constraintList = SplitConstraints(constraints);

            if (constraintList.Count == 1)
            {
                constraints = constraintList[0];

                string remainingConstraints = "";
                if (constraints.Contains("HAS["))
                {
                    int start = constraints.IndexOf("HAS[") + 4;
                    remainingConstraints = constraints.Substring(start, constraintList[0].LastIndexOf(']') - start);
                    constraints = constraints.Substring(0, start - 5);
                }

                char[] sep = { '[', ']' };
                string[] splits = constraints.Split(sep, 3);
                string type = splits[0].Substring(1);
                string name = splits.Length > 1 ? splits[1] : null;

                switch (constraints[0])
                {
                    case '@':
                    case '!':

                        // @MODULE[ModuleAlternator] or !MODULE[ModuleAlternator]
                        bool not = (constraints[0] == '!');
                        ConfigNode subNode = MMPatchLoader.FindConfigNodeIn(node, type, name);
                        if (subNode != null)
                            return not ^ CheckConstraints(subNode, remainingConstraints);
                        return not ^ false;

                    case '#':

                        // #module[Winglet]
                        if (node.HasValue(type) && WildcardMatchValues(node, type, name))
                            return CheckConstraints(node, remainingConstraints);
                        return false;

                    case '~':

                        // ~breakingForce[]  breakingForce is not present
                        // or: ~breakingForce[100]  will be true if it's present but not 100, too.
                        if (name == "" && node.HasValue(type))
                            return false;
                        if (name != "" && WildcardMatchValues(node, type, name))
                            return false;
                        return CheckConstraints(node, remainingConstraints);

                    default:
                        return false;
                }
            }
            return constraintList.TrueForAll(c => CheckConstraints(node, c));
        }

        public static bool WildcardMatchValues(ConfigNode node, string type, string value)
        {
            string[] values = node.GetValues(type);
            for (int i = 0; i < values.Length; i++)
            {
                if (WildcardMatch(values[i], value))
                    return true;
            }
            return false;
        }

        public static bool WildcardMatch(String s, String wildcard)
        {
            if (wildcard == null)
                return true;
            String pattern = "^" + Regex.Escape(wildcard).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";

            Regex regex;
            if (!regexCache.TryGetValue(pattern, out regex))
            {
                regex = new Regex(pattern);
                regexCache.Add(pattern, regex);
            }
            return regex.IsMatch(s);
        }

        #endregion Condition checking

        #region Config Node Utilities

        private static void InsertNode(ConfigNode newNode, ConfigNode subMod, int index)
        {
            string modName = subMod.name;

            ConfigNode[] oldValues = newNode.GetNodes(modName);
            if (index < oldValues.Length)
            {
                newNode.RemoveNodes(modName);
                int i = 0;
                for (; i < index; ++i)
                    newNode.AddNode(oldValues[i]);
                newNode.AddNode(subMod);
                for (; i < oldValues.Length; ++i)
                    newNode.AddNode(oldValues[i]);
            }
            else
                newNode.AddNode(subMod);
        }

        private static void InsertValue(ConfigNode newNode, int index, string name, string value)
        {
            string[] oldValues = newNode.GetValues(name);
            if (index < oldValues.Length)
            {
                newNode.RemoveValues(name);
                int i = 0;
                for (; i < index; ++i)
                    newNode.AddValue(name, oldValues[i]);
                newNode.AddValue(name, value);
                for (; i < oldValues.Length; ++i)
                    newNode.AddValue(name, oldValues[i]);
                return;
            }
            newNode.AddValue(name, value);
        }

        private static void ShallowCopy(ConfigNode from, ConfigNode to)
        {
            to.ClearData();
            foreach (ConfigNode.Value value in from.values)
                to.values.Add(value);
            foreach (ConfigNode node in from.nodes)
                to.nodes.Add(node);
        }

        private static ConfigNode DeepCopy(ConfigNode from)
        {
            ConfigNode to = new ConfigNode(from.name);
            foreach (ConfigNode.Value value in from.values)
                to.AddValue(value.name, value.value);
            foreach (ConfigNode node in from.nodes)
            {
                ConfigNode newNode = DeepCopy(node);
                to.nodes.Add(newNode);
            }
            return to;
        }

        //FindConfigNodeIn finds and returns a ConfigNode in src of type nodeType.
        //If nodeName is not null, it will only find a node of type nodeType with the value name=nodeName.
        //If nodeTag is not null, it will only find a node of type nodeType with the value name=nodeName and tag=nodeTag.
        public static ConfigNode FindConfigNodeIn(
            ConfigNode src,
            string nodeType,
            string nodeName = null,
            int index = 0)
        {
            ConfigNode[] nodes = src.GetNodes(nodeType);
            if (nodes.Length == 0)
                return null;
            if (nodeName == null)
            {
                if (index >= 0)
                    return nodes[Math.Min(index, nodes.Length - 1)];
                return nodes[Math.Max(0, nodes.Length + index)];
            }
            ConfigNode last = null;
            if (index >= 0)
            {
                for (int i = 0; i < nodes.Length; ++i)
                {
                    if (nodes[i].HasValue("name") && WildcardMatch(nodes[i].GetValue("name"), nodeName))
                    {
                        last = nodes[i];
                        if (--index < 0)
                            return last;
                    }
                }
                return last;
            }
            for (int i = nodes.Length - 1; i >= 0; --i)
            {
                if (nodes[i].HasValue("name") && WildcardMatch(nodes[i].GetValue("name"), nodeName))
                {
                    last = nodes[i];
                    if (++index >= 0)
                        return last;
                }
            }
            return last;
        }

        private static ConfigNode.Value FindValueIn(ConfigNode newNode, string valName, int index)
        {
            ConfigNode.Value v = null;
            for (int i = 0; i < newNode.values.Count; ++i)
            {
                if (WildcardMatch(newNode.values[i].name, valName))
                {
                    v = newNode.values[i];
                    if (--index < 0)
                        return v;
                }
            }
            return v;
        }

        private static bool CompareRecursive(ConfigNode expectNode, ConfigNode gotNode)
        {
            if (expectNode.values.Count != gotNode.values.Count || expectNode.nodes.Count != gotNode.nodes.Count)
                return false;
            for (int i = 0; i < expectNode.values.Count; ++i)
            {
                ConfigNode.Value eVal = expectNode.values[i];
                ConfigNode.Value gVal = gotNode.values[i];
                if (eVal.name != gVal.name || eVal.value != gVal.value)
                    return false;
            }
            for (int i = 0; i < expectNode.nodes.Count; ++i)
            {
                ConfigNode eNode = expectNode.nodes[i];
                ConfigNode gNode = gotNode.nodes[i];
                if (!CompareRecursive(eNode, gNode))
                    return false;
            }
            return true;
        }

        #endregion Config Node Utilities

        #region logging

        public void addErrorFiles(UrlDir.UrlFile file, int n = 1)
        {
            string key = file.url + "." + file.fileExtension;
            if (key[0] == '/')
                key = key.Substring(1);
            if (!errorFiles.ContainsKey(key))
                errorFiles.Add(key, n);
            else
                errorFiles[key] = errorFiles[key] + n;
        }

        public static void log(String s)
        {
            print("[ModuleManager] " + s);
        }

        #endregion logging

        #region Tests

        private void RunTestCases()
        {
            log("Running tests...");

            // Do MM testcases
            foreach (UrlDir.UrlConfig expect in GameDatabase.Instance.GetConfigs("MMTEST_EXPECT"))
            {
                // So for each of the expects, we expect all the configs before that node to match exactly.
                UrlDir.UrlFile parent = expect.parent;
                if (parent.configs.Count != expect.config.CountNodes + 1)
                {
                    log("Test " + parent.name + " failed as expected number of nodes differs expected:" +
                        expect.config.CountNodes + " found: " + parent.configs.Count);
                    for (int i = 0; i < parent.configs.Count; ++i)
                        log(parent.configs[i].config.ToString());
                    continue;
                }
                for (int i = 0; i < expect.config.CountNodes; ++i)
                {
                    ConfigNode gotNode = parent.configs[i].config;
                    ConfigNode expectNode = expect.config.nodes[i];
                    if (!CompareRecursive(expectNode, gotNode))
                    {
                        log("Test " + parent.name + "[" + i +
                            "] failed as expected output and actual output differ.\nexpected:\n" + expectNode +
                            "\nActually got:\n" + gotNode);
                    }
                }

                // Purge the tests
                parent.configs.Clear();
            }
            log("tests complete.");
        }

        #endregion Tests
    }
}