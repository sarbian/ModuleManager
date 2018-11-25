using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using Debug = UnityEngine.Debug;

using ModuleManager.Logging;
using ModuleManager.Extensions;
using ModuleManager.Collections;
using ModuleManager.Tags;
using ModuleManager.Threading;
using ModuleManager.Patches;
using ModuleManager.Progress;
using NodeStack = ModuleManager.Collections.ImmutableStack<ConfigNode>;

namespace ModuleManager
{
    public delegate void ModuleManagerPostPatchCallback();

    [SuppressMessage("ReSharper", "StringLastIndexOfIsCultureSpecific.1")]
    [SuppressMessage("ReSharper", "StringIndexOfIsCultureSpecific.1")]
    public class MMPatchLoader : LoadingSystem
    {
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

        private static readonly List<ModuleManagerPostPatchCallback> postPatchCallbacks = new List<ModuleManagerPostPatchCallback>();

        private const float yieldInterval = 1f/30f; // Patch at ~30fps

        private IBasicLogger logger;

        private float progressFraction = 0;

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

            cachePath = Path.Combine(Path.Combine(KSPUtil.ApplicationRootPath, "GameData"), "ModuleManager.ConfigCache");
            techTreeFile = Path.Combine("GameData", "ModuleManager.TechTree");
            techTreePath = Path.Combine(KSPUtil.ApplicationRootPath, techTreeFile);
            physicsFile = Path.Combine("GameData", "ModuleManager.Physics");
            physicsPath = Path.Combine(KSPUtil.ApplicationRootPath, physicsFile);
            defaultPhysicsPath = Path.Combine(KSPUtil.ApplicationRootPath, "Physics.cfg");
            partDatabasePath = Path.Combine(KSPUtil.ApplicationRootPath, "PartDatabase.cfg");
            shaPath = Path.Combine(Path.Combine(KSPUtil.ApplicationRootPath, "GameData"), "ModuleManager.ConfigSHA");

            logger = new ModLogger("ModuleManager", new UnityLogger(Debug.unityLogger));
        }

        private bool ready;

        public override bool IsReady()
        {
            return ready;
        }

        public override float ProgressFraction() => progressFraction;

        public override string ProgressTitle()
        {
            return activity;
        }

        public override void StartLoad()
        {
            ready = false;

            // DB check used to track the now fixed TextureReplacer corruption
            //checkValues();

            StartCoroutine(ProcessPatch());
        }

        public static void AddPostPatchCallback(ModuleManagerPostPatchCallback callback)
        {
            if (!postPatchCallbacks.Contains(callback))
                postPatchCallbacks.Add(callback);
        }

