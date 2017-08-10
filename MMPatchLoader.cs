using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
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

        //private Texture2D tex;
        //private Texture2D tex2;

        private Sprite[] catFrames;
        private Texture2D rainbow;

        private bool nyan = false;
        private bool nCats = false;

        private PopupDialog menu;

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

        private Stopwatch totalTime = new Stopwatch();

        internal void Awake()
        {
            totalTime.Start();

            // Allow loading the background in the laoding screen
            Application.runInBackground = true;

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

            Version v = Assembly.GetExecutingAssembly().GetName().Version;
            version = v.Major + "." + v.Minor + "." + v.Build;

            // Subscribe to the RnD center spawn/deSpawn events
            GameEvents.onGUIRnDComplexSpawn.Add(OnRnDCenterSpawn);
            GameEvents.onGUIRnDComplexDespawn.Add(OnRnDCenterDeSpawn);


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

                GameObject aGameObject = new GameObject("ModuleManager");
                MMPatchLoader loader = aGameObject.AddComponent<MMPatchLoader>();

                log(string.Format("Adding ModuleManager to the loading screen {0}", list.Count));

                int gameDatabaseIndex = list.FindIndex(s => s is GameDatabase);
                list.Insert(gameDatabaseIndex + 1, loader);
            }

            bool foolsDay = (DateTime.Now.Month == 4 && DateTime.Now.Day == 1);
            bool catDay = (DateTime.Now.Month == 2 && DateTime.Now.Day == 22);
            nyan = foolsDay
                || catDay
                || (DateTime.Now < new DateTime(2016, 11, 1))
                || Environment.GetCommandLineArgs().Contains("-nyan-nyan");

            nCats = catDay
                || Environment.GetCommandLineArgs().Contains("-ncats") ;

            loadedInScene = true;
        }

        private TextMeshProUGUI status;
        private TextMeshProUGUI errors;
        private TextMeshProUGUI warning;


        private void Start()
        {
            SendCatToLaunchBay();

            Canvas canvas = LoadingScreen.Instance.GetComponentInChildren<Canvas>();
            
            status = CreateTextObject(canvas, "MMStatus");
            errors = CreateTextObject(canvas, "MMErrors");
            warning = CreateTextObject(canvas, "MMWarning");
            warning.text = "";

            //if (Versioning.version_major == 1 && Versioning.version_minor == 0 && Versioning.Revision == 5 && Versioning.BuildID == 1024)
            //{
            //    warning.text = "Your KSP 1.0.5 is running on build 1024. You should upgrade to build 1028 to avoid problems with addons.";
            //    //if (GUI.Button(new Rect(Screen.width / 2f - 100, offsetY, 200, 20), "Click to open the Forum thread"))
            //    //    Application.OpenURL("http://forum.kerbalspaceprogram.com/index.php?/topic/124998-silent-patch-for-ksp-105-published/");
            //}
        }

        private TextMeshProUGUI CreateTextObject(Canvas canvas, string name)
        {
            GameObject statusGameObject = new GameObject(name);
            TextMeshProUGUI text = statusGameObject.AddComponent<TextMeshProUGUI>();
            text.text = "STATUS";
            text.fontSize = 16;
            text.autoSizeTextContainer = true;
            text.font = Resources.Load("Fonts/Calibri SDF", typeof(TMP_FontAsset)) as TMP_FontAsset;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.isOverlay = true;
            statusGameObject.transform.SetParent(canvas.transform);

            return text;
        }

        private void SendCatToLaunchBay()
        {
            if (!nyan)
                return;

            // Nyancat are GO !!!

            Texture2D[] tex = new Texture2D[12];
            for (int i = 0; i < tex.Length; i++)
            {
                tex[i] = new Texture2D(70, 42, TextureFormat.ARGB32, false);
            }
            tex[0].LoadImage(Properties.Resources.cat1);
            tex[1].LoadImage(Properties.Resources.cat2);
            tex[2].LoadImage(Properties.Resources.cat3);
            tex[3].LoadImage(Properties.Resources.cat4);
            tex[4].LoadImage(Properties.Resources.cat5);
            tex[5].LoadImage(Properties.Resources.cat6);
            tex[6].LoadImage(Properties.Resources.cat7);
            tex[7].LoadImage(Properties.Resources.cat8);
            tex[8].LoadImage(Properties.Resources.cat9);
            tex[9].LoadImage(Properties.Resources.cat10);
            tex[10].LoadImage(Properties.Resources.cat11);
            tex[11].LoadImage(Properties.Resources.cat12);

            rainbow = new Texture2D(39, 36, TextureFormat.ARGB32, false);
            rainbow.LoadImage(Properties.Resources.rainbow);
            rainbow.Apply();

            catFrames = new Sprite[12];

            for (int i = 0; i < tex.Length; i++)
            {
                tex[i].Apply();
                catFrames[i] = Sprite.Create(tex[i], new Rect(0, 0, tex[i].width, tex[i].height), new Vector2(.5f, .5f));
                catFrames[i].name = "cat" + i;
            }

            int scale = 1;
            if (Screen.height >= 1080)
                scale *= 2;
            if (Screen.height > 1440)
                scale *= 3;

            Physics2D.gravity = Vector2.zero;


            if (!nCats)
            {
                GameObject cat = LaunchCat(scale);
                CatMover catMover = cat.AddComponent<CatMover>();
            }
            else
            {
                GameObject catSun = LaunchCat(scale);
                CatOrbiter catSunOrbiter = catSun.AddComponent<CatOrbiter>();
                catSunOrbiter.Init(null,0);

                int cats = UnityEngine.Random.Range(6, 10);
                for (int i = 0; i < cats; i++)
                {
                    GameObject cat = LaunchCat(scale);
                    CatOrbiter catOrbiter = cat.AddComponent<CatOrbiter>();
                    catOrbiter.Init(catSunOrbiter, Screen.height * 0.5f);

                    int moons = UnityEngine.Random.Range(0, 4);

                    for (int j = 0; j < moons; j++)
                    {
                        GameObject catMoon = LaunchCat(scale);
                        CatOrbiter catMoonOrbiter = catMoon.AddComponent<CatOrbiter>();
                        catMoonOrbiter.Init(catOrbiter, Screen.height * 0.06f);
                    }
                }
            }
        }

        private GameObject LaunchCat(int scale)
        {
            GameObject cat = new GameObject("NyanCat");
            SpriteRenderer sr = cat.AddComponent<SpriteRenderer>();
            TrailRenderer trail = cat.AddComponent<TrailRenderer>();
            CatAnimator catAnimator = cat.AddComponent<CatAnimator>();

            sr.sprite = catFrames[0];

            trail.material = new Material(Shader.Find("Particles/Alpha Blended"));

            Debug.Log("material = " + trail.material);
            trail.material.mainTexture = rainbow;
            trail.time = 1.5f;
            trail.startWidth = 0.6f * scale  * rainbow.height;
            trail.endWidth = 0.6f * scale * rainbow.height * 0.9f;

            cat.layer = LayerMask.NameToLayer("UI");

            catAnimator.frames = catFrames;

            cat.transform.localScale = 70 * scale * Vector3.one;
            return cat;
        }

        // Unsubscribe from events when the behavior dies
        internal void OnDestroy()
        {
            GameEvents.onGUIRnDComplexSpawn.Remove(OnRnDCenterSpawn);
            GameEvents.onGUIRnDComplexDespawn.Remove(OnRnDCenterDeSpawn);
        }

        internal void Update()
        {
            if (GameSettings.MODIFIER_KEY.GetKey() && Input.GetKeyDown(KeyCode.F11) 
                && (HighLogic.LoadedScene == GameScenes.SPACECENTER || HighLogic.LoadedScene == GameScenes.MAINMENU)
                && !inRnDCenter)
            {
                if (menu == null)
                {
                    menu = PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                        new Vector2(0.5f, 0.5f),
                        new MultiOptionDialog(
                            "ModuleManagerMenu",
                            "",
                            "ModuleManager",
                            HighLogic.UISkin,
                            new Rect(0.5f, 0.5f, 150f, 60f),
                            new DialogGUIFlexibleSpace(),
                            new DialogGUIVerticalLayout(
                                new DialogGUIFlexibleSpace(),
                                new DialogGUIButton("Reload Database",
                                    delegate
                                    {
                                        MMPatchLoader.keepPartDB = false;
                                        StartCoroutine(DataBaseReloadWithMM());
                                    }, 140.0f, 30.0f, true),
                                new DialogGUIButton("Quick Reload Database",
                                    delegate
                                    {
                                        MMPatchLoader.keepPartDB = true;
                                        StartCoroutine(DataBaseReloadWithMM());
                                    }, 140.0f, 30.0f, true),
                                new DialogGUIButton("Dump Database to Files",
                                    delegate
                                    {
                                        StartCoroutine(DataBaseReloadWithMM(true));
                                    }, 140.0f, 30.0f, true),
                                new DialogGUIButton("Close", () => { }, 140.0f, 30.0f, true)
                                )),
                        false,
                        HighLogic.UISkin);
                }
                else
                {
                    menu.Dismiss();
                    menu = null;
                }
            }

            if (totalTime.IsRunning && HighLogic.LoadedScene == GameScenes.MAINMENU)
            {
                totalTime.Stop();
                log("Total loading Time = " + ((float)totalTime.ElapsedMilliseconds / 1000).ToString("F3") + "s");

                Application.runInBackground = GameSettings.SIMULATE_IN_BACKGROUND;
            }

            float offsetY = Mathf.FloorToInt(0.23f * Screen.height);
            float h;
            if (warning)
            {
                warning.transform.localPosition = new Vector3(0, -offsetY);
                h = warning.textBounds.size.y;
                if (h > 0)
                    offsetY = offsetY + h + 10;
            }

            if (status)
            {
                status.transform.localPosition = new Vector3(0, -offsetY);
                status.text = MMPatchLoader.Instance.status;

                h = status.textBounds.size.y;
                if (h > 0)
                    offsetY = offsetY + h + 10;
            }
            
            if (errors)
            {
                errors.transform.localPosition = new Vector3(0, -offsetY);
                errors.text = MMPatchLoader.Instance.errors;
            }
            
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
        
        internal static IntPtr intPtr = new IntPtr(long.MaxValue);
        /* Not required anymore. At least
        public static bool IsABadIdea()
        {
            return (intPtr.ToInt64() == long.MaxValue) && (Environment.OSVersion.Platform == PlatformID.Win32NT);
        }
        */

        private IEnumerator DataBaseReloadWithMM(bool dump = false)
        {
            reloading = true;

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;

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
                MMPatchLoader.Instance.needsUnsatisfiedCount + " exceptionCount=" + MMPatchLoader.Instance.exceptionCount);

            PartResourceLibrary.Instance.LoadDefinitions();
            
            PartUpgradeManager.Handler.FillUpgrades();

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

            QualitySettings.vSyncCount = GameSettings.SYNC_VBL;
            Application.targetFrameRate = GameSettings.FRAMERATE_LIMIT;
            reloading = false;
            ScreenMessages.PostScreenMessage("Database reloading finished", 1, ScreenMessageStyle.UPPER_CENTER);
        }

        private static void OutputAllConfigs()
        {
            string path = KSPUtil.ApplicationRootPath + Path.DirectorySeparatorChar + "_MMCfgOutput"
                          + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(path);

            foreach (UrlDir.UrlConfig d in GameDatabase.Instance.root.AllConfigs)
            {
                string file = d.url.Replace('/', '.').Replace(':', '.');
                string filePath = path + file + ".cfg";
                try
                {
                    
                    File.WriteAllText(filePath, d.config.ToString());
                }
                catch (Exception e)
                {
                    log("Exception while trying to write the file " + filePath + "\n" + e);
                }
            }
        }

        #endregion GUI stuff.

        public bool ElectionAndCheck()
        {
            #region Type election

            // TODO : Move the old version check in a process that call Update.

            // Check for old version and MMSarbianExt
            IEnumerable<AssemblyLoader.LoadedAssembly> oldMM =
                AssemblyLoader.loadedAssemblies.Where(
                    a => a.assembly.GetName().Name == Assembly.GetExecutingAssembly().GetName().Name)
                    .Where(a => a.assembly.GetName().Version.CompareTo(new System.Version(1, 5, 0)) == -1);
            IEnumerable<AssemblyLoader.LoadedAssembly> oldAssemblies =
                oldMM.Concat(AssemblyLoader.loadedAssemblies.Where(a => a.assembly.GetName().Name == "MMSarbianExt"));
            if (oldAssemblies.Any())
            {
                IEnumerable<string> badPaths =
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
                PopupDialog.SpawnPopupDialog(new Vector2(0f, 1f), new Vector2(0f, 1f), "ModuleManagerOldVersions", "Old versions of Module Manager", status, "OK", false, UISkinManager.defaultSkin);
                log("Old version of Module Manager present. Stopping");
                return false;
            }


            //PopupDialog.SpawnPopupDialog(new Vector2(0.1f, 1f), new Vector2(0.2f, 1f), "Test of the dialog", "Stuff", "OK", false, UISkinManager.defaultSkin);

            Assembly currentAssembly = Assembly.GetExecutingAssembly();
            IEnumerable<AssemblyLoader.LoadedAssembly> eligible = from a in AssemblyLoader.loadedAssemblies
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

    [SuppressMessage("ReSharper", "StringLastIndexOfIsCultureSpecific.1")]
    [SuppressMessage("ReSharper", "StringIndexOfIsCultureSpecific.1")]
    public class MMPatchLoader : LoadingSystem
    {
        public int totalPatchCount = 0;

        public int appliedPatchCount = 0;

        public int patchedNodeCount = 0;

        public int errorCount = 0;

        public int exceptionCount = 0;

        public int needsUnsatisfiedCount = 0;

        private int catEatenCount = 0;

        private Dictionary<String, int> errorFiles;

        private List<string> mods;

        public string status = "";

        public string errors = "";

        public static bool keepPartDB = false;

        private string activity = "Module Manager";

        private static readonly Dictionary<string, Regex> regexCache = new Dictionary<string, Regex>();

        private static readonly Stack<ConfigNode> nodeStack = new Stack<ConfigNode>();

        private static ConfigNode topNode;

        private static string cachePath;

        internal static string techTreeFile;
        internal static string techTreePath;

        internal static string physicsFile;
        internal static string physicsPath;
        private static string defaultPhysicsPath;

        internal static string partDatabasePath;

        private static string shaPath;

        private UrlDir.UrlFile physicsUrlFile;

        private string configSha;
        private Dictionary<string, string> filesSha = new Dictionary<string, string>();

        private bool useCache = false;

        private readonly Stopwatch patchSw = new Stopwatch();

        private static readonly List<ModuleManagerPostPatchCallback> postPatchCallbacks = new List<ModuleManagerPostPatchCallback>();

        private const float yieldInterval = 1f/30f; // Patch at ~30fps

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

            cachePath = KSPUtil.ApplicationRootPath + Path.DirectorySeparatorChar + "GameData" + Path.DirectorySeparatorChar + "ModuleManager.ConfigCache";
            techTreeFile = "GameData" + Path.DirectorySeparatorChar + "ModuleManager.TechTree";
            techTreePath = KSPUtil.ApplicationRootPath + Path.DirectorySeparatorChar + techTreeFile;
            physicsFile = "GameData" + Path.DirectorySeparatorChar + "ModuleManager.Physics";
            physicsPath = KSPUtil.ApplicationRootPath + Path.DirectorySeparatorChar + physicsFile;
            defaultPhysicsPath = KSPUtil.ApplicationRootPath + Path.DirectorySeparatorChar + "Physics.cfg";
            partDatabasePath = KSPUtil.ApplicationRootPath + Path.DirectorySeparatorChar + "PartDatabase.cfg";
            shaPath = KSPUtil.ApplicationRootPath + Path.DirectorySeparatorChar + "GameData" + Path.DirectorySeparatorChar + "ModuleManager.ConfigSHA";
        }

        private bool ready;

        public override bool IsReady()
        {
            //return false;
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
            if (appliedPatchCount > 0 && HighLogic.LoadedScene == GameScenes.LOADING)
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

            // DB check used to track the now fixed TextureReplacer corruption
            //checkValues();

            StartCoroutine(ProcessPatch(blocking), blocking);
        }

        public static void addPostPatchCallback(ModuleManagerPostPatchCallback callback)
        {
            if (!postPatchCallbacks.Contains(callback))
                postPatchCallbacks.Add(callback);
        }
        private List<string> PrePatchInit()
        {
            #region Excluding directories

            // Build a list of subdirectory that won't be processed
            List<string> excludePaths = new List<string>();

            //if (ModuleManager.IsABadIdea())
            //{
            //    foreach (UrlDir.UrlConfig mod in GameDatabase.Instance.root.AllConfigs)
            //    {
            //        if (mod.name == "MODULEMANAGER[NOWIN64]")
            //        {
            //            string fullpath = mod.url.Substring(0, mod.url.LastIndexOf('/'));
            //            string excludepath = fullpath.Substring(0, fullpath.LastIndexOf('/'));
            //            excludePaths.Add(excludepath);
            //            log("excludepath: " + excludepath);
            //        }
            //    }
            //    if (excludePaths.Any())
            //        log("will not process patches in these subdirectories since they were disbaled on KSP Win64:\n" + String.Join("\n", excludePaths.ToArray()));
            //}

            #endregion Excluding directories

            #region List of mods

            //string envInfo = "ModuleManager env info\n";
            //envInfo += "  " + Environment.OSVersion.Platform + " " + ModuleManager.intPtr.ToInt64().ToString("X16") + "\n";
            //envInfo += "  " + Convert.ToString(ModuleManager.intPtr.ToInt64(), 2)  + " " + Convert.ToString(ModuleManager.intPtr.ToInt64() >> 63, 2) + "\n";
            //string gamePath = Environment.GetCommandLineArgs()[0];
            //envInfo += "  Args: " + gamePath.Split(Path.DirectorySeparatorChar).Last() + " " + string.Join(" ", Environment.GetCommandLineArgs().Skip(1).ToArray()) + "\n";
            //envInfo += "  Executable SHA256 " + FileSHA(gamePath);
            //
            //log(envInfo);

            mods = new List<string>();

            string modlist = "compiling list of loaded mods...\nMod DLLs found:\n";

            foreach (AssemblyLoader.LoadedAssembly mod in AssemblyLoader.loadedAssemblies)
            {

                if (string.IsNullOrEmpty(mod.assembly.Location)) //Diazo Edit for xEvilReeperx AssemblyReloader mod
                    continue;

                FileVersionInfo fileVersionInfo = FileVersionInfo.GetVersionInfo(mod.assembly.Location);

                AssemblyName assemblyName = mod.assembly.GetName();

                string modInfo = "  " + assemblyName.Name
                                 + " v" + assemblyName.Version
                                 +
                                 (fileVersionInfo.ProductVersion != " " && fileVersionInfo.ProductVersion != assemblyName.Version.ToString()
                                     ? " / v" + fileVersionInfo.ProductVersion
                                     : "")
                                 +
                                 (fileVersionInfo.FileVersion != " " &&fileVersionInfo.FileVersion != assemblyName.Version.ToString() && fileVersionInfo.FileVersion != fileVersionInfo.ProductVersion
                                     ? " / v" + fileVersionInfo.FileVersion
                                     : "");

                modlist += String.Format("  {0,-50} SHA256 {1}\n", modInfo, FileSHA(mod.assembly.Location));

                if (!mods.Contains(assemblyName.Name, StringComparer.OrdinalIgnoreCase))
                    mods.Add(assemblyName.Name);
            }

            modlist += "Non-DLL mods added (:FOR[xxx]):\n";
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
                            if (!mods.Contains(dependency, StringComparer.OrdinalIgnoreCase))
                            {
                                // found one, now add it to the list.
                                mods.Add(dependency);
                                modlist += "  " + dependency + "\n";
                            }
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            log("Skipping :FOR init for line " + name +
                                ". The line most likely contains a space that should be removed");
                            errorCount++;
                        }
                    }
                }
            }
            modlist += "Mods by directory (sub directories of GameData):\n";
            string gameData = Path.Combine(Path.GetFullPath(KSPUtil.ApplicationRootPath), "GameData");
            foreach (string subdir in Directory.GetDirectories(gameData))
            {
                string name = Path.GetFileName(subdir);
                string cleanName = RemoveWS(name);
                if (!mods.Contains(cleanName, StringComparer.OrdinalIgnoreCase))
                {
                    mods.Add(cleanName);
                    modlist += "  " + cleanName + "\n";
                }
            }
            log(modlist);

            mods.Sort();

            return excludePaths;

            #endregion List of mods
        }


        Coroutine StartCoroutine(IEnumerator enumerator, bool blocking)
        {
            if (blocking)
            {
                while (enumerator.MoveNext()) { }
                return null;
            }
            else
            {
                return StartCoroutine(enumerator);
            }
        }

        private IEnumerator ProcessPatch(bool blocking)
        {
            status = "Checking Cache";
            log(status);
            yield return null;
            
            try
            {
                IsCacheUpToDate();
            }
            catch (Exception ex)
            {
                log("Exception in IsCacheUpToDate : " + ex.Message + "\n" + ex.StackTrace);
                useCache = false;
            }

#if DEBUG
            //useCache = false;
#endif

            status = "Pre patch init";
            log(status);
            yield return null;

            List<string> excludePaths = PrePatchInit();


            if (!useCache)
            {
                yield return null;

                // If we don't use the cache then it is best to clean the PartDatabase.cfg
                if (!keepPartDB && File.Exists(partDatabasePath))
                    File.Delete(partDatabasePath);

                LoadPhysicsConfig();

                #region Check Needs



                // Do filtering with NEEDS
                status = "Checking NEEDS.";
                log(status);
                yield return null;
                CheckNeeds(excludePaths);

                #endregion Check Needs

                #region Applying patches

                status = "Applying patches";
                log(status);

                yield return null;

                // :First node
                yield return StartCoroutine(ApplyPatch(excludePaths, ":FIRST"), blocking);

                // any node without a :pass
                yield return StartCoroutine(ApplyPatch(excludePaths, ":LEGACY"), blocking);

                foreach (string mod in mods)
                {
                    string upperModName = mod.ToUpper();
                    yield return StartCoroutine(ApplyPatch(excludePaths, ":BEFORE[" + upperModName + "]"), blocking);
                    yield return StartCoroutine(ApplyPatch(excludePaths, ":FOR[" + upperModName + "]"), blocking);
                    yield return StartCoroutine(ApplyPatch(excludePaths, ":AFTER[" + upperModName + "]"), blocking);
                }

                // :Final node
                yield return StartCoroutine(ApplyPatch(excludePaths, ":FINAL"), blocking);

                PurgeUnused(excludePaths);

                #endregion Applying patches

                #region Logging

                if (errorCount > 0 || exceptionCount > 0)
                {
                    foreach (string file in errorFiles.Keys)
                    {
                        errors += errorFiles[file] + " error" + (errorFiles[file] > 1 ? "s" : "") + " related to GameData/" + file
                                  + "\n";
                    }
                    
                    log("Errors in patch prevents the creation of the cache");
                    try
                    {
                        if (File.Exists(cachePath))
                            File.Delete(cachePath);
                        if (File.Exists(shaPath))
                            File.Delete(shaPath);
                    }
                    catch (Exception e)
                    {
                        log("Exception while deleting stale cache " + e);
                    }
                }
                else
                {
                    status = "Saving Cache";
                    log(status);
                    yield return null;
                    CreateCache();
                }
                
                SaveModdedTechTree();
                SaveModdedPhysics();
            }
            else
            {
                status = "Loading from Cache";
                log(status);
                yield return null;
                LoadCache();
            }

            StatusUpdate();

            log(status + "\n" + errors);

                #endregion Logging

#if DEBUG
            RunTestCases();
#endif

            // TODO : Remove if we ever get a way to load sooner
            log("Reloading resources definitions");
            PartResourceLibrary.Instance.LoadDefinitions();

            log("Reloading Trait configs");
            GameDatabase.Instance.ExperienceConfigs.LoadTraitConfigs();
            
            log("Reloading Part Upgrades");
            PartUpgradeManager.Handler.FillUpgrades();

            foreach (ModuleManagerPostPatchCallback callback in postPatchCallbacks)
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

            // Call all "public static void ModuleManagerPostLoad()" on all class
            foreach (Assembly ass in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (Type type in ass.GetTypes())
                    {
                        MethodInfo method = type.GetMethod("ModuleManagerPostLoad", BindingFlags.Public | BindingFlags.Static);
                        
                        if (method != null && method.GetParameters().Length == 0)
                        {
                            try
                            {
                                log("Calling " + ass.GetName().Name + "." + type.Name + "." + method.Name + "()");
                                method.Invoke(null, null);
                            }
                            catch (Exception e)
                            {
                                log("Exception while calling " + ass.GetName().Name + "." + type.Name + "." + method.Name + "() :\n" + e);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    log("Post run call threw an exception in loading " + ass.FullName + ": " + e);
                }
            }

            yield return null;

            // Call "public void ModuleManagerPostLoad()" on all active MonoBehaviour instance
            foreach (MonoBehaviour obj in FindObjectsOfType<MonoBehaviour>())
            {
                MethodInfo method = obj.GetType().GetMethod("ModuleManagerPostLoad", BindingFlags.Public | BindingFlags.Instance);

                if (method != null && method.GetParameters().Length == 0)
                {
                    try
                    {
                        log("Calling " + obj.GetType().Name + "." + method.Name + "()");
                        method.Invoke(obj, null);
                    }
                    catch (Exception e)
                    {
                        log("Exception while calling " + obj.GetType().Name + "." + method.Name + "() :\n" + e);
                    }
                }
            }

            yield return null;

            ready = true;
        }

        private void LoadPhysicsConfig()
        {
            log("Loading Physics.cfg");
            UrlDir gameDataDir = GameDatabase.Instance.root.AllDirectories.First(d => d.path.EndsWith("GameData") && d.name == "" && d.url == "");
            // need to use a file with a cfg extenssion to get the right fileType or you can't AddConfig on it
            physicsUrlFile = new UrlDir.UrlFile(gameDataDir, new FileInfo(defaultPhysicsPath));
            // Since it loaded the default config badly (sub node only) we clear it first
            physicsUrlFile.configs.Clear();
            // And reload it properly
            ConfigNode physicsContent = ConfigNode.Load(defaultPhysicsPath);
            physicsContent.name = "PHYSICSGLOBALS";
            physicsUrlFile.AddConfig(physicsContent);
            gameDataDir.files.Add(physicsUrlFile);
        }


        private void SaveModdedPhysics()
        {
            List<UrlDir.UrlConfig> configs = physicsUrlFile.configs;

            if (configs.Count == 0)
            {
                log("No PHYSICSGLOBALS node found. No custom Physics config will be saved");
                return;
            }

            if (configs.Count > 1)
            {
                log(configs.Count + " PHYSICSGLOBALS node found. A patch may be wrong. Using the first one");
            }

            configs[0].config.Save(physicsPath);
        }


        // DB check used to track the now fixed TextureReplacer corruption
        public static void checkValues()
        {
            foreach (UrlDir.UrlConfig mod in GameDatabase.Instance.root.AllConfigs)
            {
                if (checkValues(mod.config))
                {
                    log("Found bad value");
                    return;
                }
            }
            log("Found no bad value");
        }

        static bool checkValues(ConfigNode node)
        {
            foreach (ConfigNode.Value value in node.values)
            {
                if (value.name.Length == -1)
                    return true;
            }

            foreach (ConfigNode subNode in node.nodes)
            {
                if (checkValues(subNode))
                    return true;
            }
            return false;
        }

        private string FileSHA(string filename)
        {
            try
            {
                if (File.Exists(filename))
                {
                    System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create();

                    byte[] data = null;
                    using (FileStream fs = File.Open(filename, FileMode.Open, FileAccess.Read))
                    {
                        data = sha.ComputeHash(fs);
                    }

                    string hashedValue = string.Empty;

                    foreach (byte b in data)
                    {
                        hashedValue += String.Format("{0,2:x2}", b);
                    }

                    return hashedValue;
                }
            }
            catch (Exception e)
            {
                log("Exception hashing file " + filename + "\n" + e.ToString());
                return "0";
            }
            return "0";
        }

        private void IsCacheUpToDate()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create();
            System.Security.Cryptography.SHA256 filesha = System.Security.Cryptography.SHA256.Create();
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

                
                filesha.ComputeHash(contentBytes);
                if (!filesSha.ContainsKey(files[i].url))
                {
                    filesSha.Add(files[i].url, BitConverter.ToString(filesha.Hash));
                }
                else
                {
                    log("Duplicate fileSha key. This should not append. The key is " + files[i].url);
                }
            }

            // Hash the mods dll path so the checksum change if dlls are moved or removed (impact NEEDS)
            foreach (AssemblyLoader.LoadedAssembly dll in AssemblyLoader.loadedAssemblies)
            {
                string path = dll.url + "/" + dll.name;
                byte[] pathBytes = Encoding.UTF8.GetBytes(path);
                sha.TransformBlock(pathBytes, 0, pathBytes.Length, pathBytes, 0);
            }

            configSha = BitConverter.ToString(sha.Hash);
            sha.Clear();
            filesha.Clear();

            sw.Stop();

            log("SHA generated in " + ((float)sw.ElapsedMilliseconds / 1000).ToString("F3") + "s");
            log("      SHA = " + configSha);

            useCache = false;
            if (File.Exists(shaPath))
            {
                ConfigNode shaConfigNode = ConfigNode.Load(shaPath);
                if (shaConfigNode != null && shaConfigNode.HasValue("SHA") && shaConfigNode.HasValue("version") && shaConfigNode.HasValue("KSPVersion"))
                {
                    string storedSHA = shaConfigNode.GetValue("SHA");
                    string version = shaConfigNode.GetValue("version");
                    string kspVersion = shaConfigNode.GetValue("KSPVersion");
                    ConfigNode filesShaNode = shaConfigNode.GetNode("FilesSHA");
                    useCache = CheckFilesChange(files, filesShaNode);
                    useCache = useCache && storedSHA.Equals(configSha);
                    useCache = useCache && version.Equals(Assembly.GetExecutingAssembly().GetName().Version.ToString());
                    useCache = useCache && kspVersion.Equals(Versioning.version_major + "." + Versioning.version_minor + "." + Versioning.Revision + "." + Versioning.BuildID);
                    useCache = useCache && File.Exists(cachePath);
                    useCache = useCache && File.Exists(physicsPath);
                    useCache = useCache && File.Exists(techTreePath);
                    log("Cache SHA = " + storedSHA);
                    log("useCache = " + useCache);
                }
            }
        }

        private bool CheckFilesChange(UrlDir.UrlFile[] files, ConfigNode shaConfigNode)
        {
            bool noChange = true;
            StringBuilder changes = new StringBuilder();
            
            for (int i = 0; i < files.Length; i++)
            {
                ConfigNode fileNode = getFileNode(shaConfigNode, files[i].url);
                string fileSha = fileNode != null ? fileNode.GetValue("SHA") : null;

                if (fileNode == null)
                    continue;

                if (fileSha == null || filesSha[files[i].url] != fileSha)
                {
                    changes.Append("Changed : " + fileNode.GetValue("filename") + ".cfg\n");
                    noChange = false;
                }
            }
            for (int i = 0; i < files.Length; i++)
            {
                ConfigNode fileNode = getFileNode(shaConfigNode, files[i].url);

                if (fileNode == null)
                {
                    changes.Append("Added   : " + files[i].url + ".cfg\n");
                    noChange = false;
                }
                shaConfigNode.RemoveNode(fileNode);
            }
            foreach (ConfigNode fileNode in shaConfigNode.GetNodes())
            {
                changes.Append("Deleted : " + fileNode.GetValue("filename") + ".cfg\n");
                noChange = false;
            }
            if (!noChange)
                log("Changes :\n" + changes.ToString());
            return noChange;
        }

        private ConfigNode getFileNode(ConfigNode shaConfigNode, string filename)
        {
            for (int i = 0; i < shaConfigNode.nodes.Count; i++)
            {
                ConfigNode file = shaConfigNode.nodes[i];
                if (file.name == "FILE" && file.GetValue("filename") == filename)
                    return file;
            }
            return null;
        }
        

        private void CreateCache()
        {
            ConfigNode shaConfigNode = new ConfigNode();
            shaConfigNode.AddValue("SHA", configSha);
            shaConfigNode.AddValue("version", Assembly.GetExecutingAssembly().GetName().Version.ToString());
            shaConfigNode.AddValue("KSPVersion", Versioning.version_major + "." + Versioning.version_minor + "." + Versioning.Revision + "." + Versioning.BuildID);
            ConfigNode filesSHANode = shaConfigNode.AddNode("FilesSHA");

            ConfigNode cache = new ConfigNode();

            cache.AddValue("patchedNodeCount", patchedNodeCount.ToString());

            cache.AddValue("catEatenCount", catEatenCount.ToString());

            foreach (UrlDir.UrlConfig config in GameDatabase.Instance.root.AllConfigs)
            {
                ConfigNode node = cache.AddNode("UrlConfig");
                node.AddValue("name", config.name);
                node.AddValue("type", config.type);
                node.AddValue("parentUrl", config.parent.url);
                node.AddValue("url", config.url);
                node.AddNode(config.config);
            }

            foreach (var file in GameDatabase.Instance.root.AllConfigFiles)
            {
                // "/Physics" is the node we created manually to loads the PHYSIC config
                if (file.url != "/Physics" && filesSha.ContainsKey(file.url))
                {
                    ConfigNode shaNode = filesSHANode.AddNode("FILE");
                    shaNode.AddValue("filename", file.url);
                    shaNode.AddValue("SHA", filesSha[file.url]);
                    filesSha.Remove(file.url);
                }
            }

            log("Saving cache");

            try
            {
                shaConfigNode.Save(shaPath);
            }
            catch (Exception e)
            {
                log("Exception while saving the sha\n" + e.ToString());
            }
            try
            {
                cache.Save(cachePath);
                return;
            }
            catch (NullReferenceException e)
            {
                log("NullReferenceException while saving the cache\n" + e.ToString());
            }
            catch (Exception e)
            {
                log("Exception while saving the cache\n" + e.ToString());
            }

            try
            {
                log("An error occured while creating the cache. Deleting the cache files to avoid keeping a bad cache");
                if (File.Exists(cachePath))
                    File.Delete(cachePath);
                if (File.Exists(shaPath))
                    File.Delete(shaPath);
            }
            catch (Exception e)
            {
                log("Exception while deleting the cache\n" + e.ToString());
            }
        }

        private void SaveModdedTechTree()
        {
            UrlDir.UrlConfig[] configs = GameDatabase.Instance.GetConfigs("TechTree");

            if (configs.Length == 0)
            {
                log("No TechTree node found. No custom TechTree will be saved");
                return;
            }

            if (configs.Length > 1)
            {
                log(configs.Length + " TechTree node found. A patch may be wrong. Using the first one");
            }

            ConfigNode techNode = new ConfigNode("TechTree");
            techNode.AddNode(configs[0].config);
            techNode.Save(techTreePath);
        }

        private void LoadCache()
        {
            // Clear the config DB
            foreach (UrlDir.UrlFile files in GameDatabase.Instance.root.AllConfigFiles)
            {
                files.configs.Clear();
            }

            // And then load all the cached configs
            ConfigNode cache = ConfigNode.Load(cachePath);

            if (cache.HasValue("patchedNodeCount"))
                int.TryParse(cache.GetValue("patchedNodeCount"), out patchedNodeCount);

            if (cache.HasValue("catEatenCount"))
                int.TryParse(cache.GetValue("catEatenCount"), out catEatenCount);


            // Create the fake file where we load the physic config cache
            UrlDir gameDataDir = GameDatabase.Instance.root.AllDirectories.First(d => d.path.EndsWith("GameData") && d.name == "" && d.url == "");
            // need to use a file with a cfg extension to get the right fileType or you can't AddConfig on it
            physicsUrlFile = new UrlDir.UrlFile(gameDataDir, new FileInfo(defaultPhysicsPath));
            gameDataDir.files.Add(physicsUrlFile);

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
                status += ", found <color=orange>" + errorCount + " error" + (errorCount != 1 ? "s" : "") + "</color>";

            if (exceptionCount > 0)
                status += ", encountered <color=red>" + exceptionCount + " exception" + (exceptionCount != 1 ? "s" : "") + "</color>";

            if (catEatenCount > 0)
                status += ", " + catEatenCount + " patch" + (catEatenCount != 1 ? "es were" : " was") + " eaten by the Win64 cat";
        }

        #region Needs checking

        private void CheckNeeds(List<string> excludePaths)
        {
            UrlDir.UrlConfig[] allConfigs = GameDatabase.Instance.root.AllConfigs.ToArray();

            // Check the NEEDS parts first.
            foreach (UrlDir.UrlConfig mod in allConfigs)
            {
                UrlDir.UrlConfig currentMod = mod; 
                try
                {
                    string name;
                    if (IsPathInList(currentMod.url, excludePaths) && (ParseCommand(currentMod.type, out name) != Command.Insert))
                    {
                        mod.parent.configs.Remove(currentMod);
                        catEatenCount++;
                        log("Deleting Node in file " + currentMod.parent.url + " subnode: " + currentMod.type +
                                " as it is set to be disabled on KSP Win64");
                        continue;
                    }

                    if (mod.config.name == null)
                    {
                        log("Error - Node in file " + currentMod.parent.url + " subnode: " + currentMod.type +
                                " has config.name == null");
                    }

                    if (currentMod.type.Contains(":NEEDS["))
                    {
                        mod.parent.configs.Remove(currentMod);
                        string type = currentMod.type;

                        if (!CheckNeeds(ref type))
                        {
                            log("Deleting Node in file " + currentMod.parent.url + " subnode: " + currentMod.type +
                                " as it can't satisfy its NEEDS");
                            needsUnsatisfiedCount++;
                            continue;
                        }

                        ConfigNode copy = new ConfigNode(type);
                        ShallowCopy(currentMod.config, copy);
                        currentMod = new UrlDir.UrlConfig(currentMod.parent, copy);
                        mod.parent.configs.Add(currentMod);
                    }

                    // Recursively check the contents
                    CheckNeeds(currentMod.config, currentMod.parent.url, new List<string>());
                }
                catch (Exception ex)
                {
                    log("Exception while checking needs : " + currentMod.url + " with a type of " + currentMod.type + "\n" + ex);
                    log("Node is : " + PrettyConfig(currentMod));
                    exceptionCount++;
                }
            }
        }

        private void CheckNeeds(ConfigNode subMod, string url, List<string> path)
        {
            try
            {
                path.Add(subMod.name);
                bool needsCopy = false;
                ConfigNode copy = new ConfigNode(subMod.name);
                for (int i = 0; i < subMod.values.Count; ++i)
                {
                    ConfigNode.Value val = subMod.values[i];
                    string valname = val.name;
                    try
                    {
                        if (CheckNeeds(ref valname))
                        {
                            copy.AddValue(valname, val.value);
                        }
                        else
                        {
                            needsCopy = true;
                            log(
                                "Deleting value in file: " + url + " subnode: " + string.Join("/", path.ToArray()) +
                                " value: " + val.name + " = " + val.value + " as it can't satisfy its NEEDS");
                            needsUnsatisfiedCount++;
                        }
                    }
                    catch (ArgumentOutOfRangeException e)
                    {
                        log("ArgumentOutOfRangeException in CheckNeeds for value \"" + val.name + "\"\n" + e);
                        throw;
                    }
                    catch (Exception e)
                    {
                        log("General Exception " + e.GetType().Name + " for value \"" + val.name + " = " + val.value + "\"\n" + e.ToString());
                        throw;
                    }
                }

                for (int i = 0; i < subMod.nodes.Count; ++i)
                {
                    ConfigNode node = subMod.nodes[i];
                    string nodeName = node.name;

                    if (nodeName == null)
                    {
                        log("Error - Node in file " + url + " subnode: " + string.Join("/", path.ToArray()) +
                                " has config.name == null");
                    }

                    try
                    {
                        if (CheckNeeds(ref nodeName))
                        {
                            node.name = nodeName;
                            CheckNeeds(node, url, path);
                            copy.AddNode(node);
                        }
                        else
                        {
                            needsCopy = true;
                            log(
                                "Deleting node in file: " + url + " subnode: " + string.Join("/", path.ToArray()) + "/" +
                                node.name + " as it can't satisfy its NEEDS");
                            needsUnsatisfiedCount++;
                        }
                    }
                    catch (ArgumentOutOfRangeException e)
                    {
                        log("ArgumentOutOfRangeException in CheckNeeds for node \"" + node.name + "\"\n" + e);
                        throw;
                    }
                    catch (Exception e)
                    {
                        log("General Exception " + e.GetType().Name + " for node \"" + node.name + "\"\n " + e.ToString());
                        throw;
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
                    bool found = mods.Contains(toFind, StringComparer.OrdinalIgnoreCase);

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
                string name = RemoveWS(mod.type);

                if (ParseCommand(name, out name) != Command.Insert)
                    mod.parent.configs.Remove(mod);
            }
        }

        #endregion Needs checking

        #region Applying Patches

        // Apply patch to all relevent nodes
        public IEnumerator ApplyPatch(List<string> excludePaths, string Stage)
        {
            StatusUpdate();
            log(Stage + (Stage == ":LEGACY" ? " (default) pass" : " pass"));
            yield return null;

            activity = "ModuleManager " + Stage;

            UrlDir.UrlConfig[] allConfigs = GameDatabase.Instance.root.AllConfigs.ToArray();
            
            float nextYield = Time.realtimeSinceStartup + yieldInterval;

            for (int modsIndex = 0; modsIndex < allConfigs.Length; modsIndex++)
            {
                UrlDir.UrlConfig mod = allConfigs[modsIndex];
                int lastErrorCount = errorCount;
                int lastExceptionCount = exceptionCount;
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
                                "Error - Skipping a patch with unbalanced square brackets or a space (replace them with a '?') :\n" +
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
                                    bool loop = false;
                                    do
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
                                                        log("Error - Error while processing " + mod.config.name +
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

                                                case Command.Create:

                                                    // TODO: something similar to above
                                                    break;
                                            }
                                            // When this special node is found then try to apply the patch once more on the same NODE
                                            if (mod.config.HasNode("MM_PATCH_LOOP"))
                                            {
                                                log("Looping on " + mod.url + " to " + url.url);
                                                loop = true;
                                            }
                                        }
                                        else
                                        {
                                            loop = false;
                                        }
                                    } while (loop);

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
                    exceptionCount++;
                    log("Processed node was\n" + PrettyConfig(mod));
                    mod.parent.configs.Remove(mod);
                }
                finally
                {
                    if (lastErrorCount < errorCount || lastExceptionCount < exceptionCount)
                        addErrorFiles(mod.parent, errorCount - lastErrorCount + exceptionCount - lastExceptionCount);
                }
                if (nextYield < Time.realtimeSinceStartup)
                {
                    nextYield = Time.realtimeSinceStartup + yieldInterval;
                    StatusUpdate();
                    yield return null;
                }
            }
            StatusUpdate();
            yield return null;
        }

        // Name is group 1, index is group 2, vector related filed is group 3, vector separator is group 4, operator is group 5
        private static Regex parseValue = new Regex(@"([\w\&\-\.\?\*]+(?:,[^*\d][\w\&\-\.\?\*]*)*)(?:,(-?[0-9\*]+))?(?:\[((?:[0-9\*]+)+)(?:,(.))?\])?(?:\s([+\-*/^!]))?");

        // Path is group 1, operator is group 5
        private static Regex parseAssign = new Regex(@"(.*)(?:\s)+([+\-*/^!])?");

        // ModifyNode applies the ConfigNode mod as a 'patch' to ConfigNode original, then returns the patched ConfigNode.
        // it uses FindConfigNodeIn(src, nodeType, nodeName, nodeTag) to recurse.
        public ConfigNode ModifyNode(ConfigNode original, ConfigNode mod)
        {
            ConfigNode newNode = DeepCopy(original);

            if (nodeStack.Count == 0)
                topNode = newNode;

            nodeStack.Push(newNode);

            #region Values

            #if LOGSPAM
            string vals = "[ModuleManager] modding values";
            #endif
            foreach (ConfigNode.Value modVal in mod.values)
            {
                #if LOGSPAM
                vals += "\n   " + modVal.name + "= " + modVal.value;
                #endif
                string valName;
                Command cmd = ParseCommand(modVal.name, out valName);

                if (cmd == Command.Special)
                {
                    Match assignMatch = parseAssign.Match(valName);
                    if (!assignMatch.Success)
                    {
                        log("Error - Cannot parse value assigning command: " + valName);
                        errorCount++;
                        continue;
                    }

                    valName = assignMatch.Groups[1].Value;

                    ConfigNode.Value val = RecurseVariableSearch(valName, mod);

                    if (val == null)
                    {
                        log("Error - Cannot find value assigning command: " + valName);
                        errorCount++;
                        continue;
                    }
                    
                    if (assignMatch.Groups[2].Success)
                    {
                        double os, s;
                        if (double.TryParse(modVal.value, out s) && double.TryParse(val.value, out os))
                        {
                            switch (assignMatch.Groups[2].Value[0])
                            {
                                case '*':
                                    val.value = (os * s).ToString();
                                    break;

                                case '/':
                                    val.value = (os / s).ToString();
                                    break;

                                case '+':
                                    val.value = (os + s).ToString();
                                    break;

                                case '-':
                                    val.value = (os - s).ToString();
                                    break;

                                case '!':
                                    val.value = Math.Pow(os, s).ToString();
                                    break;
                            }
                        }
                    }
                    else
                    {
                        val.value = modVal.value;
                    }
                    continue;
                }

                Match match = parseValue.Match(valName);
                if (!match.Success)
                {
                    log("Error - Cannot parse value modifying command: " + valName);
                    errorCount++;
                    continue;
                }

                // Get the bits and pieces from the regexp
                valName = match.Groups[1].Value;

                // Get a position for editing a vector
                int position = 0;
                bool isPosStar = false;
                if (match.Groups[3].Success)
                {
                    if (match.Groups[3].Value == "*")
                        isPosStar = true;
                    else if (!int.TryParse(match.Groups[3].Value, out position))
                    {
                        Debug.LogError("Error - Unable to parse number as number. Very odd.");
                        errorCount++;
                        continue;
                    }
                }
                char seperator = ',';
                if (match.Groups[4].Success)
                {
                    seperator = match.Groups[4].Value[0];
                }

                // In this case insert the value at position index (with the same node names)
                int index = 0;
                bool isStar = false;
                if (match.Groups[2].Success)
                {
                    if (match.Groups[2].Value == "*")
                        isStar = true;
                    // can have "node,n *" (for *= ect)
                    else if (!int.TryParse(match.Groups[2].Value, out index))
                    {
                        Debug.LogError("Error - Unable to parse number as number. Very odd.");
                        errorCount++;
                        continue;
                    }
                }

                int valCount = 0;
                for (int i=0; i<newNode.CountValues; i++)
                    if (newNode.values[i].name == valName)
                        valCount++;

                char op = ' ';
                if (match.Groups[5].Success)
                    op = match.Groups[5].Value[0];

                string varValue;
                switch (cmd)
                {
                    case Command.Insert:
                        if (match.Groups[5].Success)
                        {
                            log("Error - Cannot use operators with insert value: " + mod.name);
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
                                log("Error - Cannot parse variable search when inserting new key " + valName + " = " +
                                    modVal.value);
                                errorCount++;
                            }
                        }
                        break;

                    case Command.Replace:
                        if (match.Groups[2].Success || match.Groups[5].Success || valName.Contains('*')
                            || valName.Contains('?'))
                        {
                            if (match.Groups[2].Success)
                                log("Error - Cannot use index with replace (%) value: " + mod.name);
                            if (match.Groups[5].Success)
                                log("Error - Cannot use operators with replace (%) value: " + mod.name);
                            if (valName.Contains('*') || valName.Contains('?'))
                                log("Error - Cannot use wildcards (* or ?) with replace (%) value: " + mod.name);
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
                                log("Error - Cannot parse variable search when replacing (%) key " + valName + " = " +
                                    modVal.value);
                                errorCount++;
                            }
                        }
                        break;

                    case Command.Edit:
                    case Command.Copy:

                        // Format is @key = value or @key *= value or @key += value or @key -= value
                        // or @key,index = value or @key,index *= value or @key,index += value or @key,index -= value

                        while (index < valCount)
                        {
                            varValue = ProcessVariableSearch(modVal.value, newNode);

                            if (varValue != null)
                            {
                                ConfigNode.Value origVal;
                                string value = FindAndReplaceValue(mod, ref valName, varValue, newNode, op, index,
                                    out origVal, match.Groups[3].Success, position, isPosStar, seperator);

                                if (value != null)
                                {
                                    #if LOGSPAM
                                    if (origVal.value != value)
                                        vals += ": " + origVal.value + " -> " + value;
                                    #endif

                                    if (cmd != Command.Copy)
                                        origVal.value = value;
                                    else
                                        newNode.AddValue(valName, value);
                                }
                            }
                            else
                            {
                                log("Error - Cannot parse variable search when editing key " + valName + " = " + modVal.value);
                                errorCount++;
                            }

                            if (isStar) index++;
                            else break;
                        }
                        break;

                    case Command.Delete:
                        if (match.Groups[5].Success)
                        {
                            log("Error - Cannot use operators with delete (- or !) value: " + mod.name);
                            errorCount++;
                        }
                        else if (match.Groups[2].Success)
                        {
                            while (index < valCount)
                            {
                                // If there is an index, use it.
                                ConfigNode.Value v = FindValueIn(newNode, valName, index);
                                if (v != null)
                                    newNode.values.Remove(v);
                                if (isStar) index++;
                                else break;
                            }
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
                            log("Error - Renaming nodes does not work on top nodes");
                            errorCount++;
                            break;
                        }
                        newNode.name = modVal.value;
                        break;

                    case Command.Create:
                        if (match.Groups[2].Success || match.Groups[5].Success || valName.Contains('*')
                            || valName.Contains('?'))
                        {
                            if (match.Groups[2].Success)
                                log("Error - Cannot use index with create (&) value: " + mod.name);
                            if (match.Groups[5].Success)
                                log("Error - Cannot use operators with create (&) value: " + mod.name);
                            if (valName.Contains('*') || valName.Contains('?'))
                                log("Error - Cannot use wildcards (* or ?) with create (&) value: " + mod.name);
                            errorCount++;
                        }
                        else
                        {
                            varValue = ProcessVariableSearch(modVal.value, newNode);
                            if (varValue != null)
                            {
                                if (!newNode.HasValue(valName))
                                    newNode.AddValue(valName, varValue);
                            }
                            else
                            {
                                log("Error - Cannot parse variable search when replacing (&) key " + valName + " = " +
                                    modVal.value);
                                errorCount++;
                            }
                        }
                        break;
                }
            }
            #if LOGSPAM
            log(vals);
            #endif

            #endregion Values

            #region Nodes

            foreach (ConfigNode subMod in mod.nodes)
            {
                subMod.name = RemoveWS(subMod.name);

                if (!IsBracketBalanced(subMod.name))
                {
                    log(
                        "Error - Skipping a patch subnode with unbalanced square brackets or a space (replace them with a '?') in "
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
                    subName = newSubMod.name;
                    int index;
                    if (subName.Contains(",") && int.TryParse(subName.Split(',')[1], out index))
                    {
                        // In this case insert the node at position index (with the same node names)
                        newSubMod.name = subName.Split(',')[0];
                        InsertNode(newNode, newSubMod, index);
                    }
                    else
                    {
                        newNode.AddNode(newSubMod);
                    }
                }
                else if (command == Command.Paste)
                {
                    //int start = subName.IndexOf('[');
                    //int end = subName.LastIndexOf(']');
                    //if (start == -1 || end == -1 || end - start < 1)
                    //{
                    //    log("Pasting a node require a [path] to the node to paste" + mod.name + " : \n" + subMod.name + "\n");
                    //    errorCount++;
                    //    continue;
                    //}

                    //string newName = subName.Substring(0, start);
                    //string path = subName.Substring(start + 1, end - start - 1);

                    ConfigNode toPaste = RecurseNodeSearch(subName.Substring(1), nodeStack.Peek());

                    if (toPaste == null)
                    {
                        log("Error - Can not find the node to paste in " + mod.name + " : " + subMod.name + "\n");
                        errorCount++;
                        continue;
                    }

                    ConfigNode newSubMod = new ConfigNode(toPaste.name);
                    newSubMod = ModifyNode(newSubMod, toPaste);
                    int index;
                    if (subName.LastIndexOf(",") > 0 && int.TryParse(subName.Substring(subName.LastIndexOf(",") + 1), out index))
                    {
                        // In this case insert the node at position index
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
                    #if LOGSPAM
                    string msg = "";
                    #endif
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

                    if (subName.Contains(","))
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
                        if (command != Command.Replace)
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
#if LOGSPAM
                        else
                            msg += "  cannot wildcard a % node: " + subMod.name + "\n";
#endif
                    }
                    else
                    {
                        // just get one node
                        ConfigNode n = FindConfigNodeIn(newNode, nodeType, nodeName, index);
                        if (n != null)
                            subNodes.Add(n);
                    }
                    
                    if (command == Command.Replace)
                    {
                        // if the original exists modify it
                        if (subNodes.Count > 0)
                        {
                            #if LOGSPAM
                            msg += "  Applying subnode " + subMod.name + "\n";
                            #endif
                            ConfigNode newSubNode = ModifyNode(subNodes[0], subMod);
                            subNodes[0].ClearData();
                            newSubNode.CopyTo(subNodes[0], newSubNode.name);
                        }
                        else
                        {
                            // if not add the mod node without the % in its name
                            #if LOGSPAM
                            msg += "  Adding subnode " + subMod.name + "\n";
                            #endif

                            ConfigNode copy = new ConfigNode(nodeType);

                            if (nodeName != null)
                                copy.AddValue("name", nodeName);

                            ConfigNode newSubNode = ModifyNode(copy, subMod);
                            newNode.nodes.Add(newSubNode);
                        }
                    }
                    else if (command == Command.Create)
                    {
                        if (subNodes.Count == 0)
                        {
                            #if LOGSPAM
                            msg += "  Adding subnode " + subMod.name + "\n";
                            #endif

                            ConfigNode copy = new ConfigNode(nodeType);

                            if (nodeName != null)
                                copy.AddValue("name", nodeName);

                            ConfigNode newSubNode = ModifyNode(copy, subMod);
                            newNode.nodes.Add(newSubNode);
                        }
                    }
                    else
                    {
                        // find each original subnode to modify, modify it and add the modified.
                        #if LOGSPAM
                        if (subNodes.Count == 0) // no nodes to modify!
                            msg += "  Could not find node(s) to modify: " + subMod.name + "\n";
                        #endif

                        foreach (ConfigNode subNode in subNodes)
                        {
                            #if LOGSPAM
                            msg += "  Applying subnode " + subMod.name + "\n";
                            #endif
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
                    #if LOGSPAM
                    print(msg);
                    #endif
                }
            }

            #endregion Nodes

            nodeStack.Pop();

            return newNode;
        }


        // Search for a ConfigNode by a path alike string
        private ConfigNode RecurseNodeSearch(string path, ConfigNode currentNode)
        {
            //log("Path : \"" + path + "\"");

            if (path[0] == '/')
            {
                currentNode = topNode;
                path = path.Substring(1);
            }

            int nextSep = path.IndexOf('/');

            bool root = (path[0] == '@');
            int shift = root ? 1 : 0;
            string subName = (nextSep != -1) ? path.Substring(shift, nextSep - shift) : path.Substring(shift);
            string nodeType, nodeName;
            string constraint = "";

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
                // NODETYPE[Name]
                nodeType = subName.Split('[')[0];
                nodeName = subName.Split('[')[1].Replace("]", "");
            }
            else
            {
                // NODETYPE
                nodeType = subName;
                nodeName = null;
            }

            // ../XXXXX
            if (path.StartsWith("../"))
            {
                if (nodeStack.Count == 1)
                    return null;
                ConfigNode result;
                ConfigNode top = nodeStack.Pop();
                try
                {
                    result = RecurseNodeSearch(path.Substring(3), nodeStack.Peek());
                }
                finally
                {
                    nodeStack.Push(top);
                }
                return result;
            }

            //log("nextSep : \"" + nextSep + " \" root : \"" + root + " \" nodeType : \"" + nodeType + "\" nodeName : \"" + nodeName + "\"");

            // @XXXXX
            if (root)
            {
                ConfigNode[] list = GameDatabase.Instance.GetConfigNodes(nodeType);
                if (list.Length == 0)
                {
                    log("Can't find nodeType:" + nodeType);
                    return null;
                }

                if (nodeName == null)
                {
                    currentNode = list[0];
                }
                else
                {
                    for (int i = 0; i < list.Length; i++)
                    {
                        if (list[i].HasValue("name") && WildcardMatch(list[i].GetValue("name"), nodeName))
                        {
                            currentNode = list[i];
                            break;
                        }
                    }
                }
            }
            else
            {
                if (constraint.Length > 0)
                {
                    // get the first one matching
                    ConfigNode last = null;
                    while (true)
                    {
                        ConfigNode n = FindConfigNodeIn(currentNode, nodeType, nodeName, index++);
                        if (n == last || n == null)
                        {
                            currentNode = null;
                            break;
                        }
                        if (CheckConstraints(n, constraint))
                        {
                            currentNode = n;
                            break;
                        }
                        last = n;
                    }
                }
                else
                {
                    // just get one node
                    currentNode = FindConfigNodeIn(currentNode, nodeType, nodeName, index);
                }
            }

            // XXXXXX/
            if (nextSep > 0 && currentNode != null)
            {
                path = path.Substring(nextSep + 1);
                //log("NewPath : \"" + path + "\"");
                return RecurseNodeSearch(path, currentNode);
            }

            return currentNode;
        }

        // KeyName is group 1, index is group 2, value index is group 3, value separator is group 4
        private static readonly Regex parseVarKey = new Regex(@"([\w\&\-\.]+)(?:,((?:[0-9]+)+))?(?:\[((?:[0-9]+)+)(?:,(.))?\])?");

        // Search for a value by a path alike string
        private static ConfigNode.Value RecurseVariableSearch(string path, ConfigNode currentNode)
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
                ConfigNode.Value result;
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
                    ConfigNode last = null;
                    while (true)
                    {
                        ConfigNode n = FindConfigNodeIn(currentNode, nodeType, nodeName, index++);
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
            
            if (match.Groups[3].Success)
            {
                ConfigNode.Value newVal = new ConfigNode.Value(cVal.name, cVal.value);
                int splitIdx = 0;
                int.TryParse(match.Groups[3].Value, out splitIdx);

                char sep = ',';
                if (match.Groups[4].Success)
                    sep = match.Groups[4].Value[0];
                string[] split = newVal.value.Split(sep);
                if (splitIdx < split.Length)
                    newVal.value = split[splitIdx];
                else
                    newVal.value = "";
                return newVal;
            }
            return cVal;
        }

        private static string ProcessVariableSearch(string value, ConfigNode node)
        {
            // value = #xxxx$yyyyy$zzzzz$aaaa$bbbb
            // There is 2 or more '$'
            if (value.Length > 0 && value[0] == '#' && value.IndexOf('$') != -1 && value.IndexOf('$') != value.LastIndexOf('$'))
            {
                //log("variable search input : =\"" + value + "\"");
                string[] split = value.Split('$');

                if (split.Length % 2 != 1)
                    return null;

                StringBuilder builder = new StringBuilder();
                builder.Append(split[0].Substring(1));

                for (int i = 1; i < split.Length - 1; i = i + 2)
                {
                    ConfigNode.Value result = RecurseVariableSearch(split[i], node);
                    if (result == null || result.value == null)
                        return null;
                    builder.Append(result.value);
                    builder.Append(split[i + 1]);
                }
                value = builder.ToString();
                //log("variable search output : =\"" + value + "\"");
            }
            return value;
        }

        private string FindAndReplaceValue(
            ConfigNode mod,
            ref string valName,
            string value,
            ConfigNode newNode,
            char op,
            int index,
            out ConfigNode.Value origVal,
            bool hasPosIndex = false,
            int posIndex = 0,
            bool hasPosStar = false,
            char seperator = ',')
        {
            origVal = FindValueIn(newNode, valName, index);
            if (origVal == null)
                return null;
            string oValue = origVal.value;

            string[] strArray = new string[] { oValue };
            if (hasPosIndex)
            {
                strArray = oValue.Split(new char[] { seperator }, StringSplitOptions.RemoveEmptyEntries);
                if (posIndex >= strArray.Length)
                {
                    log("Invalid Vector Index!");
                    errorCount++;
                    return null;
                }
            }
            string backupValue = value;
            while (posIndex < strArray.Length)
            {
                value = backupValue;
                oValue = strArray[posIndex];
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
                            log("Error - Failed to do a regexp replacement: " + mod.name + " : original value=\"" + oValue +
                                "\" regexp=\"" + value +
                                "\" \nNote - to use regexp, the first char is used to subdivide the string (much like sed)\n" +
                                ex);
                            errorCount++;
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
                        log("Error - Failed to do a maths replacement: " + mod.name + " : original value=\"" + oValue +
                            "\" operator=" + op + " mod value=\"" + value + "\"");
                        errorCount++;
                        return null;
                    }
                }
                strArray[posIndex] = value;
                if (hasPosStar) posIndex++;
                else break;
            }
            value = String.Join(new string(seperator, 1), strArray);
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

            Rename,

            Paste,

            Special,

            Create
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

                case '#':
                    ret = Command.Paste;
                    break;

                case '*':
                    ret = Command.Special;
                    break;

                case '&':
                    ret = Command.Create;
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

        public static bool IsBracketBalanced(string str)
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

        public bool IsPathInList(string modPath, List<string> pathList)
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

        static readonly char[] contraintSeparators = { '[', ']' };

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

                string[] splits = constraints.Split(contraintSeparators, 3);
                string type = splits[0].Substring(1);
                string name = splits.Length > 1 ? splits[1] : null;

                switch (constraints[0])
                {
                    case '@':
                    case '!':

                        // @MODULE[ModuleAlternator] or !MODULE[ModuleAlternator]
                        bool not = (constraints[0] == '!');

                        bool any = false;
                        int index = 0;
                        ConfigNode last = null;
                        while (true)
                        {
                            ConfigNode subNode = FindConfigNodeIn(node, type, name, index++);
                            if (subNode == last || subNode == null)
                                break;
                            any = any || CheckConstraints(subNode, remainingConstraints);
                            last = subNode;
                        }
                        if (last != null)
                        {
                            //print("CheckConstraints: " + constraints + " " + (not ^ any));
                            return not ^ any;
                        }
                        //print("CheckConstraints: " + constraints + " " + (not ^ false));
                        return not ^ false;

                    case '#':

                        // #module[Winglet]
                        if (node.HasValue(type) && WildcardMatchValues(node, type, name))
                        {
                            bool ret2 = CheckConstraints(node, remainingConstraints);
                            //print("CheckConstraints: " + constraints + " " + ret2);
                            return ret2;
                        }
                        //print("CheckConstraints: " + constraints + " false");
                        return false;

                    case '~':

                        // ~breakingForce[]  breakingForce is not present
                        // or: ~breakingForce[100]  will be true if it's present but not 100, too.
                        if (name == "" && node.HasValue(type))
                        {
                            //print("CheckConstraints: " + constraints + " false");
                            return false;
                        }
                        if (name != "" && WildcardMatchValues(node, type, name))
                        {
                            //print("CheckConstraints: " + constraints + " false");
                            return false;
                        }
                        bool ret = CheckConstraints(node, remainingConstraints);
                        //print("CheckConstraints: " + constraints + " " + ret);
                        return ret;

                    default:
                        //print("CheckConstraints: " + constraints + " false");
                        return false;
                }
            }

            bool ret3 = true;
            foreach (string constraint in constraintList)
            {
                ret3 = ret3 && CheckConstraints(node, constraint);
            }
            //print("CheckConstraints: " + constraints + " " + ret3);
            return ret3;
        }

        public static bool WildcardMatchValues(ConfigNode node, string type, string value)
        {
            double val = 0;
            bool compare = value.Length > 1 && (value[0] == '<' || value[0] == '>');
            compare = compare && Double.TryParse(value.Substring(1), out val);

            string[] values = node.GetValues(type);
            for (int i = 0; i < values.Length; i++)
            {
                if (!compare && WildcardMatch(values[i], value))
                    return true;

                double val2;
                if (compare && Double.TryParse(values[i], out val2)
                    && ((value[0] == '<' && val2 < val) || (value[0] == '>' && val2 > val)))
                {
                    return true;
                }
            }
            return false;
        }

        public static bool WildcardMatch(string s, string wildcard)
        {
            if (wildcard == null)
                return true;
            string pattern = "^" + Regex.Escape(wildcard).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";

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

        private string PrettyConfig(UrlDir.UrlConfig config)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("{0}[{1}]\n", config.type ?? "NULL", config.name ?? "NULL");
            try
            {
                if (config.config != null)
                {
                    PrettyConfig(config.config, ref sb, "  ");
                }
                else
                {
                    sb.Append("NULL\n");
                }
                sb.Append("\n");
            }
            catch (Exception e)
            {
                log("PrettyConfig Exception " + e);
            }
            return sb.ToString();
        }

        private void PrettyConfig(ConfigNode node, ref StringBuilder sb, string indent)
        {
            sb.AppendFormat("{0}{1}\n{2}{{\n", indent, node.name ?? "NULL", indent);
            string newindent = indent + "  ";
            if (node.values != null)
            {
                foreach (ConfigNode.Value value in node.values)
                {
                    if (value != null)
                    {
                        try
                        {
                            sb.AppendFormat("{0}{1} = {2}\n", newindent, value.name ?? "null", value.value ?? "null");
                        }
                        catch (Exception)
                        {
                            log("value.name.Length=" + value.name.Length);
                            log("value.name.IsNullOrEmpty=" + string.IsNullOrEmpty(value.name));
                            log("n " + value.name);
                            log("v " + value.value);
                            throw;
                        }
                    }
                    else
                    {
                        sb.AppendFormat("{0} Null value\n", newindent);
                    }
                }
            }
            else
            {
                sb.AppendFormat("{0} Null values\n", newindent);
            }
            if (node.nodes != null)
            {
                foreach (ConfigNode subnode in node.nodes)
                {
                    if (subnode != null)
                    {
                        PrettyConfig(subnode, ref sb, newindent);
                    }
                    else
                    {
                        sb.AppendFormat("{0} Null Subnode\n", newindent);
                    }
                }
            }
            else
            {
                sb.AppendFormat("{0} Null nodes\n", newindent);
            }
            sb.AppendFormat("{0}}}\n", indent);
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
