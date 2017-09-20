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
using UnityEngine;
using Debug = UnityEngine.Debug;

using ModuleManager.Logging;
using ModuleManager.Extensions;
using NodeStack = ModuleManager.Collections.ImmutableStack<ConfigNode>;

namespace ModuleManager
{
    public delegate void ModuleManagerPostPatchCallback();

    [SuppressMessage("ReSharper", "StringLastIndexOfIsCultureSpecific.1")]
    [SuppressMessage("ReSharper", "StringIndexOfIsCultureSpecific.1")]
    public class MMPatchLoader : LoadingSystem
    {

        private List<string> mods;

        public string status = "";

        public string errors = "";

        public static bool keepPartDB = false;

        private string activity = "Module Manager";

        private static readonly Dictionary<string, Regex> regexCache = new Dictionary<string, Regex>();

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

        private IBasicLogger logger;

        public IPatchProgress progress;

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

            logger = new ModLogger("ModuleManager", Debug.logger);
            progress = new PatchProgress(logger);
        }

        private bool ready;

        public override bool IsReady()
        {
            //return false;
            if (ready)
            {
                patchSw.Stop();
                logger.Info("Ran in " + ((float)patchSw.ElapsedMilliseconds / 1000).ToString("F3") + "s");
            }
            return ready;
        }

        public override float ProgressFraction() => progress.ProgressFraction;

        public override string ProgressTitle()
        {
            return activity;
        }

        public override void StartLoad()
        {
            patchSw.Reset();
            patchSw.Start();

            progress = new PatchProgress(logger);

            ready = false;

            // DB check used to track the now fixed TextureReplacer corruption
            //checkValues();

            StartCoroutine(ProcessPatch());
        }

        public void Update()
        {
            if (progress.AppliedPatchCount > 0 && HighLogic.LoadedScene == GameScenes.LOADING)
                StatusUpdate();
        }

        public static void AddPostPatchCallback(ModuleManagerPostPatchCallback callback)
        {
            if (!postPatchCallbacks.Contains(callback))
                postPatchCallbacks.Add(callback);
        }
        private void PrePatchInit()
        {
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
                if (CommandParser.Parse(cfgmod.type, out string name) != Command.Insert)
                {
                    progress.PatchAdded();
                    if (name.Contains(":FOR["))
                    {
                        name = name.RemoveWS();

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
                            progress.Error(cfgmod, "Skipping :FOR init for line " + name +
                                ". The line most likely contains a space that should be removed");
                        }
                    }
                }
            }
            modlist += "Mods by directory (sub directories of GameData):\n";
            string gameData = Path.Combine(Path.GetFullPath(KSPUtil.ApplicationRootPath), "GameData");
            foreach (string subdir in Directory.GetDirectories(gameData))
            {
                string name = Path.GetFileName(subdir);
                string cleanName = name.RemoveWS();
                if (!mods.Contains(cleanName, StringComparer.OrdinalIgnoreCase))
                {
                    mods.Add(cleanName);
                    modlist += "  " + cleanName + "\n";
                }
            }
            logger.Info(modlist);

            mods.Sort();

            #endregion List of mods
        }

        private IEnumerator ProcessPatch()
        {
            status = "Checking Cache";
            logger.Info(status);
            yield return null;
            
            try
            {
                IsCacheUpToDate();
            }
            catch (Exception ex)
            {
                logger.Exception("Exception in IsCacheUpToDate", ex);
                useCache = false;
            }

#if DEBUG
            //useCache = false;
#endif

            status = "Pre patch init";
            logger.Info(status);
            yield return null;

            PrePatchInit();


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
                logger.Info(status);
                yield return null;
                CheckNeeds();

                #endregion Check Needs

                #region Sorting Patches

                status = "Sorting patches";
                logger.Info(status);

                yield return null;

                PatchList patchList = PatchExtractor.SortAndExtractPatches(GameDatabase.Instance.root, mods, progress);

                #endregion

                #region Applying patches

                status = "Applying patches";
                logger.Info(status);

                yield return null;

                // :First node
                yield return StartCoroutine(ApplyPatch(":FIRST", patchList.firstPatches));

                // any node without a :pass
                yield return StartCoroutine(ApplyPatch(":LEGACY (default)", patchList.legacyPatches));

                foreach (PatchList.ModPass pass in patchList.modPasses)
                {
                    string upperModName = pass.name.ToUpper();
                    yield return StartCoroutine(ApplyPatch(":BEFORE[" + upperModName + "]", pass.beforePatches));
                    yield return StartCoroutine(ApplyPatch(":FOR[" + upperModName + "]", pass.forPatches));
                    yield return StartCoroutine(ApplyPatch(":AFTER[" + upperModName + "]", pass.afterPatches));
                }

                // :Final node
                yield return StartCoroutine(ApplyPatch(":FINAL", patchList.finalPatches));

                PurgeUnused();

                #endregion Applying patches

                #region Saving Cache

                if (progress.ErrorCount > 0 || progress.ExceptionCount > 0)
                {
                    foreach (string file in progress.ErrorFiles.Keys)
                    {
                        errors += progress.ErrorFiles[file] + " error" + (progress.ErrorFiles[file] > 1 ? "s" : "") + " related to GameData/" + file
                                  + "\n";
                    }

                    logger.Warning("Errors in patch prevents the creation of the cache");
                    try
                    {
                        if (File.Exists(cachePath))
                            File.Delete(cachePath);
                        if (File.Exists(shaPath))
                            File.Delete(shaPath);
                    }
                    catch (Exception e)
                    {
                        logger.Exception("Exception while deleting stale cache ", e);
                    }
                }
                else
                {
                    status = "Saving Cache";
                    logger.Info(status);
                    yield return null;
                    CreateCache();
                }

                #endregion Saving Cache

                SaveModdedTechTree();
                SaveModdedPhysics();
            }
            else
            {
                status = "Loading from Cache";
                logger.Info(status);
                yield return null;
                LoadCache();
            }

            StatusUpdate();

            logger.Info(status + "\n" + errors);

