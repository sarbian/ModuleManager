using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using Debug = UnityEngine.Debug;
using ModuleManager.Cats;
using ModuleManager.Extensions;
using ModuleManager.Logging;
using ModuleManager.UnityLogHandle;

namespace ModuleManager
{
    [KSPAddon(KSPAddon.Startup.Instantly, true)]
    public class ModuleManager : MonoBehaviour
    {
        #region state

        private bool inRnDCenter;

        public bool showUI = false;
        private float textPos = 0;

        //private Texture2D tex;
        //private Texture2D tex2;

        private bool nyan = false;
        private bool nCats = false;
        public static bool dumpPostPatch = false;
        public static bool DontCopyLogs { get; private set; } = false;

        private PopupDialog menu;

        private MMPatchRunner patchRunner;

        private InterceptLogHandler interceptLogHandler;

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

        public static void Log(String s)
        {
            print("[ModuleManager] " + s);
        }

        private readonly Stopwatch totalTime = new Stopwatch();

        internal void Awake()
        {
            if (LoadingScreen.Instance == null)
            {
                Destroy(gameObject);
                return;
            }

            // Ensure that only one copy of the service is run per scene change.
            if (loadedInScene || !ElectionAndCheck())
            {
                Assembly currentAssembly = Assembly.GetExecutingAssembly();
                Log("Multiple copies of current version. Using the first copy. Version: " +
                    currentAssembly.GetName().Version);
                Destroy(gameObject);
                return;
            }

            totalTime.Start();
            
            interceptLogHandler = new InterceptLogHandler();

            // Allow loading the background in the loading screen
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;

            // More cool loading screen. Less 4 stoke logo.
            for (int i = 0; i < LoadingScreen.Instance.Screens.Count; i++)
            {
                var state = LoadingScreen.Instance.Screens[i];
                state.fadeInTime = i < 3 ? 0.1f : 1;
                state.displayTime = i < 3 ? 1 : 3;
                state.fadeOutTime = i < 3 ? 0.1f : 1;
            }

            TextMeshProUGUI[] texts = LoadingScreen.Instance.gameObject.GetComponentsInChildren<TextMeshProUGUI>();
            foreach (var text in texts)
            {
                textPos = Mathf.Min(textPos, text.rectTransform.localPosition.y);
            }
            DontDestroyOnLoad(gameObject);

            // Subscribe to the RnD center spawn/deSpawn events
            GameEvents.onGUIRnDComplexSpawn.Add(OnRnDCenterSpawn);
            GameEvents.onGUIRnDComplexDespawn.Add(OnRnDCenterDeSpawn);


            LoadingScreen screen = FindObjectOfType<LoadingScreen>();
            if (screen == null)
            {
                Log("Can't find LoadingScreen type. Aborting ModuleManager execution");
                return;
            }
            List<LoadingSystem> list = LoadingScreen.Instance.loaders;

            if (list != null)
            {
                // So you can insert a LoadingSystem object in this list at any point.
                // GameDatabase is first in the list, and PartLoader is second
                // We could insert ModuleManager after GameDatabase to get it to run there
                // and SaveGameFixer after PartLoader.

                int gameDatabaseIndex = list.FindIndex(s => s is GameDatabase);

                GameObject aGameObject = new GameObject("ModuleManager");
                DontDestroyOnLoad(aGameObject);

                Log(string.Format("Adding post patch to the loading screen {0}", list.Count));
                list.Insert(gameDatabaseIndex + 1, aGameObject.AddComponent<PostPatchLoader>());

                patchRunner = new MMPatchRunner(new PrefixLogger("ModuleManager", new UnityLogger(Debug.unityLogger)));
                StartCoroutine(patchRunner.Run());

                // Workaround for 1.6.0 Editor bug after a PartDatabase rebuild.
                if (Versioning.version_major == 1 && Versioning.version_minor == 6 && Versioning.Revision == 0)
                {
                    Fix16 fix16 = aGameObject.AddComponent<Fix16>();
                    list.Add(fix16);
                }
            }

            bool foolsDay = (DateTime.Now.Month == 4 && DateTime.Now.Day == 1);
            bool catDay = (DateTime.Now.Month == 2 && DateTime.Now.Day == 22);
            nyan = foolsDay
                || Environment.GetCommandLineArgs().Contains("-nyan-nyan");

            nCats = catDay
                || Environment.GetCommandLineArgs().Contains("-ncats");

            dumpPostPatch = Environment.GetCommandLineArgs().Contains("-mm-dump");

            DontCopyLogs = Environment.GetCommandLineArgs().Contains("-mm-dont-copy-logs");

            loadedInScene = true;
        }

