using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using KSP;
using UnityEngine;

namespace ModuleManager
{
    // Once MUST be true for the election process to work when 2+ dll of the same version are loaded
    // But I need it to be false for the reload database thingy
    [KSPAddon(KSPAddon.Startup.Instantly, false)]
    public class ModuleManager : MonoBehaviour
    {
        #region state

        private bool inRnDCenter = false;
        private bool reloading = false;

        public bool showUI = false;
        private Rect windowPos = new Rect(80f, 60f, 240f, 40f);

        private string version = "";

        #endregion

        #region Top Level - Update
        private static bool loadedInScene = false;

        internal void OnRnDCenterSpawn()
        {
            inRnDCenter = true;
        }

        internal void OnRnDCenterDespawn()
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
                log("Multiple copies of current version. Using the first copy. Version: " + currentAssembly.GetName().Version);
                Destroy(gameObject);
                return;
            }
            DontDestroyOnLoad(gameObject);

            System.Version v = Assembly.GetExecutingAssembly().GetName().Version;
            version = v.Major.ToString() + "." + v.Minor.ToString() + "." + v.Build.ToString();

            // Subscrive to the RnD center spawn/despawn events
            GameEvents.onGUIRnDComplexSpawn.Add(OnRnDCenterSpawn);
            GameEvents.onGUIRnDComplexDespawn.Add(OnRnDCenterDespawn);

            // This code is the one that should allow to plugin into the stock loading bar
            // but it can't insert the LoadingSystembefore PartLoader ie too late
            // So for now we keep using a blocking update

            LoadingScreen screen = FindObjectOfType<LoadingScreen>();
            if (screen == null)
            {
                log("Can't find LoadingScreen type. Abording ModuleManager execution");
                return;
            }
            Type lsType = typeof(LoadingScreen);
            List<LoadingSystem> list = (
                from fld in lsType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                where fld.FieldType == typeof(List<LoadingSystem>)
                select (List<LoadingSystem>)fld.GetValue(screen)).FirstOrDefault();

            if (list != null)
            {
                // So you can insert a LoadingSystem object in this list at any point.
                // GameDatabase is first in the list, and PartLoader is second
                // We could insert ModuleManager after GameDatabase to get it to run there
                // and SaveGameFixer after PartLoader.

                GameObject aGameObject = new GameObject();
                aGameObject.AddComponent<MMPatchLoader>();

                // it seems Awake is called to late to insert it before the Loading starts :(
                //log("Adding ModuleManager to the loading screen " + list.Count);
                //list.Insert(1, loader);
            }
            else
            {
                Debug.LogWarning("Can't find the LoadingSystem list. Abording ModuleManager execution");
            }

            // if we ever find a way to make the LoadingSystem stuff work we just 
            // have to comment that line and uncomment the Insert a few line higher
            // or whatever we need for the LoadingSystem to be inserted
            StartCoroutine(InitialLoad());