#if DEBUG
            RunTestCases();
#endif

            // TODO : Remove if we ever get a way to load sooner
            logger.Info("Reloading resources definitions");
            PartResourceLibrary.Instance.LoadDefinitions();

            logger.Info("Reloading Trait configs");
            GameDatabase.Instance.ExperienceConfigs.LoadTraitConfigs();

            logger.Info("Reloading Part Upgrades");
            PartUpgradeManager.Handler.FillUpgrades();

            foreach (ModuleManagerPostPatchCallback callback in postPatchCallbacks)
            {
                try
                {
                    callback();
                }
                catch (Exception e)
                {
                    logger.Exception("Exception while running a post patch callback", e);
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
                                logger.Info("Calling " + ass.GetName().Name + "." + type.Name + "." + method.Name + "()");
                                method.Invoke(null, null);
                            }
                            catch (Exception e)
                            {
                                logger.Exception("Exception while calling " + ass.GetName().Name + "." + type.Name + "." + method.Name + "()", e);
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    logger.Exception("Post run call threw an exception in loading " + ass.FullName, e);
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
                        logger.Info("Calling " + obj.GetType().Name + "." + method.Name + "()");
                        method.Invoke(obj, null);
                    }
                    catch (Exception e)
                    {
                        logger.Exception("Exception while calling " + obj.GetType().Name + "." + method.Name + "() :\n", e);
                    }
                }
            }

            yield return null;

            ready = true;
        }

        private void LoadPhysicsConfig()
        {
            logger.Info("Loading Physics.cfg");
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
                logger.Info("No PHYSICSGLOBALS node found. No custom Physics config will be saved");
                return;
            }

            if (configs.Count > 1)
            {
                logger.Info(configs.Count + " PHYSICSGLOBALS node found. A patch may be wrong. Using the first one");
            }

            configs[0].config.Save(physicsPath);
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
                logger.Exception("Exception hashing file " + filename, e);
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
                    logger.Warning("Duplicate fileSha key. This should not append. The key is " + files[i].url);
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

            logger.Info("SHA generated in " + ((float)sw.ElapsedMilliseconds / 1000).ToString("F3") + "s");
            logger.Info("      SHA = " + configSha);

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
                    logger.Info("Cache SHA = " + storedSHA);
                    logger.Info("useCache = " + useCache);
                }
            }
        }

        private bool CheckFilesChange(UrlDir.UrlFile[] files, ConfigNode shaConfigNode)
        {
            bool noChange = true;
            StringBuilder changes = new StringBuilder();
            
            for (int i = 0; i < files.Length; i++)
            {
                ConfigNode fileNode = GetFileNode(shaConfigNode, files[i].url);
                string fileSha = fileNode?.GetValue("SHA");

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
                ConfigNode fileNode = GetFileNode(shaConfigNode, files[i].url);

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
                logger.Info("Changes :\n" + changes.ToString());
            return noChange;
        }

        private ConfigNode GetFileNode(ConfigNode shaConfigNode, string filename)
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

            cache.AddValue("patchedNodeCount", progress.PatchedNodeCount.ToString());

            foreach (UrlDir.UrlConfig config in GameDatabase.Instance.root.AllConfigs)
            {
                ConfigNode node = cache.AddNode("UrlConfig");
                node.AddValue("name", config.name);
                node.AddValue("type", config.type);
                node.AddValue("parentUrl", config.parent.url);
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

            logger.Info("Saving cache");

            try
            {
                shaConfigNode.Save(shaPath);
            }
            catch (Exception e)
            {
                logger.Exception("Exception while saving the sha", e);
            }
            try
            {
                cache.Save(cachePath);
                return;
            }
            catch (NullReferenceException e)
            {
                logger.Exception("NullReferenceException while saving the cache", e);
            }
            catch (Exception e)
            {
                logger.Exception("Exception while saving the cache", e);
            }

            try
            {
                logger.Error("An error occured while creating the cache. Deleting the cache files to avoid keeping a bad cache");
                if (File.Exists(cachePath))
                    File.Delete(cachePath);
                if (File.Exists(shaPath))
                    File.Delete(shaPath);
            }
            catch (Exception e)
            {
                logger.Exception("Exception while deleting the cache", e);
            }
        }

        private void SaveModdedTechTree()
        {
            UrlDir.UrlConfig[] configs = GameDatabase.Instance.GetConfigs("TechTree");

            if (configs.Length == 0)
            {
                logger.Info("No TechTree node found. No custom TechTree will be saved");
                return;
            }

            if (configs.Length > 1)
            {
                logger.Info(configs.Length + " TechTree node found. A patch may be wrong. Using the first one");
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
            
            if (cache.HasValue("patchedNodeCount") && int.TryParse(cache.GetValue("patchedNodeCount"), out int patchedNodeCount))
                progress.PatchedNodeCount = patchedNodeCount;

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

                UrlDir.UrlFile parent = GameDatabase.Instance.root.AllConfigFiles.FirstOrDefault(f => f.url == parentUrl);
                if (parent != null)
                {
                    parent.AddConfig(node.nodes[0]);
                }
                else
                {
                    logger.Warning("Parent null for " + parentUrl);
                }
            }
            logger.Info("Cache Loaded");
        }

        private void StatusUpdate()
        {
            status = "ModuleManager: " + progress.PatchedNodeCount + " patch" + (progress.PatchedNodeCount != 1 ? "es" : "") + (useCache ? " loaded from cache" : " applied");

            if (progress.ErrorCount > 0)
                status += ", found <color=orange>" + progress.ErrorCount + " error" + (progress.ErrorCount != 1 ? "s" : "") + "</color>";

            if (progress.ExceptionCount > 0)
                status += ", encountered <color=red>" + progress.ExceptionCount + " exception" + (progress.ExceptionCount != 1 ? "s" : "") + "</color>";
        }

        #region Needs checking

        private void CheckNeeds()
        {
            UrlDir.UrlConfig[] allConfigs = GameDatabase.Instance.root.AllConfigs.ToArray();

            // Check the NEEDS parts first.
            foreach (UrlDir.UrlConfig mod in allConfigs)
            {
                UrlDir.UrlConfig currentMod = mod; 
                try
                {
                    if (mod.config.name == null)
                    {
                        progress.Error(currentMod, "Error - Node in file " + currentMod.parent.url + " subnode: " + currentMod.type +
                                " has config.name == null");
                    }

                    if (currentMod.type.Contains(":NEEDS["))
                    {
                        mod.parent.configs.Remove(currentMod);
                        string type = currentMod.type;

                        if (!CheckNeeds(ref type))
                        {
                            progress.NeedsUnsatisfiedRoot(currentMod);
                            continue;
                        }

                        ConfigNode copy = new ConfigNode(type);
                        copy.ShallowCopyFrom(currentMod.config);
                        currentMod = new UrlDir.UrlConfig(currentMod.parent, copy);
                        mod.parent.configs.Add(currentMod);
                    }

                    // Recursively check the contents
                    CheckNeeds(new NodeStack(mod.config), new PatchContext(mod, GameDatabase.Instance.root, logger, progress));
                }
                catch (Exception ex)
                {
                    progress.Exception(currentMod, "Exception while checking needs : " + currentMod.SafeUrl() + " with a type of " + currentMod.type, ex);
                    logger.Error("Node is : " + PrettyConfig(currentMod));
                }
            }
        }

        private void CheckNeeds(NodeStack stack, PatchContext context)
        {
            bool needsCopy = false;
            ConfigNode original = stack.value;
            ConfigNode copy = new ConfigNode(original.name);
            for (int i = 0; i < original.values.Count; ++i)
            {
                ConfigNode.Value val = original.values[i];
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
                        context.progress.NeedsUnsatisfiedValue(context.patchUrl, stack, val.name);
                    }
                }
                catch (ArgumentOutOfRangeException e)
                {
                    progress.Exception("ArgumentOutOfRangeException in CheckNeeds for value \"" + val.name + "\"", e);
                    throw;
                }
                catch (Exception e)
                {
                    progress.Exception("General Exception in CheckNeeds for value \"" + val.name + "\"", e);
                    throw;
                }
            }

            for (int i = 0; i < original.nodes.Count; ++i)
            {
                ConfigNode node = original.nodes[i];
                string nodeName = node.name;

                if (nodeName == null)
                {
                    progress.Error(context.patchUrl, "Error - Node in file " + context.patchUrl.SafeUrl() + " subnode: " + stack.GetPath() +
                            " has config.name == null");
                }

                try
                {
                    if (CheckNeeds(ref nodeName))
                    {
                        node.name = nodeName;
                        CheckNeeds(stack.Push(node), context);
                        copy.AddNode(node);
                    }
                    else
                    {
                        needsCopy = true;
                        progress.NeedsUnsatisfiedNode(context.patchUrl, stack.Push(node));
                    }
                }
                catch (ArgumentOutOfRangeException e)
                {
                    progress.Exception("ArgumentOutOfRangeException in CheckNeeds for node \"" + node.name + "\"", e);
                    throw;
                }
                catch (Exception e)
                {
                    progress.Exception("General Exception " + e.GetType().Name + " for node \"" + node.name + "\"", e);
                    throw;
                }
            }

            if (needsCopy)
                original.ShallowCopyFrom(copy);
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

        private void PurgeUnused()
        {
            foreach (UrlDir.UrlConfig mod in GameDatabase.Instance.root.AllConfigs.ToArray())
            {
                string name = mod.type.RemoveWS();

                if (CommandParser.Parse(name, out name) != Command.Insert)
                    mod.parent.configs.Remove(mod);
            }
        }

        #endregion Needs checking

        #region Applying Patches

        // Apply patch to all relevent nodes
        public IEnumerator ApplyPatch(string Stage, IEnumerable<UrlDir.UrlConfig> patches)
        {
            StatusUpdate();
            logger.Info(Stage +  " pass");
            yield return null;

            activity = "ModuleManager " + Stage;

            UrlDir.UrlConfig[] allConfigs = GameDatabase.Instance.root.AllConfigs.ToArray();
            
            float nextYield = Time.realtimeSinceStartup + yieldInterval;

            foreach (UrlDir.UrlConfig mod in patches)
            {
                try
                {
                    string name = mod.type.RemoveWS();
                    Command cmd = CommandParser.Parse(name, out string tmp);

                    if (cmd == Command.Insert)
                    {
                        logger.Warning("Warning - Encountered insert node that should not exist at this stage: " + mod.SafeUrl());
                        continue;
                    }

                    string upperName = name.ToUpper();
                    PatchContext context = new PatchContext(mod, GameDatabase.Instance.root, logger, progress);
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
                                    && CheckConstraints(url.config, condition))
                                {
                                    switch (cmd)
                                    {
                                        case Command.Edit:
                                            progress.ApplyingUpdate(url, mod);
                                            url.config = ModifyNode(new NodeStack(url.config), mod.config, context);
                                            break;

                                        case Command.Copy:
                                            ConfigNode clone = ModifyNode(new NodeStack(url.config), mod.config, context);
                                            if (url.config.name != mod.name)
                                            {
                                                progress.ApplyingCopy(url, mod);
                                                url.parent.configs.Add(new UrlDir.UrlConfig(url.parent, clone));
                                            }
                                            else
                                            {
                                                progress.Error(mod, "Error - Error while processing " + mod.config.name +
                                                    " the copy needs to have a different name than the parent (use @name = xxx)");
                                            }
                                            break;

                                        case Command.Delete:
                                            progress.ApplyingDelete(url, mod);
                                            url.parent.configs.Remove(url);
                                            break;

                                        default:
                                            logger.Warning("Invalid command encountered on a root node: " + mod.SafeUrl());
                                            break;
                                    }
                                    // When this special node is found then try to apply the patch once more on the same NODE
                                    if (mod.config.HasNode("MM_PATCH_LOOP"))
                                    {
                                        logger.Info("Looping on " + mod.SafeUrl() + " to " + url.SafeUrl());
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
                catch (Exception e)
                {
                    progress.Exception(mod, "Exception while processing node : " + mod.SafeUrl(), e);
                    logger.Error("Processed node was\n" + PrettyConfig(mod));
                    mod.parent.configs.Remove(mod);
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
        public static ConfigNode ModifyNode(NodeStack original, ConfigNode mod, PatchContext context)
        {
            ConfigNode newNode = original.value.DeepCopy();
            NodeStack nodeStack = original.ReplaceValue(newNode);

            #region Values

            #if LOGSPAM
            string vals = "[ModuleManager] modding values";
            #endif
            foreach (ConfigNode.Value modVal in mod.values)
            {
                #if LOGSPAM
                vals += "\n   " + modVal.name + "= " + modVal.value;
                #endif

                Command cmd = CommandParser.Parse(modVal.name, out string valName);

                if (cmd == Command.Special)
                {
                    Match assignMatch = parseAssign.Match(valName);
                    if (!assignMatch.Success)
                    {
                        context.progress.Error(context.patchUrl, "Error - Cannot parse value assigning command: " + valName);
                        continue;
                    }

                    valName = assignMatch.Groups[1].Value;

                    ConfigNode.Value val = RecurseVariableSearch(valName, nodeStack.Push(mod), context);

                    if (val == null)
                    {
                        context.progress.Error(context.patchUrl, "Error - Cannot find value assigning command: " + valName);
                        continue;
                    }
                    
                    if (assignMatch.Groups[2].Success)
                    {
                        if (double.TryParse(modVal.value, out double s) && double.TryParse(val.value, out double os))
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
                    context.progress.Error(context.patchUrl, "Error - Cannot parse value modifying command: " + valName);
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
                        context.progress.Error(context.patchUrl, "Error - Unable to parse number as number. Very odd.");
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
                        context.progress.Error(context.patchUrl, "Error - Unable to parse number as number. Very odd.");
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
                            context.progress.Error(context.patchUrl, "Error - Cannot use operators with insert value: " + mod.name);
                        }
                        else
                        {
                            // Insert at the end by default
                            varValue = ProcessVariableSearch(modVal.value, nodeStack, context);
                            if (varValue != null)
                                InsertValue(newNode, match.Groups[2].Success ? index : int.MaxValue, valName, varValue);
                            else
                                context.progress.Error(context.patchUrl, "Error - Cannot parse variable search when inserting new key " + valName + " = " +
                                    modVal.value);
                        }
                        break;

                    case Command.Replace:
                        if (match.Groups[2].Success || match.Groups[5].Success || valName.Contains('*')
                            || valName.Contains('?'))
                        {
                            if (match.Groups[2].Success)
                                context.progress.Error(context.patchUrl, "Error - Cannot use index with replace (%) value: " + mod.name);
                            if (match.Groups[5].Success)
                                context.progress.Error(context.patchUrl, "Error - Cannot use operators with replace (%) value: " + mod.name);
                            if (valName.Contains('*') || valName.Contains('?'))
                                context.progress.Error(context.patchUrl, "Error - Cannot use wildcards (* or ?) with replace (%) value: " + mod.name);
                        }
                        else
                        {
                            varValue = ProcessVariableSearch(modVal.value, nodeStack, context);
                            if (varValue != null)
                            {
                                newNode.RemoveValues(valName);
                                newNode.AddValue(valName, varValue);
                            }
                            else
                            {
                                context.progress.Error(context.patchUrl, "Error - Cannot parse variable search when replacing (%) key " + valName + " = " +
                                    modVal.value);
                            }
                        }
                        break;

                    case Command.Edit:
                    case Command.Copy:

                        // Format is @key = value or @key *= value or @key += value or @key -= value
                        // or @key,index = value or @key,index *= value or @key,index += value or @key,index -= value

                        while (index < valCount)
                        {
                            varValue = ProcessVariableSearch(modVal.value, nodeStack, context);

                            if (varValue != null)
                            {
                                string value = FindAndReplaceValue(
                                    mod,
                                    ref valName,
                                    varValue, newNode,
                                    op,
                                    index,
                                    out ConfigNode.Value origVal,
                                    context,
                                    match.Groups[3].Success,
                                    position,
                                    isPosStar,
                                    seperator
                                );

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
                                context.progress.Error(context.patchUrl, "Error - Cannot parse variable search when editing key " + valName + " = " + modVal.value);
                            }

                            if (isStar) index++;
                            else break;
                        }
                        break;

                    case Command.Delete:
                        if (match.Groups[5].Success)
                        {
                            context.progress.Error(context.patchUrl, "Error - Cannot use operators with delete (- or !) value: " + mod.name);
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
                        if (nodeStack.IsRoot)
                        {
                            context.progress.Error(context.patchUrl, "Error - Renaming nodes does not work on top nodes");
                            break;
                        }
                        newNode.name = modVal.value;
                        break;

                    case Command.Create:
                        if (match.Groups[2].Success || match.Groups[5].Success || valName.Contains('*')
                            || valName.Contains('?'))
                        {
                            if (match.Groups[2].Success)
                                context.progress.Error(context.patchUrl, "Error - Cannot use index with create (&) value: " + mod.name);
                            if (match.Groups[5].Success)
                                context.progress.Error(context.patchUrl, "Error - Cannot use operators with create (&) value: " + mod.name);
                            if (valName.Contains('*') || valName.Contains('?'))
                                context.progress.Error(context.patchUrl, "Error - Cannot use wildcards (* or ?) with create (&) value: " + mod.name);
                        }
                        else
                        {
                            varValue = ProcessVariableSearch(modVal.value, nodeStack, context);
                            if (varValue != null)
                            {
                                if (!newNode.HasValue(valName))
                                    newNode.AddValue(valName, varValue);
                            }
                            else
                            {
                                context.progress.Error(context.patchUrl, "Error - Cannot parse variable search when replacing (&) key " + valName + " = " +
                                    modVal.value);
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
                subMod.name = subMod.name.RemoveWS();

                if (!subMod.name.IsBracketBalanced())
                {
                    context.progress.Error(context.patchUrl,
                        "Error - Skipping a patch subnode with unbalanced square brackets or a space (replace them with a '?') in "
                        + mod.name + " : \n" + subMod.name + "\n");
                    continue;
                }

                string subName = subMod.name;
                Command command = CommandParser.Parse(subName, out string tmp);

                if (command == Command.Insert)
                {
                    ConfigNode newSubMod = new ConfigNode(subMod.name);
                    newSubMod = ModifyNode(nodeStack.Push(newSubMod), subMod, context);
                    subName = newSubMod.name;
                    if (subName.Contains(",") && int.TryParse(subName.Split(',')[1], out int index))
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

                    ConfigNode toPaste = RecurseNodeSearch(subName.Substring(1), nodeStack, context);

                    if (toPaste == null)
                    {
                        context.progress.Error(context.patchUrl, "Error - Can not find the node to paste in " + mod.name + " : " + subMod.name + "\n");
                        continue;
                    }

                    ConfigNode newSubMod = new ConfigNode(toPaste.name);
                    newSubMod = ModifyNode(nodeStack.Push(newSubMod), toPaste, context);
                    if (subName.LastIndexOf(",") > 0 && int.TryParse(subName.Substring(subName.LastIndexOf(",") + 1), out int index))
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
                            ConfigNode newSubNode = ModifyNode(nodeStack.Push(subNodes[0]), subMod, context);
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

                            ConfigNode newSubNode = ModifyNode(nodeStack.Push(copy), subMod, context);
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

                            ConfigNode newSubNode = ModifyNode(nodeStack.Push(copy), subMod, context);
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
                                    newSubNode = ModifyNode(nodeStack.Push(subNode), subMod, context);
                                    subNode.ClearData();
                                    newSubNode.CopyTo(subNode, newSubNode.name);
                                    break;

                                case Command.Delete:

                                    // Delete the node
                                    newNode.nodes.Remove(subNode);
                                    break;

                                case Command.Copy:

                                    // Copy the node
                                    newSubNode = ModifyNode(nodeStack.Push(subNode), subMod, context);
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

            return newNode;
        }


        // Search for a ConfigNode by a path alike string
        private static ConfigNode RecurseNodeSearch(string path, NodeStack nodeStack, PatchContext context)
        {
            //log("Path : \"" + path + "\"");

            if (path[0] == '/')
            {
                return RecurseNodeSearch(path.Substring(1), nodeStack.Root, context);
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
                if (nodeStack.IsRoot)
                    return null;

                return RecurseNodeSearch(path.Substring(3), nodeStack.Pop(), context);
            }

            //log("nextSep : \"" + nextSep + " \" root : \"" + root + " \" nodeType : \"" + nodeType + "\" nodeName : \"" + nodeName + "\"");

            // @XXXXX
            if (root)
            {
                IEnumerable<UrlDir.UrlConfig> urlConfigs = context.databaseRoot.GetConfigs(nodeType);
                if (!urlConfigs.Any())
                {
                    context.logger.Warning("Can't find nodeType:" + nodeType);
                    return null;
                }

                if (nodeName == null)
                {
                    nodeStack = new NodeStack(urlConfigs.First().config);
                }
                else
                {
                    foreach (UrlDir.UrlConfig url in urlConfigs)
                    {
                        if (url.config.HasValue("name") && WildcardMatch(url.config.GetValue("name"), nodeName))
                        {
                            nodeStack = new NodeStack(url.config);
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
                        ConfigNode n = FindConfigNodeIn(nodeStack.value, nodeType, nodeName, index++);
                        if (n == last || n == null)
                        {
                            nodeStack = null;
                            break;
                        }
                        if (CheckConstraints(n, constraint))
                        {
                            nodeStack = nodeStack.Push(n);
                            break;
                        }
                        last = n;
                    }
                }
                else
                {
                    // just get one node
                    nodeStack = nodeStack.Push(FindConfigNodeIn(nodeStack.value, nodeType, nodeName, index));
                }
            }

            // XXXXXX/
            if (nextSep > 0 && nodeStack != null)
            {
                path = path.Substring(nextSep + 1);
                //log("NewPath : \"" + path + "\"");
                return RecurseNodeSearch(path, nodeStack, context);
            }

            return nodeStack.value;
        }

        // KeyName is group 1, index is group 2, value index is group 3, value separator is group 4
        private static readonly Regex parseVarKey = new Regex(@"([\w\&\-\.]+)(?:,((?:[0-9]+)+))?(?:\[((?:[0-9]+)+)(?:,(.))?\])?");

        // Search for a value by a path alike string
        private static ConfigNode.Value RecurseVariableSearch(string path, NodeStack nodeStack, PatchContext context)
        {
            //log("path:" + path);
            if (path[0] == '/')
                return RecurseVariableSearch(path.Substring(1), nodeStack.Root, context);
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
                UrlDir.UrlConfig target = null;

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

                IEnumerable<UrlDir.UrlConfig> urlConfigs = context.databaseRoot.GetConfigs(nodeType);
                if (!urlConfigs.Any())
                {
                    context.logger.Warning("Can't find nodeType:" + nodeType);
                    return null;
                }

                if (nodeName == string.Empty)
                {
                    target = urlConfigs.First();
                }
                else
                {
                    foreach (UrlDir.UrlConfig url in urlConfigs)
                    {
                        if (url.config.HasValue("name") && WildcardMatch(url.config.GetValue("name"), nodeName))
                        {
                            target = url;
                            break;
                        }
                    }
                }
                return target != null ? RecurseVariableSearch(path.Substring(nextSep + 1), new NodeStack(target.config), context) : null;
            }
            if (path.StartsWith("../"))
            {
                if (nodeStack.IsRoot)
                    return null;

                return RecurseVariableSearch(path.Substring(3), nodeStack.Pop(), context);
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
                        ConfigNode n = FindConfigNodeIn(nodeStack.value, nodeType, nodeName, index++);
                        if (n == last || n == null)
                            break;
                        if (CheckConstraints(n, constraint))
                            return RecurseVariableSearch(path.Substring(nextSep + 1), nodeStack.Push(n), context);
                        last = n;
                    }
                    return null;
                }
                else
                {
                    // just get one node
                    ConfigNode n = FindConfigNodeIn(nodeStack.value, nodeType, nodeName, index);
                    if (n != null)
                        return RecurseVariableSearch(path.Substring(nextSep + 1), nodeStack.Push(n), context);
                    return null;
                }
            }

            // Value search

            Match match = parseVarKey.Match(path);
            if (!match.Success)
            {
                context.logger.Warning("Cannot parse variable search command: " + path);
                return null;
            }

            string valName = match.Groups[1].Value;

            int idx = 0;
            if (match.Groups[2].Success)
                int.TryParse(match.Groups[2].Value, out idx);

            ConfigNode.Value cVal = FindValueIn(nodeStack.value, valName, idx);
            if (cVal == null)
            {
                context.logger.Warning("Cannot find key " + valName + " in " + nodeStack.value.name);
                return null;
            }
            
            if (match.Groups[3].Success)
            {
                ConfigNode.Value newVal = new ConfigNode.Value(cVal.name, cVal.value);
                int.TryParse(match.Groups[3].Value, out int splitIdx);

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

        private static string ProcessVariableSearch(string value, NodeStack nodeStack, PatchContext context)
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
                    ConfigNode.Value result = RecurseVariableSearch(split[i], nodeStack, context);
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

        private static string FindAndReplaceValue(
            ConfigNode mod,
            ref string valName,
            string value,
            ConfigNode newNode,
            char op,
            int index,
            out ConfigNode.Value origVal,
            PatchContext context,
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
                    context.progress.Error(context.patchUrl, "Invalid Vector Index!");
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
                            context.progress.Exception(context.patchUrl, "Error - Failed to do a regexp replacement: " + mod.name + " : original value=\"" + oValue +
                                "\" regexp=\"" + value +
                                "\" \nNote - to use regexp, the first char is used to subdivide the string (much like sed)", ex);
                            return null;
                        }
                    }
                    else if (double.TryParse(value, out double s) && double.TryParse(oValue, out double os))
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
                        context.progress.Error(context.patchUrl, "Error - Failed to do a maths replacement: " + mod.name + " : original value=\"" + oValue +
                            "\" operator=" + op + " mod value=\"" + value + "\"");
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

        #region Condition checking

        // Split condiction while not getting lost in embeded brackets
        public static List<string> SplitConstraints(string condition)
        {
            condition = condition.RemoveWS() + ",";
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
            constraints = constraints.RemoveWS();
            
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

                if (compare && Double.TryParse(values[i], out double val2)
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

            if (!regexCache.TryGetValue(pattern, out Regex regex))
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

        private string PrettyConfig(UrlDir.UrlConfig config)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("{0}[{1}]\n", config.type ?? "NULL", config.name ?? "NULL");
            try
            {
                if (config.config != null)
                {
                    config.config.PrettyPrint(ref sb, "  ");
                }
                else
                {
                    sb.Append("NULL\n");
                }
                sb.Append("\n");
            }
            catch (Exception e)
            {
                logger.Exception("PrettyConfig Exception", e);
            }
            return sb.ToString();
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

        #region Tests

        private void RunTestCases()
        {
            logger.Info("Running tests...");

            // Do MM testcases
            foreach (UrlDir.UrlConfig expect in GameDatabase.Instance.GetConfigs("MMTEST_EXPECT"))
            {
                // So for each of the expects, we expect all the configs before that node to match exactly.
                UrlDir.UrlFile parent = expect.parent;
                if (parent.configs.Count != expect.config.CountNodes + 1)
                {
                    logger.Error("Test " + parent.name + " failed as expected number of nodes differs expected:" +
                        expect.config.CountNodes + " found: " + parent.configs.Count);
                    for (int i = 0; i < parent.configs.Count; ++i)
                        logger.Info(parent.configs[i].config.ToString());
                    continue;
                }
                for (int i = 0; i < expect.config.CountNodes; ++i)
                {
                    ConfigNode gotNode = parent.configs[i].config;
                    ConfigNode expectNode = expect.config.nodes[i];
                    if (!CompareRecursive(expectNode, gotNode))
                    {
                        logger.Error("Test " + parent.name + "[" + i +
                            "] failed as expected output and actual output differ.\nexpected:\n" + expectNode +
                            "\nActually got:\n" + gotNode);
                    }
                }

                // Purge the tests
                parent.configs.Clear();
            }
            logger.Info("tests complete.");
        }

        #endregion Tests
    }
}