        private TextMeshProUGUI status;
        private TextMeshProUGUI errors;
        private TextMeshProUGUI warning;

        [SuppressMessage("Code Quality", "IDE0051", Justification = "Called by Unity")]
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

            if (Versioning.version_major == 1 && Versioning.version_minor >= 8)
            {
                foreach (AssemblyLoader.LoadedAssembly assembly in AssemblyLoader.loadedAssemblies)
                {
                    AssemblyName assemblyName = assembly.assembly.GetName();
                    if (assemblyName.Name == "Firespitter" && assemblyName.Version <= Version.Parse("7.3.7175.38653"))
                    {
                        warning.text = "You are using a version of Firespitter that does not run properly on KSP 1.8+\nThis version may prevent the game from loading properly and may create problems for other mods";
                    }
                }
            }
        }

        private TextMeshProUGUI CreateTextObject(Canvas canvas, string name)
        {
            GameObject statusGameObject = new GameObject(name);
            TextMeshProUGUI text = statusGameObject.AddComponent<TextMeshProUGUI>();
            text.text = "STATUS";
            text.fontSize = 18;
            text.autoSizeTextContainer = true;
            text.font = Resources.Load("Fonts/Calibri SDF", typeof(TMP_FontAsset)) as TMP_FontAsset;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.isOverlay = true;
            text.rectTransform.anchorMin = new Vector2(0.5f, 0);
            text.rectTransform.anchorMax = new Vector2(0.5f, 0);
            text.rectTransform.anchoredPosition = Vector2.zero;
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
                Log("Total loading Time = " + ((float)totalTime.ElapsedMilliseconds / 1000).ToString("F3") + "s");

                Application.runInBackground = GameSettings.SIMULATE_IN_BACKGROUND;
                QualitySettings.vSyncCount = GameSettings.SYNC_VBL;
                Application.targetFrameRate = GameSettings.FRAMERATE_LIMIT;
            }

            float offsetY = textPos;
            float h;

            if (patchRunner != null)
            {
                if (warning)
                {
                    warning.text = InterceptLogHandler.Warnings;
                    h = warning.text.Length > 0 ? warning.textBounds.size.y : 0;
                    offsetY += h;
                    warning.rectTransform.localPosition = new Vector3(0, offsetY);
                }

                if (status)
                {
                    status.text = patchRunner.Status;
                    h = status.text.Length > 0 ? status.textBounds.size.y : 0;
                    offsetY += h;
                    status.transform.localPosition = new Vector3(0, offsetY);
                }

                if (errors)
                {
                    errors.text = patchRunner.Errors;
                    h = errors.text.Length > 0 ? errors.textBounds.size.y : 0;
                    offsetY += h;
                    errors.transform.localPosition = new Vector3(0, offsetY);
                }
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
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;

            patchRunner = new MMPatchRunner(new PrefixLogger("ModuleManager", new UnityLogger(Debug.unityLogger)));

            float totalLoadWeight = GameDatabase.Instance.LoadWeight() + PartLoader.Instance.LoadWeight();
            bool startedReload = false;

            UISkinDef skinDef = HighLogic.UISkin;
            UIStyle centeredTextStyle = new UIStyle(skinDef.label)
            {
                alignment = TextAnchor.UpperCenter
            };

            PopupDialog reloadingDialog = PopupDialog.SpawnPopupDialog(new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new MultiOptionDialog(
                    "ModuleManagerReloading",
                    "",
                    "ModuleManager - Reloading Database",
                    skinDef,
                    new Rect(0.5f, 0.5f, 600f, 60f),
                    new DialogGUIFlexibleSpace(),
                    new DialogGUIVerticalLayout(
                        new DialogGUIFlexibleSpace(),
                        new DialogGUILabel(delegate ()
                        {
                            float progressFraction;
                            if (!startedReload)
                            {
                                progressFraction = 0f;
                            }
                            else if (!GameDatabase.Instance.IsReady() || !PostPatchLoader.Instance.IsReady())
                            {
                                progressFraction = GameDatabase.Instance.ProgressFraction() * GameDatabase.Instance.LoadWeight();
                                progressFraction /= totalLoadWeight;
                            }
                            else if (!PartLoader.Instance.IsReady())
                            {
                                progressFraction = GameDatabase.Instance.LoadWeight() + (PartLoader.Instance.ProgressFraction() * GameDatabase.Instance.LoadWeight());
                                progressFraction /= totalLoadWeight;
                            }
                            else
                            {
                                progressFraction = 1f;
                            }

                            return $"Overall progress: {progressFraction:P0}";
                        }, centeredTextStyle, expandW: true),
                        new DialogGUILabel(delegate ()
                        {
                            if (!startedReload)
                                return "Starting";
                            else if (!GameDatabase.Instance.IsReady())
                                return GameDatabase.Instance.ProgressTitle();
                            else if (!PostPatchLoader.Instance.IsReady())
                                return PostPatchLoader.Instance.ProgressTitle();
                            else if (!PartLoader.Instance.IsReady())
                                return PartLoader.Instance.ProgressTitle();
                            else
                                return "";
                        }),
                        new DialogGUISpace(5f),
                        new DialogGUILabel(() => patchRunner.Status)
                    )
                ),
                false,
                skinDef);

            yield return null;

            GameDatabase.Instance.Recompile = true;
            GameDatabase.Instance.StartLoad();

            startedReload = true;

            yield return null;
            StartCoroutine(patchRunner.Run());

            // wait for it to finish
            while (!GameDatabase.Instance.IsReady())
                yield return null;

            PostPatchLoader.Instance.StartLoad();

            while (!PostPatchLoader.Instance.IsReady())
                yield return null;

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

            reloadingDialog.Dismiss();
        }