            loadedInScene = true;
        }

        // Unsubscribe from events when the behavior dies
        internal void OnDestroy()
        {
            GameEvents.onGUIRnDComplexSpawn.Remove(OnRnDCenterSpawn);
            GameEvents.onGUIRnDComplexDespawn.Remove(OnRnDCenterDespawn);
        }


        internal void Update()
        {
            if (GameSettings.MODIFIER_KEY.GetKey() && Input.GetKeyDown(KeyCode.F11))
            {
                showUI = !showUI;
            }

            if (reloading)
            {
                float perc = 0;
                if (!GameDatabase.Instance.IsReady())
                    perc = GameDatabase.Instance.ProgressFraction();
                else if (!MMPatchLoader.Instance.IsReady())
                    perc = 1f + MMPatchLoader.Instance.ProgressFraction();
                else if (!PartLoader.Instance.IsReady())
                    perc = 2f + PartLoader.Instance.ProgressFraction();

                int intperc = Mathf.CeilToInt(perc * 100f / 3f);
                ScreenMessages.PostScreenMessage("Database reloading " + intperc + "%", Time.deltaTime, ScreenMessageStyle.UPPER_CENTER);
            }
        }

        #region GUI stuff.

        public void OnGUI()
        {
            if (HighLogic.LoadedScene == GameScenes.LOADING && MMPatchLoader.Instance != null)
            {
                var centeredStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
                centeredStyle.alignment = TextAnchor.UpperCenter;
                centeredStyle.fontSize = 16;
                Vector2 sizeOfLabel = centeredStyle.CalcSize(new GUIContent(MMPatchLoader.Instance.status));
                GUI.Label(new Rect(Screen.width / 2 - (sizeOfLabel.x / 2), Mathf.FloorToInt(0.8f * Screen.height), sizeOfLabel.x, sizeOfLabel.y), MMPatchLoader.Instance.status, centeredStyle);

                if (MMPatchLoader.Instance.errorCount > 0)
                {
                    var errorStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
                    errorStyle.alignment = TextAnchor.UpperLeft;
                    errorStyle.fontSize = 16;
                    Vector2 sizeOfError = errorStyle.CalcSize(new GUIContent(MMPatchLoader.Instance.errors));
                    GUI.Label(new Rect(Screen.width / 2 - (sizeOfLabel.x / 2), Mathf.FloorToInt(0.8f * Screen.height) + sizeOfLabel.y, sizeOfError.x, sizeOfError.y), MMPatchLoader.Instance.errors, errorStyle);
                }
            }

            if (showUI && HighLogic.LoadedScene == GameScenes.SPACECENTER && !inRnDCenter)
            {
                windowPos = GUILayout.Window(GetType().FullName.GetHashCode(), windowPos, WindowGUI, "ModuleManager " + version, GUILayout.Width(200), GUILayout.Height(20));
            }
        }

        protected void WindowGUI(int windowID)
        {
            GUILayout.BeginVertical();

            if (GUILayout.Button("Reload Database"))
            {
                StartCoroutine(DataBaseReloadWithMM());
            }
            if (GUILayout.Button("Dump Database to File"))
            {
                StartCoroutine(DataBaseReloadWithMM(true));
            }
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        // A short coroutine to make sure we skip the first frame 
        // and the "please wait" message is displayed
        IEnumerator InitialLoad()
        {
            yield return null;
            MMPatchLoader.Instance.StartLoad(true);
        }

        IEnumerator DataBaseReloadWithMM(bool dump = false)
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

            log("DB Reload OK with patchCount=" + MMPatchLoader.Instance.patchedNodeCount + " errorCount=" + MMPatchLoader.Instance.errorCount + " needsUnsatisfiedCount=" + MMPatchLoader.Instance.needsUnsatisfiedCount);

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
            string path = KSPUtil.ApplicationRootPath + Path.DirectorySeparatorChar + "_MMCfgOutput" + Path.DirectorySeparatorChar;
            Directory.CreateDirectory(path);

            foreach (var d in GameDatabase.Instance.root.AllConfigs)
            {
                File.WriteAllText(path + d.url.Replace('/', '.') + ".cfg", d.config.ToString());
            }
        }

        #endregion

        public bool ElectionAndCheck()
        {
            #region Type election

            // TODO : Move the old version check in a process that call Update.

            // Check for old version and MMSarbianExt
            var oldMM = AssemblyLoader.loadedAssemblies.Where(a => a.assembly.GetName().Name == Assembly.GetExecutingAssembly().GetName().Name).Where(a => a.assembly.GetName().Version.CompareTo(new System.Version(1, 5, 0)) == -1);
            var oldAssemblies = oldMM.Concat(AssemblyLoader.loadedAssemblies.Where(a => a.assembly.GetName().Name == "MMSarbianExt"));
            if (oldAssemblies.Any())
            {
                var badPaths = oldAssemblies.Select(a => a.path).Select(p => Uri.UnescapeDataString(new Uri(Path.GetFullPath(KSPUtil.ApplicationRootPath)).MakeRelativeUri(new Uri(p)).ToString().Replace('/', Path.DirectorySeparatorChar)));
                string status = "You have old versions of Module Manager (older than 1.5) or MMSarbianExt.\nYou will need to remove them for Module Manager and the mods using it to work\nExit KSP and delete those files :\n" + String.Join("\n", badPaths.ToArray());
                PopupDialog.SpawnPopupDialog("Old versions of Module Manager", status, "OK", false, HighLogic.Skin);
                //loaded = true;
                print("[ModuleManager] Old version of Module Manager present. Stopping");
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
                print("[ModuleManager] version " + currentAssembly.GetName().Version + " at " + currentAssembly.Location + " lost the election");
                Destroy(gameObject);
                return false;
            }
            else
            {
                string candidates = "";
                foreach (AssemblyLoader.LoadedAssembly a in eligible)
                    if (currentAssembly.Location != a.path)
                        candidates += "Version " + a.assembly.GetName().Version + " " + a.path + " " + "\n";
                if (candidates.Length > 0)
                    print("[ModuleManager] version " + currentAssembly.GetName().Version + " at " + currentAssembly.Location + " won the election against\n" + candidates);
            }

            #endregion
            return true;
        }
    }

    public class MMPatchLoader : LoadingSystem
    {
        private static MMPatchLoader _instance;

        //private bool loaded = false;

        public int totalPatchCount = 0;
        public int appliedPatchCount = 0;
        public int patchedNodeCount = 0;
        public int errorCount = 0;
        public int needsUnsatisfiedCount = 0;

        private Dictionary<String, int> errorFiles;
        private List<AssemblyName> mods;

        public string status = "Processing Module Manager patch\nPlease Wait...";
        public string errors = "";

        public static MMPatchLoader Instance
        {
            get
            {
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null)
            {
                DestroyImmediate(this);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private bool ready = false;

        public override bool IsReady()
        {
            return ready;
        }

        public override float ProgressFraction()
        {
            if (totalPatchCount > 0)
                return (float)(appliedPatchCount + needsUnsatisfiedCount) / (float)totalPatchCount;
            else
                return 0;
        }

        public override string ProgressTitle()
        {
            return "Quacking System " + (ready ? "Ready" : "NotReady");
        }

        public override void StartLoad()
        {
            StartLoad(false);
        }

        public void StartLoad(bool blocking)
        {

            //if (!GameDatabase.Instance.IsReady() && ((HighLogic.LoadedScene == GameScenes.MAINMENU) || (HighLogic.LoadedScene == GameScenes.SPACECENTER)))
            //{
            //    return;
            //}

            //if (loaded || PartLoader.Instance.IsReady())
            //    return;

            totalPatchCount = 0;
            appliedPatchCount = 0;
            patchedNodeCount = 0;
            errorCount = 0;
            needsUnsatisfiedCount = 0;
            errorFiles = new Dictionary<string, int>();
            #endregion

            ready = false;

            List<String> excludePaths = PrePatchInit();

            if (blocking)
            {
                IEnumerator fib = ProcessPatch(excludePaths);
                while (fib.MoveNext());
            }
            else
                StartCoroutine(ProcessPatch(excludePaths));

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
                    print("excludepath: " + excludepath);
                }
            }
            if (excludePaths.Any())
                print("[ModuleManager] will not procces patch in these subdirectories:\n" + String.Join("\n", excludePaths.ToArray()));
            #endregion

            #region List of mods
            List<AssemblyName> modsWithDup = AssemblyLoader.loadedAssemblies.Select(a => (a.assembly.GetName())).ToList();

            mods = new List<AssemblyName>();

            foreach (AssemblyName a in modsWithDup)
            {
                if (!mods.Any(m => m.Name == a.Name))
                    mods.Add(a);
            }

            string modlist = "compiling list of loaded mods...\nMod DLLs found:\n";
            foreach (AssemblyName mod in mods)
            {
                modlist += "  " + mod.Name + " v" + mod.Version.ToString() + "\n";
            }
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
                            if (mods.Find(a => RemoveWS(a.Name.ToUpper()).Equals(RemoveWS(dependency.ToUpper()))) == null)
                            { // found one, now add it to the list.
                                AssemblyName newMod = new AssemblyName(dependency);
                                newMod.Name = dependency;
                                mods.Add(newMod);
                                modlist += "  " + dependency + "\n";
                            }
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            print("[ModuleManager] Skipping :FOR init for line " + name + ". The line most likely contain a space that should be removed");
                        }
                    }
                }
            }
            modlist += "Mods by directory (subdirs of GameData):\n";
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

            #endregion
        }

        private IEnumerator ProcessPatch(List<String> excludePaths)
        {
            #region Check Needs
            // Do filtering with NEEDS 
            print("[ModuleManager] Checking NEEDS.");

            CheckNeeds(excludePaths);
            #endregion

            yield return null;

            #region Applying patches
            // :First node (and any node without a :pass)
            ApplyPatch(excludePaths, ":FIRST");

            yield return null;

            foreach (AssemblyName mod in mods)
            {
                string upperModName = mod.Name.ToUpper();
                ApplyPatch(excludePaths, ":BEFORE[" + upperModName + "]");
                yield return null;
                ApplyPatch(excludePaths, ":FOR[" + upperModName + "]");
                yield return null;
                ApplyPatch(excludePaths, ":AFTER[" + upperModName + "]");
                yield return null;
            }

            // :Final node
            ApplyPatch(excludePaths, ":FINAL");

            yield return null;

            PurgeUnused(excludePaths);
            #endregion

            #region Logging
            if (errorCount > 0)
                foreach (String file in errorFiles.Keys)
                    errors += errorFiles[file] + " error" + (errorFiles[file] > 1 ? "s" : "") + " in GameData/" + file + "\n";


            status = "ModuleManager: "
                + patchedNodeCount + " patch" + (patchedNodeCount != 1 ? "es" : "") + " applied"
                + ", "
                + needsUnsatisfiedCount + " hidden item" + (needsUnsatisfiedCount != 1 ? "s" : "");

            if (errorCount > 0)
                status += ", found " + errorCount + " error" + (errorCount != 1 ? "s" : "");

            print("[ModuleManager] " + status + "\n" + errors);

            //loaded = true;
            #endregion

#if DEBUG
            RunTestCases();
#endif

            ready = true;
            yield return null;
        }

        #region Needs checking
        private void CheckNeeds(List<String> excludePaths)
        {
            // Check the NEEDS parts first.
            foreach (UrlDir.UrlConfig mod in GameDatabase.Instance.root.AllConfigs.ToArray())
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
                            print("[ModuleManager] Deleting Node in file " + mod.parent.url + " subnode: " + mod.type + " as it can't satisfy its NEEDS");
                            needsUnsatisfiedCount++;
                            continue;
                        }

                        ConfigNode copy = new ConfigNode(type);
                        ShallowCopy(mod.config, copy);
                        mod.parent.configs.Add(new UrlDir.UrlConfig(mod.parent, copy));
                    }

                    // Recursivly check the contents
                    CheckNeeds(mod.config, mod.parent.url, new List<string>() { mod.type });
                }
                catch (Exception ex)
                {
                    print("[ModuleManager] Exception while checking needs : " + mod.url + "\n" + ex.ToString());
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
                        print("[ModuleManager] Deleting value in file: " + url + " subnode: " + string.Join("/", path.ToArray()) + " value: " + val.name + " = " + val.value + " as it can't satisfy its NEEDS");
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
                        print("[ModuleManager] Deleting node in file: " + url + " subnode: " + string.Join("/", path.ToArray()) + "/" + node.name + " as it can't satisfy its NEEDS");
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

        #endregion

        #region Applying Patches
        // Apply patch to all relevent nodes
        public void ApplyPatch(List<String> excludePaths, string Stage)
        {
            print("[ModuleManager] " + Stage + (Stage == ":FIRST" ? " (default) pass" : " pass"));

            foreach (UrlDir.UrlConfig mod in GameDatabase.Instance.root.AllConfigs.ToArray())
            {
                int lastErrorCount = errorCount;

                try
                {
                    string name = RemoveWS(mod.type);
                    string tmp;
                    Command cmd = ParseCommand(name, out tmp);

                    if (cmd != Command.Insert)
                    {
                        if (!IsBraquetBalanced(mod.type))
                        {
                            print("[ModuleManager] Skipping a patch with unbalanced square brackets or a space (replace them with a '?') :\n" + mod.name + "\n");
                            errorCount++;
                            // And remove it so it's not tried anymore
                            mod.parent.configs.Remove(mod);
                            continue;
                        }

                        // Ensure the stage is correct
                        string upperName = name.ToUpper();

                        int stageIdx = upperName.IndexOf(Stage);
                        if (stageIdx >= 0)
                        {
                            name = name.Substring(0, stageIdx) + name.Substring(stageIdx + Stage.Length);
                        }
                        else if (!(Stage == ":FIRST"
                                    && !upperName.Contains(":BEFORE[")
                                    && !upperName.Contains(":FOR[")
                                    && !upperName.Contains(":AFTER[")
                                    && !upperName.Contains(":FINAL")))
                        {
                            continue;
                        }

                        // TODO: do we want to ensure there's only one phase specifier?

                        try
                        {
                            char[] sep = new char[] { '[', ']' };
                            string cond = "";

                            if (upperName.Contains(":HAS["))
                            {
                                int start = upperName.IndexOf(":HAS[");
                                cond = name.Substring(start + 5, name.LastIndexOf(']') - start - 5);
                                name = name.Substring(0, start);
                            }

                            string[] splits = name.Split(sep, 3);
                            string pattern = splits.Length > 1 ? splits[1] : null;
                            string type = splits[0].Substring(1);

                            foreach (UrlDir.UrlConfig url in GameDatabase.Instance.root.AllConfigs.ToArray())
                            {
                                if (url.type == type
                                    && WildcardMatch(url.name, pattern)
                                    && CheckCondition(url.config, cond)
                                    && !IsPathInList(mod.url, excludePaths)
                                    )
                                {
                                    switch (cmd)
                                    {
                                        case Command.Edit:
                                            print("[ModuleManager] Applying node " + mod.url + " to " + url.url);
                                            patchedNodeCount++;
                                            url.config = ModifyNode(url.config, mod.config);
                                            break;
                                        case Command.Copy:
                                            ConfigNode clone = ModifyNode(url.config, mod.config);
                                            if (url.config.name != mod.name)
                                            {
                                                print("[ModuleManager] Copying Node " + url.config.name + " into " + clone.name);
                                                url.parent.configs.Add(new UrlDir.UrlConfig(url.parent, clone));
                                            }
                                            else
                                            {
                                                errorCount++;
                                                print("[ModuleManager] Error while processing " + mod.config.name + " the copy needs to have a different name than the parent (use @name = xxx)");
                                            }
                                            break;
                                        case Command.Delete:
                                            print("[ModuleManager] Deleting Node " + url.config.name);
                                            url.parent.configs.Remove(url);
                                            break;
                                        case Command.Replace:
                                            // TODO: do something sensible here.
                                            break;
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
                    print("[ModuleManager] Exception while processing node : " + mod.url + "\n" + e.ToString());
                    mod.parent.configs.Remove(mod);
                }
                finally
                {
                    if (lastErrorCount < errorCount)
                        addErrorFiles(mod.parent, errorCount - lastErrorCount);
                }
            }
        }

        // Name is group 1, index is group 2, operator is group 3
        private static Regex parseValue = new Regex(@"([\w\?\*]*)(?:,(-?[0-9]+))?(?:\s([+\-*/^]))?");

        // ModifyNode applies the ConfigNode mod as a 'patch' to ConfigNode original, then returns the patched ConfigNode.
        // it uses FindConfigNodeIn(src, nodeType, nodeName, nodeTag) to recurse.
        public ConfigNode ModifyNode(ConfigNode original, ConfigNode mod)
        {
            ConfigNode newNode = DeepCopy(original);

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
                    print("[ModuleManager] Cannot parse value modifying command: " + valName);
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
                        continue;
                    }
                }

                char op = ' ';
                if (match.Groups[3].Success)
                {
                    op = match.Groups[3].Value[0];
                }

                switch (cmd)
                {
                    case Command.Insert:
                        if (match.Groups[3].Success)
                        {
                            print("[ModuleManager] Cannot use operators with insert value: " + mod.name);
                        }
                        else
                        {
                            // Insert at the end by default
                            InsertValue(newNode, match.Groups[2].Success ? index : int.MaxValue, valName, modVal.value);
                        }
                        break;
                    case Command.Replace:
                        if (match.Groups[2].Success || match.Groups[3].Success || valName.Contains('*') || valName.Contains('?'))
                        {
                            if (match.Groups[2].Success)
                                print("[ModuleManager] Cannot use index with replace (%) value: " + mod.name);
                            if (match.Groups[3].Success)
                                print("[ModuleManager] Cannot use operators with replace (%) value: " + mod.name);
                            if (valName.Contains('*') || valName.Contains('?'))
                                print("[ModuleManager] Cannot use wildcards (* or ?) with replace (%) value: " + mod.name);
                        }
                        else
                        {
                            newNode.RemoveValues(valName);
                            newNode.AddValue(valName, modVal.value);
                        }
                        break;
                    case Command.Edit:
                    case Command.Copy:
                        // Format is @key = value or @key *= value or @key += value or @key -= value 
                        // or @key,index = value or @key,index *= value or @key,index += value or @key,index -= value 

                        ConfigNode.Value origVal;
                        string value = FindAndReplaceValue(mod, ref valName, modVal.value, newNode, op, index, out origVal);

                        if (value != null)
                        {
                            if (origVal.value != value)
                                vals += ": " + origVal.value + " -> " + value;

                            if (cmd != Command.Copy)
                                origVal.value = value;
                            else
                                newNode.AddValue(valName, value);
                        }

                        break;
                    case Command.Delete:
                        if (match.Groups[3].Success)
                            print("[ModuleManager] Cannot use operators with delete (- or !) value: " + mod.name);
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
                }
            }
            //print(vals);
            #endregion

            #region Nodes
            foreach (ConfigNode subMod in mod.nodes)
            {
                subMod.name = RemoveWS(subMod.name);

                if (!IsBraquetBalanced(subMod.name))
                {
                    print("[ModuleManager] Skipping a patch subnode with unbalanced square brackets or a space (replace them with a '?') in " + mod.name + " : \n" + subMod.name + "\n");
                    errorCount++;
                    continue;
                }

                string subName = subMod.name;
                string tmp;
                Command command = ParseCommand(subName, out tmp);

                if (command == Command.Insert)
                {
                    int index = int.MaxValue;
                    if (subName.Contains(",") && int.TryParse(subName.Split(',')[1], out index))
                    {
                        // In this case insert the value at position index (with the same node names)
                        subMod.name = subName = subName.Split(',')[0];

                        InsertNode(newNode, subMod, index);
                    }
                    else
                    {
                        newNode.AddNode(subMod);
                    }
                }
                else
                {
                    string cond = "";
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
                        cond = subName.Substring(start + 5, subName.LastIndexOf(']') - start - 5);
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


                    if (tag == "*" || cond.Length > 0)
                    { // get ALL nodes
                        if (command == Command.Replace)
                        {
                            msg += "  cannot wildcard a % node: " + subMod.name + "\n";
                        }
                        else
                        {
                            ConfigNode n, last = null;
                            while (true)
                            {
                                n = FindConfigNodeIn(newNode, nodeType, nodeName, index++);
                                if (n == last || n == null)
                                    break;
                                if (CheckCondition(n, cond))
                                    subNodes.Add(n);
                                last = n;
                            }
                        }
                    }
                    else
                    { // just get one node
                        ConfigNode n = FindConfigNodeIn(newNode, nodeType, nodeName, index);
                        if (n != null)
                            subNodes.Add(n);
                    }

                    if (command != Command.Replace)
                    { // find each original subnode to modify, modify it and add the modified.

                        if (subNodes.Count == 0)   // no nodes to modify!
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
                                    newSubNode.CopyTo(subNode);
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
                            newSubNode.CopyTo(subNodes[0]);
                        }
                        else
                        { // if not add the mod node without the % in its name                            
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
            #endregion

            return newNode;
        }

        private static string FindAndReplaceValue(ConfigNode mod, ref string valName, string value, ConfigNode newNode, char op, int index, out ConfigNode.Value origVal)
        {
            origVal = FindValueIn(newNode, valName, index);
            if (origVal == null)
                return null;
            string ovalue = origVal.value;

            if (op != ' ')
            {
                double s, os;
                if (op == '^')
                {
                    try
                    {
                        string[] split = value.Split(value[0]);
                        value = Regex.Replace(ovalue, split[1], split[2]);
                    }
                    catch (Exception ex)
                    {
                        print("[ModuleManager] Failed to do a regexp replacement: " + mod.name + " : original value=\"" + ovalue + "\" regexp=\"" + value + "\" \nNote - to use regexp, the first char is used to subdivide the string (much like sed)\n" + ex.ToString());
                        return null;
                    }
                }
                else if (double.TryParse(value, out s) && double.TryParse(ovalue, out os))
                {
                    switch (op)
                    {
                        case '*':
                            value = (s * os).ToString();
                            break;
                        case '/':
                            value = (s / os).ToString();
                            break;
                        case '+':
                            value = (s + os).ToString();
                            break;
                        case '-':
                            value = (s - os).ToString();
                            break;
                    }
                }
                else
                {
                    print("[ModuleManager] Failed to do a maths replacement: " + mod.name + " : original value=\"" + ovalue + "\" operator=" + op + " mod value=\"" + value + "\"");
                    return null;
                }
            }
            return value;
        }

        #endregion

        #region Command Parsing

        private enum Command
        {
            Insert,
            Delete,
            Edit,
            Replace,
            Copy
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
                default:
                    valueName = name;
                    return Command.Insert;
            }
            valueName = name.Substring(1);
            return ret;
        }

        #endregion

        #region Sanity checking & Utility functions

        public static bool IsBraquetBalanced(String str)
        {
            Stack<char> stack = new Stack<char>();

            char c;
            for (int i = 0; i < str.Length; i++)
            {
                c = str[i];
                if (c == '[')
                    stack.Push(c);
                else if (c == ']')
                    if (stack.Count == 0)
                        return false;
                    else if (stack.Peek() == '[')
                        stack.Pop();
                    else
                        return false;
            }
            return stack.Count == 0;
        }

        public static string RemoveWS(string withWhite)
        {   // Removes ALL whitespace of a string.
            return new string(withWhite.ToCharArray().Where(c => !Char.IsWhiteSpace(c)).ToArray());
        }

        public bool IsPathInList(string modPath, List<String> pathList)
        {
            return pathList.Any(modPath.StartsWith);
        }
        #endregion

        #region Condition checking
        // Split condiction while not getting lost in embeded brackets
        public static List<string> SplitCondition(string cond)
        {
            cond = RemoveWS(cond) + ",";
            List<string> conds = new List<string>();
            int start = 0;
            int level = 0;
            for (int end = 0; end < cond.Length; end++)
            {
                if (cond[end] == ',' && level == 0)
                {
                    conds.Add(cond.Substring(start, end - start));
                    start = end + 1;
                }
                else if (cond[end] == '[')
                    level++;
                else if (cond[end] == ']')
                    level--;
            }
            return conds;
        }

        public static bool CheckCondition(ConfigNode node, string conds)
        {
            conds = RemoveWS(conds);
            if (conds.Length == 0)
                return true;

            List<string> condsList = SplitCondition(conds);

            if (condsList.Count == 1)
            {
                conds = condsList[0];



                string remainCond = "";
                if (conds.Contains("HAS["))
                {
                    int start = conds.IndexOf("HAS[") + 4;
                    remainCond = conds.Substring(start, condsList[0].LastIndexOf(']') - start);
                    conds = conds.Substring(0, start - 5);
                }

                char[] sep = new char[] { '[', ']' };
                string[] splits = conds.Split(sep, 3);
                string type = splits[0].Substring(1);
                string name = splits.Length > 1 ? splits[1] : null;

                switch (conds[0])
                {
                    case '@':
                    case '!':
                        // @MODULE[ModuleAlternator] or !MODULE[ModuleAlternator]
                        bool not = (conds[0] == '!');
                        ConfigNode subNode = MMPatchLoader.FindConfigNodeIn(node, type, name);
                        if (subNode != null)
                            return not ^ CheckCondition(subNode, remainCond);
                        return not ^ false;
                    case '#':
                        // #module[Winglet]
                        if (node.HasValue(type) && node.GetValue(type).Equals(name))
                            return CheckCondition(node, remainCond);
                        return false;
                    case '~':
                        // ~breakingForce[]  breakingForce is not present
                        // or: ~breakingForce[100]  will be true if it's present but not 100, too.
                        if (!(node.HasValue(type)))
                            return CheckCondition(node, remainCond);
                        if (name != null && node.GetValue(type).Equals(name))
                            return CheckCondition(node, remainCond);

                        return false;
                    default:
                        return false;
                }
            }
            return condsList.TrueForAll(c => CheckCondition(node, c));
        }

        public static bool WildcardMatch(String s, String wildcard)
        {
            if (wildcard == null) return true;
            String pattern = "^" + Regex.Escape(wildcard).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
            Regex regex;
            regex = new Regex(pattern);

            return (regex.IsMatch(s));
        }
        #endregion

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
            {
                newNode.AddNode(subMod);
            }
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
        public static ConfigNode FindConfigNodeIn(ConfigNode src, string nodeType,
                                                   string nodeName = null, int index = 0)
        {
            ConfigNode[] nodes = src.GetNodes(nodeType);
            if (nodes.Length == 0) return null;
            if (nodeName == null)
            {
                if (index >= 0)
                    return nodes[Math.Min(index, nodes.Length - 1)];
                else
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
                if (WildcardMatch(newNode.values[i].name, valName))
                {
                    v = newNode.values[i];
                    if (--index < 0)
                        return v;
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

        #endregion

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
        #endregion


        #region Tests

        private void RunTestCases()
        {
            print("[ModuleManager] Running tests...");

            // Do MM testcases
            foreach (UrlDir.UrlConfig expect in GameDatabase.Instance.GetConfigs("MMTEST_EXPECT"))
            {
                // So for each of the expects, we expect all the configs before that node to match exactly.
                UrlDir.UrlFile parent = expect.parent;
                if (parent.configs.Count != expect.config.CountNodes + 1)
                {
                    print("[ModuleManager] Test " + parent.name + " failed as expecte number of nodes differs expected:" + expect.config.CountNodes + " found: " + parent.configs.Count);
                    for (int i = 0; i < parent.configs.Count; ++i)
                    {
                        print(parent.configs[i].config);
                    }
                    continue;
                }
                for (int i = 0; i < expect.config.CountNodes; ++i)
                {
                    ConfigNode gotNode = parent.configs[i].config;
                    ConfigNode expectNode = expect.config.nodes[i];
                    if (!CompareRecursive(expectNode, gotNode))
                    {
                        print("[ModuleManager] Test " + parent.name + "[" + i + "] failed as expected output and actual output differ.\nexpected:\n" + expectNode + "\nActually got:\n" + gotNode);
                    }
                }
                // Purge the tests
                parent.configs.Clear();
            }
            print("[ModuleManager] tests complete.");
        }

        #endregion
    }
}
