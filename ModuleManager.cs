using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using Debug = UnityEngine.Debug;
using ModuleManager.Cats;

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

        private bool nyan = false;
        private bool nCats = false;

        private PopupDialog menu;

        #endregion state

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
                || (DateTime.Now < new DateTime(2016, 11, 1))
                || Environment.GetCommandLineArgs().Contains("-nyan-nyan");

            nCats = catDay
                || Environment.GetCommandLineArgs().Contains("-ncats");

            loadedInScene = true;
        }

        private TextMeshProUGUI status;
        private TextMeshProUGUI errors;
        private TextMeshProUGUI warning;


        private void Start()
        {
            if (nCats)
                CatManager.LaunchCats();
            else if (nyan)
                CatManager.LaunchCat();

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
}