        private IEnumerator ProcessPatch()
        {
            Stopwatch patchSw = new Stopwatch();
            patchSw.Start();

            status = "Checking Cache";
            logger.Info(status);
            yield return null;

            bool useCache = false;
            try
            {
                useCache = IsCacheUpToDate();
            }
            catch (Exception ex)
            {
                logger.Exception("Exception in IsCacheUpToDate", ex);
            }

#if DEBUG
            //useCache = false;
#endif
            yield return null;

            if (!useCache)
            {
                IPatchProgress progress = new PatchProgress(logger);
                status = "Pre patch init";
                logger.Info(status);
                IEnumerable<string> mods = ModListGenerator.GenerateModList(progress, logger);

                yield return null;

                // If we don't use the cache then it is best to clean the PartDatabase.cfg
                if (!keepPartDB && File.Exists(partDatabasePath))
                    File.Delete(partDatabasePath);

                LoadPhysicsConfig();

                #region Sorting Patches

                status = "Extracting patches";
                logger.Info(status);

                yield return null;

                UrlDir gameData = GameDatabase.Instance.root.children.First(dir => dir.type == UrlDir.DirectoryType.GameData && dir.name == "");
                INeedsChecker needsChecker = new NeedsChecker(mods, gameData, progress, logger);
                ITagListParser tagListParser = new TagListParser(progress);
                IProtoPatchBuilder protoPatchBuilder = new ProtoPatchBuilder(progress);
                IPatchCompiler patchCompiler = new PatchCompiler();
                PatchExtractor extractor = new PatchExtractor(progress, logger, needsChecker, tagListParser, protoPatchBuilder, patchCompiler);

                // Have to convert to an array because we will be removing patches
                UrlDir.UrlConfig[] allConfigs = GameDatabase.Instance.root.AllConfigs.ToArray();
                IEnumerable<IPatch> extractedPatches = allConfigs.Select(urlConfig => extractor.ExtractPatch(urlConfig));
                PatchList patchList = new PatchList(mods, extractedPatches.Where(patch => patch != null), progress);

                #endregion

                #region Applying patches

                status = "Applying patches";
                logger.Info(status);

                yield return null;

                MessageQueue<ILogMessage> logQueue = new MessageQueue<ILogMessage>();
                IBasicLogger patchLogger = new QueueLogger(logQueue);
                IPatchProgress threadPatchProgress = new PatchProgress(progress, patchLogger);
                PatchApplier applier = new PatchApplier(threadPatchProgress, patchLogger);

                logger.Info("Starting patch thread");

                ITaskStatus patchThread = BackgroundTask.Start(delegate
                {
                    applier.ApplyPatches(GameDatabase.Instance.root.AllConfigFiles.ToArray(), patchList);
                });

                float nextYield = Time.realtimeSinceStartup + yieldInterval;

                float updateTimeRemaining()
                {
                    float timeRemaining = nextYield - Time.realtimeSinceStartup;
                    if (timeRemaining < 0)
                    {
                        nextYield = Time.realtimeSinceStartup + yieldInterval;
                        StatusUpdate(progress);
                        activity = applier.Activity;
                    }
                    return timeRemaining;
                }

                while (patchThread.IsRunning)
                {
                    foreach (ILogMessage message in logQueue.TakeAll())
                    {
                        message.LogTo(logger);

                        if (updateTimeRemaining() < 0) yield return null;
                    }

                    float timeRemaining = updateTimeRemaining();
                    if (timeRemaining > 0) System.Threading.Thread.Sleep((int)(timeRemaining * 1000));
                    yield return null;
                }

                StatusUpdate(progress);
                activity = "ModuleManager - finishing up";
                yield return null;

                // Clear any log messages that might still be in the queue
                foreach (ILogMessage message in logQueue.TakeAll())
                {
                    message.LogTo(logger);
                }

                if (patchThread.IsExitedWithError)
                {
                    progress.Exception("The patch runner threw an exception", patchThread.Exception);
                    FatalErrorHandler.HandleFatalError("The patch runner threw an exception");
                    yield break;
                }

                logger.Info("Done patching");
                yield return null;

                PurgeUnused();

                #endregion Applying patches

                #region Saving Cache

                foreach (KeyValuePair<string, int> item in progress.Counter.warningFiles)
                {
                    logger.Warning(item.Value + " warning" + (item.Value > 1 ? "s" : "") + " related to GameData/" + item.Key);
                }

                if (progress.Counter.errors > 0 || progress.Counter.exceptions > 0)
                {
                    foreach (KeyValuePair<string, int> item in progress.Counter.errorFiles)
                    {
                        errors += item.Value + " error" + (item.Value > 1 ? "s" : "") + " related to GameData/" + item.Key
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
                    CreateCache(progress.Counter.patchedNodes);
                }

                StatusUpdate(progress);

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

            if (ModuleManager.dumpPostPatch)
                ModuleManager.OutputAllConfigs();

            yield return null;

            patchSw.Stop();
            logger.Info("Ran in " + ((float)patchSw.ElapsedMilliseconds / 1000).ToString("F3") + "s");

            ready = true;
        }

        private void LoadPhysicsConfig()
        {
            logger.Info("Loading Physics.cfg");
            UrlDir gameDataDir = GameDatabase.Instance.root.AllDirectories.First(d => d.path.EndsWith("GameData") && d.name == "" && d.url == "");
            // need to use a file with a cfg extension to get the right fileType or you can't AddConfig on it
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

        private bool IsCacheUpToDate()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create();
            System.Security.Cryptography.SHA256 filesha = System.Security.Cryptography.SHA256.Create();
            UrlDir.UrlFile[] files = GameDatabase.Instance.root.AllConfigFiles.ToArray();

            filesSha.Clear();

            for (int i = 0; i < files.Length; i++)
            {
                // Hash the file path so the checksum change if files are moved
                byte[] pathBytes = Encoding.UTF8.GetBytes(files[i].url);
                sha.TransformBlock(pathBytes, 0, pathBytes.Length, pathBytes, 0);
                
                // hash the file content
                byte[] contentBytes = File.ReadAllBytes(files[i].fullPath);
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

            byte[] godsFinalMessageToHisCreation = Encoding.UTF8.GetBytes("We apologize for the inconvenience.");
            sha.TransformFinalBlock(godsFinalMessageToHisCreation, 0, godsFinalMessageToHisCreation.Length);

            configSha = BitConverter.ToString(sha.Hash);
            sha.Clear();
            filesha.Clear();

            sw.Stop();

            logger.Info("SHA generated in " + ((float)sw.ElapsedMilliseconds / 1000).ToString("F3") + "s");
            logger.Info("      SHA = " + configSha);

            bool useCache = false;
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
            return useCache;
        }

        private bool CheckFilesChange(UrlDir.UrlFile[] files, ConfigNode shaConfigNode)
        {
            bool noChange = true;
            StringBuilder changes = new StringBuilder();

            changes.Append("Changes :\n");
            
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
                logger.Info(changes.ToString());
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
        

        private void CreateCache(int patchedNodeCount)
        {
            ConfigNode shaConfigNode = new ConfigNode();
            shaConfigNode.AddValue("SHA", configSha);
            shaConfigNode.AddValue("version", Assembly.GetExecutingAssembly().GetName().Version.ToString());
            shaConfigNode.AddValue("KSPVersion", Versioning.version_major + "." + Versioning.version_minor + "." + Versioning.Revision + "." + Versioning.BuildID);
            ConfigNode filesSHANode = shaConfigNode.AddNode("FilesSHA");

            ConfigNode cache = new ConfigNode();

            cache.AddValue("patchedNodeCount", patchedNodeCount.ToString());

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
                status = "ModuleManager: " + patchedNodeCount + " patch" + (patchedNodeCount != 1 ? "es" : "") +  " loaded from cache";

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
            progressFraction = 1;
            logger.Info("Cache Loaded");
        }

        private void StatusUpdate(IPatchProgress progress)
        {
            progressFraction = progress.ProgressFraction;

            status = "ModuleManager: " + progress.Counter.patchedNodes + " patch" + (progress.Counter.patchedNodes != 1 ? "es" : "") + " applied";

            if (progress.Counter.warnings > 0)
                status += ", found <color=yellow>" + progress.Counter.warnings + " warning" + (progress.Counter.warnings != 1 ? "s" : "") + "</yellow>";

            if (progress.Counter.errors > 0)
                status += ", found <color=orange>" + progress.Counter.errors + " error" + (progress.Counter.errors != 1 ? "s" : "") + "</color>";

            if (progress.Counter.exceptions > 0)
                status += ", encountered <color=red>" + progress.Counter.exceptions + " exception" + (progress.Counter.exceptions != 1 ? "s" : "") + "</color>";
        }

        private static void PurgeUnused()
        {
            foreach (UrlDir.UrlConfig mod in GameDatabase.Instance.root.AllConfigs.ToArray())
            {
                string name = mod.type.RemoveWS();

                if (CommandParser.Parse(name, out name) != Command.Insert)
                    mod.parent.configs.Remove(mod);
            }
        }

        #region Applying Patches

        // Name is group 1, index is group 2, vector related filed is group 3, vector separator is group 4, operator is group 5
        private static Regex parseValue = new Regex(@"([\w\&\-\.\?\*+/^!\(\) ]+(?:,[^*\d][\w\&\-\.\?\*\(\) ]*)*)(?:,(-?[0-9\*]+))?(?:\[((?:[0-9\*]+)+)(?:,(.))?\])?");

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
                
                Operator op;
                if (valName.Length > 2 && valName[valName.Length - 2] == ',')
                    op = Operator.Assign;
                else
                    op = OperatorParser.Parse(valName, out valName);

                if (cmd == Command.Special)
                {
                    ConfigNode.Value val = RecurseVariableSearch(valName, nodeStack.Push(mod), context);

                    if (val == null)
                    {
                        context.progress.Error(context.patchUrl, "Error - Cannot find value assigning command: " + valName);
                        continue;
                    }
                    
                    if (op != Operator.Assign)
                    {
                        if (double.TryParse(modVal.value, NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out double s) 
                            && double.TryParse(val.value, NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out double os))
                        {
                            switch (op)
                            {
                                case Operator.Multiply:
                                    val.value = (os * s).ToString(CultureInfo.InvariantCulture);
                                    break;

                                case Operator.Divide:
                                    val.value = (os / s).ToString(CultureInfo.InvariantCulture);
                                    break;

                                case Operator.Add:
                                    val.value = (os + s).ToString(CultureInfo.InvariantCulture);
                                    break;

                                case Operator.Subtract:
                                    val.value = (os - s).ToString(CultureInfo.InvariantCulture);
                                    break;

                                case Operator.Exponentiate:
                                    val.value = Math.Pow(os, s).ToString(CultureInfo.InvariantCulture);
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
            Operator op,
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
                if (op != Operator.Assign)
                {
                    if (op == Operator.RegexReplace)
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
                    else if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out double s) 
                             && double.TryParse(oValue, NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out double os))
                    {
                        switch (op)
                        {
                            case Operator.Multiply:
                                value = (os * s).ToString(CultureInfo.InvariantCulture);
                                break;

                            case Operator.Divide:
                                value = (os / s).ToString(CultureInfo.InvariantCulture);
                                break;

                            case Operator.Add:
                                value = (os + s).ToString(CultureInfo.InvariantCulture);
                                break;

                            case Operator.Subtract:
                                value = (os - s).ToString(CultureInfo.InvariantCulture);
                                break;

                            case Operator.Exponentiate:
                                value = Math.Pow(os, s).ToString(CultureInfo.InvariantCulture);
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
            bool compare = value != null && value.Length > 1 && (value[0] == '<' || value[0] == '>');
            compare = compare && double.TryParse(value.Substring(1), NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out val);

            string[] values = node.GetValues(type);
            for (int i = 0; i < values.Length; i++)
            {
                if (!compare && WildcardMatch(values[i], value))
                    return true;

                if (compare && double.TryParse(values[i], NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out double val2)
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