        public static void OutputAllConfigs()
        {
            string path = Path.GetFullPath(Path.Combine(KSPUtil.ApplicationRootPath, "_MMCfgOutput"));
            try
            {
                Directory.CreateDirectory(path);
                foreach (string file in Directory.GetFiles(path))
                {
                    File.Delete(file);
                }
                foreach (string dir in Directory.GetDirectories(path))
                {
                    Directory.Delete(dir, true);
                }
            }
            catch (IOException ioException)
            {
                Log("Exception while cleaning the export dir\n" + ioException);
            }
            catch (UnauthorizedAccessException unauthorizedAccessException)
            {
                Log("Exception while cleaning the export dir\n" + unauthorizedAccessException);
            }

            static void WriteDirectoryRecursive(UrlDir currentDir, string dirPath)
            {
                if (currentDir.files.Count > 0) Directory.CreateDirectory(dirPath);

                foreach (UrlDir.UrlFile urlFile in currentDir.files)
                {
                    if (urlFile.fileType != UrlDir.FileType.Config) continue;

                    Log("Exporting " + urlFile.GetUrlWithExtension());
                    string filePath = Path.Combine(dirPath, urlFile.GetNameWithExtension());

                    bool first = true;

                    using FileStream stream = new FileStream(filePath, FileMode.Create);
                    using StreamWriter writer = new StreamWriter(stream);
                    foreach (UrlDir.UrlConfig urlConfig in urlFile.configs)
                    {
                        try
                        {
                            if (first) first = false;
                            else writer.Write("\n");

                            ConfigNode copy = urlConfig.config.DeepCopy();
                            copy.EscapeValuesRecursive();
                            writer.Write(copy.ToString());
                        }
                        catch (Exception e)
                        {
                            Log("Exception while trying to write the file " + filePath + "\n" + e);
                        }
                    }
                }

                foreach (UrlDir urlDir in currentDir.children)
                {
                    WriteDirectoryRecursive(urlDir, Path.Combine(dirPath, urlDir.name));
                }
            }

            try
            {
                WriteDirectoryRecursive(GameDatabase.Instance.root, path);
            }
            catch (DirectoryNotFoundException directoryNotFoundException)
            {
                Log("Exception while exporting the cfg\n" + directoryNotFoundException);
            }
            catch (IOException ioException)
            {
                Log("Exception while exporting the cfg\n" + ioException);
            }
            catch (UnauthorizedAccessException unauthorizedAccessException)
            {
                Log("Exception while exporting the cfg\n" + unauthorizedAccessException);
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
                Log("Old version of Module Manager present. Stopping");
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
                Log("version " + currentAssembly.GetName().Version + " at " + currentAssembly.Location +
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
                Log("version " + currentAssembly.GetName().Version + " at " + currentAssembly.Location +
                    " won the election against\n" + candidates);
            }

            #endregion Type election

            return true;
        }
    }
}
