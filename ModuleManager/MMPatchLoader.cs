using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using ModuleManager.Collections;
using ModuleManager.Logging;
using ModuleManager.Extensions;
using ModuleManager.Threading;
using ModuleManager.Tags;
using ModuleManager.Patches;
using ModuleManager.Progress;
using NodeStack = ModuleManager.Collections.ImmutableStack<ConfigNode>;

using static ModuleManager.FilePathRepository;

namespace ModuleManager
{
    public class MMPatchLoader
    {
        private const string PHYSICS_NODE_NAME = "PHYSICSGLOBALS";
        private const string TECH_TREE_NODE_NAME = "TechTree";

        public string status = "";

        public string errors = "";

        public static bool keepPartDB = false;

        private static readonly KeyValueCache<string, Regex> regexCache = new KeyValueCache<string, Regex>();

        private string configSha;
        private readonly Dictionary<string, string> filesSha = new Dictionary<string, string>();

        private const int STATUS_UPDATE_INVERVAL_MS = 33;

        private readonly IEnumerable<ModListGenerator.ModAddedByAssembly> modsAddedByAssemblies;
        private readonly IBasicLogger logger;

        public static void AddPostPatchCallback(ModuleManagerPostPatchCallback callback)
        {
            PostPatchLoader.AddPostPatchCallback(callback);
        }

        public MMPatchLoader(IEnumerable<ModListGenerator.ModAddedByAssembly> modsAddedByAssemblies, IBasicLogger logger)
        {
            this.modsAddedByAssemblies = modsAddedByAssemblies ?? throw new ArgumentNullException(nameof(modsAddedByAssemblies));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IEnumerable<IProtoUrlConfig> Run()
        {
            Stopwatch patchSw = new Stopwatch();
            patchSw.Start();

            status = "Checking Cache";
            logger.Info(status);

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

            IEnumerable<IProtoUrlConfig> databaseConfigs = null;

            if (!useCache)
            {
                if (!Directory.Exists(logsDirPath)) Directory.CreateDirectory(logsDirPath);
                MessageQueue<ILogMessage> patchLogQueue = new MessageQueue<ILogMessage>();
                QueueLogRunner logRunner = new QueueLogRunner(patchLogQueue);
                ITaskStatus loggingThreadStatus = BackgroundTask.Start(delegate
                {
                    using StreamLogger streamLogger = new StreamLogger(new FileStream(patchLogPath, FileMode.Create));
                    streamLogger.Info("Log started at " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                    logRunner.Run(streamLogger);
                    streamLogger.Info("Done!");
                });
                IBasicLogger patchLogger = new LogSplitter(logger, new QueueLogger(patchLogQueue));

                IPatchProgress progress = new PatchProgress(patchLogger);
                status = "Pre patch init";
                patchLogger.Info(status);
                IEnumerable<string> mods = ModListGenerator.GenerateModList(modsAddedByAssemblies, progress, patchLogger);

                // If we don't use the cache then it is best to clean the PartDatabase.cfg
                if (!keepPartDB && File.Exists(partDatabasePath))
                    File.Delete(partDatabasePath);

                LoadPhysicsConfig();

                #region Sorting Patches

                status = "Extracting patches";
                patchLogger.Info(status);

                UrlDir gameData = GameDatabase.Instance.root.children.First(dir => dir.type == UrlDir.DirectoryType.GameData && dir.name == "");
                INeedsChecker needsChecker = new NeedsChecker(mods, gameData, progress, patchLogger);
                ITagListParser tagListParser = new TagListParser(progress);
                IProtoPatchBuilder protoPatchBuilder = new ProtoPatchBuilder(progress);
                IPatchCompiler patchCompiler = new PatchCompiler();
                PatchExtractor extractor = new PatchExtractor(progress, patchLogger, needsChecker, tagListParser, protoPatchBuilder, patchCompiler);

                // Have to convert to an array because we will be removing patches
                IEnumerable<IPatch> extractedPatches =
                    GameDatabase.Instance.root.AllConfigs.Select(urlConfig => extractor.ExtractPatch(urlConfig)).Where(patch => patch != null);
                PatchList patchList = new PatchList(mods, extractedPatches, progress);

                #endregion

                #region Applying patches

                status = "Applying patches";
                patchLogger.Info(status);

                IPass currentPass = null;

                progress.OnPassStarted.Add(delegate (IPass pass)
                {
                    currentPass = pass;
                    StatusUpdate(progress, currentPass.Name);
                });

                System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
                stopwatch.Start();

                progress.OnPatchApplied.Add(delegate
                {
                    long timeRemaining = STATUS_UPDATE_INVERVAL_MS - stopwatch.ElapsedMilliseconds;
                    if (timeRemaining < 0)
                    {
                        StatusUpdate(progress, currentPass.Name);
                        stopwatch.Reset();
                        stopwatch.Start();
                    }
                });

                PatchApplier applier = new PatchApplier(progress, patchLogger);
                databaseConfigs = applier.ApplyPatches(patchList);

                stopwatch.Stop();
                StatusUpdate(progress);

                patchLogger.Info("Done patching");

                #endregion Applying patches

                #region Saving Cache

                foreach (KeyValuePair<string, int> item in progress.Counter.warningFiles)
                {
                    patchLogger.Warning(item.Value + " warning" + (item.Value > 1 ? "s" : "") + " related to GameData/" + item.Key);
                }

                if (progress.Counter.errors > 0 || progress.Counter.exceptions > 0)
                {
                    foreach (KeyValuePair<string, int> item in progress.Counter.errorFiles)
                    {
                        errors += item.Value + " error" + (item.Value > 1 ? "s" : "") + " related to GameData/" + item.Key
                                  + "\n";
                    }

                    patchLogger.Warning("Errors in patch prevents the creation of the cache");
                    try
                    {
                        if (File.Exists(cachePath))
                            File.Delete(cachePath);
                        if (File.Exists(shaPath))
                            File.Delete(shaPath);
                    }
                    catch (Exception e)
                    {
                        patchLogger.Exception("Exception while deleting stale cache ", e);
                    }
                }
                else
                {
                    status = "Saving Cache";
                    patchLogger.Info(status);
                    CreateCache(databaseConfigs, progress.Counter.patchedNodes);
                }

                StatusUpdate(progress);

                #endregion Saving Cache

                SaveModdedTechTree(databaseConfigs);
                SaveModdedPhysics(databaseConfigs);

                logRunner.RequestStop();

                // The log thread writes its last messages as soon as asked to stop
                while (loggingThreadStatus.IsRunning)
                {
                    System.Threading.Thread.Sleep(2);
                }

                if (loggingThreadStatus.IsExitedWithError)
                {
                    logger.Error("The patching thread threw an exception");
                    throw loggingThreadStatus.Exception;
                }
            }
            else
            {
                status = "Loading from Cache";
                logger.Info(status);
                databaseConfigs = LoadCache();

                if (File.Exists(patchLogPath))
                {
                    logger.Info("Dumping patch log");
                    logger.Info("\n#### BEGIN PATCH LOG ####\n\n\n" + File.ReadAllText(patchLogPath) + "\n\n\n#### END PATCH LOG ####");
                }
                else
                {
                    logger.Error("Patch log does not exist: " + patchLogPath);
                }
            }

            if (KSP.Localization.Localizer.Instance != null)
                KSP.Localization.Localizer.SwitchToLanguage(KSP.Localization.Localizer.CurrentLanguage);

            logger.Info(status + "\n" + errors);

            patchSw.Stop();
            logger.Info("Ran in " + ((float)patchSw.ElapsedMilliseconds / 1000).ToString("F3") + "s");

            return databaseConfigs;
        }

        private void LoadPhysicsConfig()
        {
            logger.Info("Loading Physics.cfg");
            UrlDir gameDataDir = GameDatabase.Instance.root.AllDirectories.First(d => d.path.EndsWith("GameData") && d.name == "" && d.url == "");
            // need to use a file with a cfg extension to get the right fileType or you can't AddConfig on it
            UrlDir.UrlFile physicsUrlFile = new UrlDir.UrlFile(gameDataDir, new FileInfo(defaultPhysicsPath));
            // Since it loaded the default config badly (sub node only) we clear it first
            physicsUrlFile.configs.Clear();
            // And reload it properly
            ConfigNode physicsContent = ConfigNode.Load(defaultPhysicsPath);
            physicsContent.name = PHYSICS_NODE_NAME;
            physicsUrlFile.AddConfig(physicsContent);
            gameDataDir.files.Add(physicsUrlFile);
        }

        private void SaveModdedPhysics(IEnumerable<IProtoUrlConfig> databaseConfigs)
        {
            IEnumerable<IProtoUrlConfig> configs = databaseConfigs.Where(config => config.NodeType == PHYSICS_NODE_NAME);
            int count = configs.Count();

            if (count == 0)
            {
                logger.Info($"No {PHYSICS_NODE_NAME} node found. No custom Physics config will be saved");
                return;
            }

            if (count > 1)
            {
                logger.Info($"{count} {PHYSICS_NODE_NAME} nodes found. A patch may be wrong. Using the first one");
            }

            configs.First().Node.Save(physicsPath);
        }

        private bool IsCacheUpToDate()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();

            using System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create();
            UrlDir.UrlFile[] files = GameDatabase.Instance.root.AllConfigFiles.ToArray();

            filesSha.Clear();

            // Worker threads hash each file, while this thread hashes the paths and the files'
            // hashes all together, in order, as they become ready.
            using (ConfigFileHasher hasher = new ConfigFileHasher(files.Select(file => file.fullPath).ToArray()))
            {
                for (int i = 0; i < files.Length; i++)
                {
                    string url = files[i].GetUrlWithExtension();
                    // Hash the file path so the checksum change if files are moved
                    byte[] pathBytes = Encoding.UTF8.GetBytes(url);
                    sha.TransformBlock(pathBytes, 0, pathBytes.Length, pathBytes, 0);

                    // hash the file content, through its own hash
                    string fileHash = hasher.WaitForHash(i);
                    byte[] hashBytes = Encoding.ASCII.GetBytes(fileHash);
                    sha.TransformBlock(hashBytes, 0, hashBytes.Length, hashBytes, 0);

                    if (!filesSha.ContainsKey(url))
                    {
                        filesSha.Add(url, fileHash);
                    }
                    else
                    {
                        logger.Warning("Duplicate fileSha key. This should not append. The key is " + url);
                    }
                }
            }

            // Hash the mods dll path so the checksum change if dlls are moved or removed (impact NEEDS)
            foreach (AssemblyLoader.LoadedAssembly dll in AssemblyLoader.loadedAssemblies)
            {
                string path = dll.url + "/" + dll.name;
                byte[] pathBytes = Encoding.UTF8.GetBytes(path);
                sha.TransformBlock(pathBytes, 0, pathBytes.Length, pathBytes, 0);
            }

            foreach (ModListGenerator.ModAddedByAssembly mod in modsAddedByAssemblies)
            {
                byte[] modBytes = Encoding.UTF8.GetBytes(mod.modName);
                sha.TransformBlock(modBytes, 0, modBytes.Length, modBytes, 0);
            }

            byte[] godsFinalMessageToHisCreation = Encoding.UTF8.GetBytes("We apologize for the inconvenience.");
            sha.TransformFinalBlock(godsFinalMessageToHisCreation, 0, godsFinalMessageToHisCreation.Length);

            configSha = BitConverter.ToString(sha.Hash);
            sha.Clear();

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

            // The FILE nodes by filename, each list in node order: the first one is what a search
            // of the nodes for that filename finds
            ConfigNode.ConfigNodeList nodes = shaConfigNode.nodes;
            Dictionary<string, Queue<ConfigNode>> fileNodes = new Dictionary<string, Queue<ConfigNode>>();
            for (int i = 0; i < nodes.Count; i++)
            {
                ConfigNode file = nodes[i];
                if (file.name != "FILE" || !(file.GetValue("filename") is string filename)) continue;
                if (!fileNodes.TryGetValue(filename, out Queue<ConfigNode> queue))
                {
                    queue = new Queue<ConfigNode>(1);
                    fileNodes.Add(filename, queue);
                }
                queue.Enqueue(file);
            }

            string[] urls = new string[files.Length];
            for (int i = 0; i < files.Length; i++)
            {
                string url = urls[i] = files[i].GetUrlWithExtension();
                ConfigNode fileNode = fileNodes.TryGetValue(url, out Queue<ConfigNode> queue) ? queue.Peek() : null;
                string fileSha = fileNode?.GetValue("SHA");

                if (fileNode == null)
                    continue;

                if (fileSha == null || filesSha[url] != fileSha)
                {
                    changes.Append("Changed : " + fileNode.GetValue("filename") + ".cfg\n");
                    noChange = false;
                }
            }

            // Each file takes the first FILE node of its name not yet taken; the rest are deleted
            HashSet<ConfigNode> taken = new HashSet<ConfigNode>();
            for (int i = 0; i < files.Length; i++)
            {
                string url = urls[i];
                ConfigNode fileNode = fileNodes.TryGetValue(url, out Queue<ConfigNode> queue) && queue.Count > 0 ? queue.Dequeue() : null;

                if (fileNode == null)
                {
                    changes.Append("Added   : " + url + "\n");
                    noChange = false;
                }
                else
                {
                    taken.Add(fileNode);
                }
            }
            for (int i = 0; i < nodes.Count; i++)
            {
                ConfigNode fileNode = nodes[i];
                if (taken.Contains(fileNode)) continue;
                changes.Append("Deleted : " + fileNode.GetValue("filename") + "\n");
                noChange = false;
            }
            if (!noChange)
                logger.Info(changes.ToString());
            return noChange;
        }


        private void CreateCache(IEnumerable<IProtoUrlConfig> databaseConfigs, int patchedNodeCount)
        {
            ConfigNode shaConfigNode = new ConfigNode();
            shaConfigNode.AddValue("SHA", configSha);
            shaConfigNode.AddValue("version", Assembly.GetExecutingAssembly().GetName().Version.ToString());
            shaConfigNode.AddValue("KSPVersion", Versioning.version_major + "." + Versioning.version_minor + "." + Versioning.Revision + "." + Versioning.BuildID);
            ConfigNode filesSHANode = shaConfigNode.AddNode("FilesSHA");

            ConfigNode cache = new ConfigNode();

            cache.AddValue("patchedNodeCount", patchedNodeCount.ToString());

            foreach (IProtoUrlConfig urlConfig in databaseConfigs)
            {
                ConfigNode node = cache.AddNode("UrlConfig");
                node.AddValue("parentUrl", urlConfig.UrlFile.GetUrlWithExtension());

                // Written as a copy with its values sanitized and escaped would be, without the copy
                node.AddNode(urlConfig.Node);
            }

            foreach (var file in GameDatabase.Instance.root.AllConfigFiles)
            {
                string url = file.GetUrlWithExtension();
                // "/Physics" is the node we created manually to loads the PHYSIC config
                if (file.url != "/Physics" && filesSha.ContainsKey(url))
                {
                    ConfigNode shaNode = filesSHANode.AddNode("FILE");
                    shaNode.AddValue("filename", url);
                    shaNode.AddValue("SHA", filesSha[url]);
                    filesSha.Remove(url);
                }
            }

            logger.Info("Saving cache");

            try
            {
                ConfigNodeWriter.Save(shaConfigNode, shaPath);
            }
            catch (Exception e)
            {
                logger.Exception("Exception while saving the sha", e);
            }
            try
            {
                ConfigNodeWriter.SaveCache(cache, cachePath);
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

        private void SaveModdedTechTree(IEnumerable<IProtoUrlConfig> databaseConfigs)
        {
            IEnumerable<IProtoUrlConfig> configs = databaseConfigs.Where(config => config.NodeType == TECH_TREE_NODE_NAME);
            int count = configs.Count();

            if (count == 0)
            {
                logger.Info($"No {TECH_TREE_NODE_NAME} node found. No custom {TECH_TREE_NODE_NAME} will be saved");
                return;
            }

            if (count > 1)
            {
                logger.Info($"{count} {TECH_TREE_NODE_NAME} nodes found. A patch may be wrong. Using the first one");
            }

            ConfigNode techNode = new ConfigNode(TECH_TREE_NODE_NAME);
            techNode.AddNode(configs.First().Node);
            techNode.Save(techTreePath);
        }

        private IEnumerable<IProtoUrlConfig> LoadCache()
        {
            ConfigNode cache = ConfigNode.Load(cachePath);

            if (cache.HasValue("patchedNodeCount") && int.TryParse(cache.GetValue("patchedNodeCount"), out int patchedNodeCount))
                status = "ModuleManager: " + patchedNodeCount + " patch" + (patchedNodeCount != 1 ? "es" : "") +  " loaded from cache";

            // Create the fake file where we load the physic config cache
            UrlDir gameDataDir = GameDatabase.Instance.root.AllDirectories.First(d => d.path.EndsWith("GameData") && d.name == "" && d.url == "");
            // need to use a file with a cfg extension to get the right fileType or you can't AddConfig on it
            UrlDir.UrlFile physicsUrlFile = new UrlDir.UrlFile(gameDataDir, new FileInfo(defaultPhysicsPath));
            gameDataDir.files.Add(physicsUrlFile);

            List<IProtoUrlConfig> databaseConfigs = new List<IProtoUrlConfig>(cache.nodes.Count);

            // Most files hold several nodes: each file is looked up once
            Dictionary<string, UrlDir.UrlFile> parents = new Dictionary<string, UrlDir.UrlFile>();
            foreach (ConfigNode node in cache.nodes)
            {
                string parentUrl = node.GetValue("parentUrl");

                UrlDir.UrlFile parent;
                if (parentUrl == null)
                {
                    parent = gameDataDir.Find(parentUrl);
                }
                else if (!parents.TryGetValue(parentUrl, out parent))
                {
                    parent = gameDataDir.Find(parentUrl);
                    parents.Add(parentUrl, parent);
                }
                if (parent != null)
                {
                    node.nodes[0].UnescapeValuesRecursive();
                    databaseConfigs.Add(new ProtoUrlConfig(parent, node.nodes[0]));
                }
                else
                {
                    logger.Warning("Parent null for " + parentUrl);
                }
            }
            logger.Info("Cache Loaded");

            return databaseConfigs;
        }

        private void StatusUpdate(IPatchProgress progress, string activity = null)
        {
            status = "ModuleManager: " + progress.Counter.patchedNodes + " patch" + (progress.Counter.patchedNodes != 1 ? "es" : "") + " applied";
            if (progress.ProgressFraction < 1f - float.Epsilon)
                status += " (" + progress.ProgressFraction.ToString("P0") + ")";

            if (activity != null)
                status += "\n" + activity;

            if (progress.Counter.warnings > 0)
                status += ", found <color=yellow>" + progress.Counter.warnings + " warning" + (progress.Counter.warnings != 1 ? "s" : "") + "</color>";

            if (progress.Counter.errors > 0)
                status += ", found <color=orange>" + progress.Counter.errors + " error" + (progress.Counter.errors != 1 ? "s" : "") + "</color>";

            if (progress.Counter.exceptions > 0)
                status += ", encountered <color=red>" + progress.Counter.exceptions + " exception" + (progress.Counter.exceptions != 1 ? "s" : "") + "</color>";
        }

        #region Applying Patches

        // Name is group 1, index is group 2, vector related filed is group 3, vector separator is group 4, operator is group 5
        private static readonly Regex parseValue = new Regex(@"([\w\&\-\.\?\*\#+/^!\(\) ]+(?:,[^*\d][\w\&\-\.\?\*\(\) ]*)*)(?:,(-?[0-9\*]+))?(?:\[((?:[0-9\*]+)+)(?:,(.))?\])?");

        // What ModifyNode reads from a patch value's name (@key,1[2, ] *= ...). It depends on the
        // name alone and is the same for every node the patch applies to, so it is parsed once
        // per name. The errors it can lead to are still reported each time.
        private sealed class ValueCommand
        {
            public Command cmd;
            public Operator op;
            public string opValName;    // the name without its command and operator
            public bool matchSuccess;   // parseValue matched opValName
            public string name;         // group 1
            public bool hasIndex;       // group 2
            public bool isStar;
            public int index;
            public bool indexBad;
            public bool hasPosition;    // group 3
            public bool isPosStar;
            public int position;
            public bool positionBad;
            public char seperator;      // group 4
            public bool hasGroup5;
        }

        private static readonly KeyValueCache<string, ValueCommand> valueCommandCache = new KeyValueCache<string, ValueCommand>();
        private static readonly Func<string, ValueCommand> parseValueCommand = ParseValueCommand;

        private static ValueCommand ParseValueCommand(string modValName)
        {
            ValueCommand parsed = new ValueCommand();
            parsed.cmd = CommandParser.Parse(modValName, out string valName);

            if (valName.Length > 2 && valName[valName.Length - 2] == ',')
                parsed.op = Operator.Assign;
            else
                parsed.op = OperatorParser.Parse(valName, out valName);
            parsed.opValName = valName;

            if (parsed.cmd == Command.Special)
                return parsed;

            Match match = parseValue.Match(valName);
            parsed.matchSuccess = match.Success;
            if (!match.Success)
                return parsed;

            parsed.name = match.Groups[1].Value;

            parsed.hasPosition = match.Groups[3].Success;
            if (parsed.hasPosition)
            {
                if (match.Groups[3].Value == "*")
                    parsed.isPosStar = true;
                else if (!int.TryParse(match.Groups[3].Value, out parsed.position))
                    parsed.positionBad = true;
            }

            parsed.seperator = match.Groups[4].Success ? match.Groups[4].Value[0] : ',';

            parsed.hasIndex = match.Groups[2].Success;
            if (parsed.hasIndex)
            {
                if (match.Groups[2].Value == "*")
                    parsed.isStar = true;
                else if (!int.TryParse(match.Groups[2].Value, out parsed.index))
                    parsed.indexBad = true;
            }

            parsed.hasGroup5 = match.Groups[5].Success;
            return parsed;
        }

        // What ModifyNode reads from a patch subnode's name (@MODULE[foo],1:HAS[...]) when it
        // edits, copies, deletes or replaces existing subnodes. Parsed once per name.
        private sealed class SubNodeCommand
        {
            public string constraints;
            public string tag;
            public int index;
            public string nodeType;
            public string nodeName;
        }

        private static readonly KeyValueCache<string, SubNodeCommand> subNodeCommandCache = new KeyValueCache<string, SubNodeCommand>();
        private static readonly Func<string, SubNodeCommand> parseSubNodeCommand = ParseSubNodeCommand;

        private static SubNodeCommand ParseSubNodeCommand(string subName)
        {
            string constraints = "";
            string tag = "";
            string nodeType, nodeName;
            int index = 0;

            // three ways to specify:
            // NODE,n will match the nth node (NODE is the same as NODE,0)
            // NODE,* will match ALL nodes
            // NODE:HAS[condition] will match ALL nodes with condition
            if (subName.Contains(":HAS[", out int hasStart))
            {
                constraints = subName.Substring(hasStart + 5, subName.LastIndexOf(']') - hasStart - 5);
                subName = subName.Substring(0, hasStart);
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

            return new SubNodeCommand { constraints = constraints, tag = tag, index = index, nodeType = nodeType, nodeName = nodeName };
        }

        // A patch subnode's name with its whitespace removed
        private static readonly KeyValueCache<string, string> removeWSCache = new KeyValueCache<string, string>();
        private static readonly Func<string, string> removeWS = s => s.RemoveWS();

        private static readonly char[] braces = { '{', '}' };
        // Characters that the ConfigNode(string) constructor removes or splits a name on
        private static readonly char[] nameCleanupChars = { '\n', '\r', '\t', '(', ')', ' ' };

        // Set when something is written that a whole copy of its root would change, and that
        // a subnode shared instead of copied would keep: a special command (*@PART[foo]/bar = x)
        // writing a value with a brace, which the copy sanitizes, or a rename (|_ = A B) to a
        // name with a space, a parenthesis or a newline, which the copy cleans. From then on
        // edits copy whole roots again. PatchApplier clears it at the start of a run.
        public static volatile bool NeedWholeCopies;

        // ModifyNode applies the ConfigNode mod as a 'patch' to ConfigNode original, then returns the patched ConfigNode.
        // it uses FindConfigNodeIn(src, nodeType, nodeName, nodeTag) to recurse.
        public static ConfigNode ModifyNode(NodeStack original, ConfigNode mod, PatchContext context)
        {
            return ModifyNode(original, mod, context, false);
        }

        // Same as ModifyNode, but the new node shares the subnodes the patch leaves alone with
        // original, instead of being a whole copy: a subnode is copied when the patch first edits
        // (@) or replaces (%) it, at the same position, and edited there as ModifyNode would edit
        // its copy. original must not be used afterwards but to read it, as the old version of a
        // root an edit patch replaces. Nothing else writes into an existing subnode but special
        // commands (*@PART[foo]/bar = x): the patch must have none, nor pastes, which can carry
        // them, and nothing must have needed whole copies since (NeedWholeCopies).
        public static ConfigNode ModifyNodeSharingSubnodes(NodeStack original, ConfigNode mod, PatchContext context)
        {
            return ModifyNode(original, mod, context, true);
        }

        private sealed class ReferenceComparer : IEqualityComparer<ConfigNode>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            public bool Equals(ConfigNode x, ConfigNode y) => ReferenceEquals(x, y);
            public int GetHashCode(ConfigNode obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        private static ConfigNode ModifyNode(NodeStack original, ConfigNode mod, PatchContext context, bool shareSubnodes)
        {
            ConfigNode newNode;
            HashSet<ConfigNode> sharedSubnodes = null;
            Dictionary<ConfigNode, ConfigNode> subnodeCopies = null;
            if (shareSubnodes)
            {
                newNode = original.value.CopySharingSubnodes();
                sharedSubnodes = new HashSet<ConfigNode>(ReferenceComparer.Instance);
                for (int i = 0; i < newNode.nodes.Count; i++)
                    sharedSubnodes.Add(newNode.nodes[i]);
                subnodeCopies = new Dictionary<ConfigNode, ConfigNode>(ReferenceComparer.Instance);
            }
            else
            {
                newNode = original.value.DeepCopy();
            }
            NodeStack nodeStack = original.ReplaceValue(newNode);

            // The subnode to write into: a shared one is first replaced by its copy. A subnode
            // can be listed twice (negative indexes); the second time it resolves to the copy.
            ConfigNode Own(ConfigNode subNode)
            {
                if (sharedSubnodes == null) return subNode;
                if (subnodeCopies.TryGetValue(subNode, out ConfigNode copy)) return copy;
                if (!sharedSubnodes.Contains(subNode)) return subNode;
                copy = subNode.DeepCopy();
                newNode.ReplaceNode(subNode, copy);
                subnodeCopies.Add(subNode, copy);
                return copy;
            }

            #region Values

            #if LOGSPAM
            string vals = "[ModuleManager] modding values";
            #endif
            foreach (ConfigNode.Value modVal in mod.values)
            {
                #if LOGSPAM
                vals += "\n   " + modVal.name + "= " + modVal.value;
                #endif

                ValueCommand parsed = valueCommandCache.FetchWithKey(modVal.name, parseValueCommand);
                Command cmd = parsed.cmd;
                Operator op = parsed.op;
                string valName = parsed.opValName;

                if (cmd == Command.Special)
                {
                    ConfigNode.Value val = RecurseVariableSearch(valName, nodeStack.Push(mod), context);

                    if (val == null)
                    {
                        context.progress.Error(context.patchUrl, "Error - Cannot find value assigning command: " + valName);
                        continue;
                    }

                    // The value may be the name of a node in the database, which its index looks nodes up by
                    if (val.name == "name")
                        PatchTypeIndex.NameWritten();

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
                        // Stock sanitizes such a value when its root is next copied whole
                        if (val.value != null && val.value.IndexOfAny(braces) >= 0)
                            NeedWholeCopies = true;
                    }
                    continue;
                }

                if (!parsed.matchSuccess)
                {
                    context.progress.Error(context.patchUrl, "Error - Cannot parse value modifying command: " + valName);
                    continue;
                }

                // Get the bits and pieces from the regexp
                valName = parsed.name;

                // Get a position for editing a vector
                int position = parsed.position;
                bool isPosStar = parsed.isPosStar;
                if (parsed.positionBad)
                {
                    context.progress.Error(context.patchUrl, "Error - Unable to parse number as number. Very odd.");
                    continue;
                }
                char seperator = parsed.seperator;

                // In this case insert the value at position index (with the same node names)
                int index = parsed.index;
                bool isStar = parsed.isStar;
                if (parsed.indexBad)
                {
                    context.progress.Error(context.patchUrl, "Error - Unable to parse number as number. Very odd.");
                    continue;
                }

                string varValue;
                switch (cmd)
                {
                    case Command.Insert:
                        if (parsed.hasGroup5)
                        {
                            context.progress.Error(context.patchUrl, "Error - Cannot use operators with insert value: " + mod.name);
                        }
                        else
                        {
                            // Insert at the end by default
                            varValue = ProcessVariableSearch(modVal.value, nodeStack, context);
                            if (varValue != null)
                            {
                                // Without an index the value goes at the end, which needs no look at the others
                                if (parsed.hasIndex)
                                    InsertValue(newNode, index, valName, varValue);
                                else
                                    newNode.AddValueSafe(valName, varValue);
                            }
                            else
                                context.progress.Error(context.patchUrl, "Error - Cannot parse variable search when inserting new key " + valName + " = " +
                                    modVal.value);
                        }
                        break;

                    case Command.Replace:
                        if (parsed.hasIndex || parsed.hasGroup5 || valName.Contains('*')
                            || valName.Contains('?'))
                        {
                            if (parsed.hasIndex)
                                context.progress.Error(context.patchUrl, "Error - Cannot use index with replace (%) value: " + mod.name);
                            if (parsed.hasGroup5)
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
                                newNode.AddValueSafe(valName, varValue);
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

                        // Counted before the loop: a copy adds values with the same name
                        int valCount = CountValues(newNode, valName);
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
                                    parsed.hasPosition,
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
                                        newNode.AddValueSafe(valName, value);
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
                        if (parsed.hasGroup5)
                        {
                            context.progress.Error(context.patchUrl, "Error - Cannot use operators with delete (- or !) value: " + mod.name);
                        }
                        else if (parsed.hasIndex)
                        {
                            int deleteCount = CountValues(newNode, valName);
                            while (index < deleteCount)
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
                        if (modVal.value == null || modVal.value.IndexOfAny(nameCleanupChars) >= 0)
                            NeedWholeCopies = true;
                        break;

                    case Command.Create:
                        if (parsed.hasIndex || parsed.hasGroup5 || valName.Contains('*')
                            || valName.Contains('?'))
                        {
                            if (parsed.hasIndex)
                                context.progress.Error(context.patchUrl, "Error - Cannot use index with create (&) value: " + mod.name);
                            if (parsed.hasGroup5)
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
                                    newNode.AddValueSafe(valName, varValue);
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
                subMod.name = removeWSCache.FetchWithKey(subMod.name, removeWS);

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
                    if (subName.LastIndexOf(',') > 0 && int.TryParse(subName.Substring(subName.LastIndexOf(',') + 1), out int index))
                    {
                        // In this case insert the node at position index
                        InsertNode(newNode, newSubMod, index);
                    }
                    else
                        newNode.AddNode(newSubMod);
                }
                else
                {
                    SubNodeCommand subCommand = subNodeCommandCache.FetchWithKey(subName, parseSubNodeCommand);
                    string constraints = subCommand.constraints;
                    string tag = subCommand.tag;
                    string nodeType = subCommand.nodeType, nodeName = subCommand.nodeName;
                    int index = subCommand.index;
                    #if LOGSPAM
                    string msg = "";
                    #endif
                    List<ConfigNode> subNodes = new List<ConfigNode>();

                    if (tag == "*" || constraints.Length > 0)
                    {
                        // get ALL nodes
                        if (command != Command.Replace)
                        {
                            FindConfigNodesFrom(newNode, nodeType, nodeName, index, GetParsedConstraints(constraints), false, subNodes);
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
                            ConfigNode target = Own(subNodes[0]);
                            ConfigNode newSubNode = ModifyNode(nodeStack.Push(target), subMod, context);
                            target.ShallowCopyFrom(newSubNode);
                            target.name = newSubNode.name;
                        }
                        else
                        {
                            // if not add the mod node without the % in its name
                            #if LOGSPAM
                            msg += "  Adding subnode " + subMod.name + "\n";
                            #endif

                            ConfigNode copy = new ConfigNode(nodeType);

                            if (nodeName != null)
                                copy.AddValueSafe("name", nodeName);

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
                                copy.AddValueSafe("name", nodeName);

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
                                    ConfigNode target = Own(subNode);
                                    newSubNode = ModifyNode(nodeStack.Push(target), subMod, context);
                                    target.ShallowCopyFrom(newSubNode);
                                    target.name = newSubNode.name;
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
            if (subName.Contains(":HAS[", out int hasStart))
            {
                constraint = subName.Substring(hasStart + 5, subName.LastIndexOf(']') - hasStart - 5);
                subName = subName.Substring(0, hasStart);
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
                ConfigNode rootNode = FindRootNode(context, nodeType, nodeName, out bool foundNodeType);
                if (rootNode != null)
                    nodeStack = new NodeStack(rootNode);

                if (!foundNodeType) context.logger.Warning("Can't find nodeType:" + nodeType);
                if (nodeStack == null) return null;
            }
            else
            {
                if (constraint.Length > 0)
                {
                    // get the first one matching
                    ConfigNode n = FindConfigNodesFrom(nodeStack.value, nodeType, nodeName, index, GetParsedConstraints(constraint), true, null);
                    nodeStack = n != null ? nodeStack.Push(n) : null;
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

        // The first root node in the database of type nodeType whose name value matches nodeName
        // (any node of that type if null), and whether there is any node of that type
        private static ConfigNode FindRootNode(PatchContext context, string nodeType, string nodeName, out bool foundNodeType)
        {
            PatchTypeIndex index = context.typeIndex;
            if (index == null)
            {
                foundNodeType = false;
                foreach (IProtoUrlConfig urlConfig in context.databaseConfigs)
                {
                    ConfigNode node = urlConfig.Node;

                    if (node.name != nodeType) continue;

                    foundNodeType = true;

                    if (nodeName == null || (node.GetValue("name") is string testNodeName && WildcardMatch(testNodeName, nodeName)))
                        return node;
                }
                return null;
            }

            // The same nodes in the same order: those of the type, in list order
            List<LinkedListNode<IProtoUrlConfig>> nodes = index.NodesOfType(nodeType);
            foundNodeType = nodes.Count > 0;
            if (nodeName == null)
                return foundNodeType ? nodes[0].Value.Node : null;

            if (nodeName.IndexOfAny(wildcardChars) < 0)
            {
                // A name without wildcards matches itself or itself followed by a newline: the
                // index has their positions, and the first of either is the first match
                List<int> exact = index.PositionsNamed(nodeType, nodeName);
                List<int> newline = index.PositionsNamed(nodeType, nodeName + "\n");
                int first = int.MaxValue;
                if (exact.Count > 0) first = exact[0];
                if (newline.Count > 0 && newline[0] < first) first = newline[0];
                return first != int.MaxValue ? nodes[first].Value.Node : null;
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                ConfigNode node = nodes[i].Value.Node;
                if (node.GetValue("name") is string testNodeName && WildcardMatch(testNodeName, nodeName))
                    return node;
            }
            return null;
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
                    nodeName = null;
                }

                ConfigNode rootNode = FindRootNode(context, nodeType, nodeName, out bool foundNodeType);
                if (rootNode != null)
                    return RecurseVariableSearch(path.Substring(nextSep + 1), new NodeStack(rootNode), context);

                if (!foundNodeType) context.logger.Warning("Can't find nodeType:" + nodeType);

                return null;
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
                if (subName.Contains(":HAS[", out int hasStart))
                {
                    constraint = subName.Substring(hasStart + 5, subName.LastIndexOf(']') - hasStart - 5);
                    subName = subName.Substring(0, hasStart);
                }
                else if (subName.Contains(','))
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
                    ConfigNode n = FindConfigNodesFrom(nodeStack.value, nodeType, nodeName, index, GetParsedConstraints(constraint), true, null);
                    if (n != null)
                        return RecurseVariableSearch(path.Substring(nextSep + 1), nodeStack.Push(n), context);
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

                for (int i = 1; i < split.Length - 1; i += 2)
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

                            Regex replace = regexCache.Fetch(split[1], delegate
                            {
                                return new Regex(split[1]);
                            });

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

        // CheckConstraints is called for every node a patch might apply to, with the same few
        // constraint strings, so each string is parsed once. The constraints it nests are looked
        // up when first checked, as the strings were parsed when first checked, and then kept, so
        // checking them again needs no lookup.
        public sealed class ParsedConstraints
        {
            internal bool empty;
            internal List<string> list;
            internal char op;
            internal string type;
            internal string name;
            internal string remaining;
            internal bool compare;
            internal double compareValue;
            // type and name have no wildcard: WildcardMatch is then a plain comparison
            internal bool typeLiteral;
            internal bool nameLiteral;

            private ParsedConstraints[] listParsed;
            private ParsedConstraints remainingParsed;

            internal ParsedConstraints ListItem(int i)
            {
                if (listParsed == null) listParsed = new ParsedConstraints[list.Count];
                return listParsed[i] ?? (listParsed[i] = GetParsedConstraints(list[i]));
            }

            internal ParsedConstraints Remaining => remainingParsed ?? (remainingParsed = GetParsedConstraints(remaining));
        }

        private static readonly KeyValueCache<string, ParsedConstraints> constraintsCache = new KeyValueCache<string, ParsedConstraints>();
        private static readonly Func<string, ParsedConstraints> parseConstraints = ParseConstraints;

        public static ParsedConstraints GetParsedConstraints(string constraints)
        {
            if (constraints == null) throw new ArgumentNullException("input");
            return constraintsCache.FetchWithKey(constraints, parseConstraints);
        }

        private static ParsedConstraints ParseConstraints(string constraints)
        {
            ParsedConstraints parsed = new ParsedConstraints();
            constraints = constraints.RemoveWS();

            if (constraints.Length == 0)
            {
                parsed.empty = true;
                return parsed;
            }

            List<string> constraintList = SplitConstraints(constraints);

            if (constraintList.Count != 1)
            {
                parsed.list = constraintList;
                return parsed;
            }

            constraints = constraintList[0];

            string remainingConstraints = "";
            if (constraints.Contains(":HAS[", out int hasStart))
            {
                hasStart += 5;
                remainingConstraints = constraints.Substring(hasStart, constraintList[0].LastIndexOf(']') - hasStart);
                constraints = constraints.Substring(0, hasStart - 5);
            }

            string[] splits = constraints.Split(contraintSeparators, 3);
            parsed.type = splits[0].Substring(1);
            parsed.name = splits.Length > 1 ? splits[1] : null;
            parsed.op = constraints[0];
            parsed.remaining = remainingConstraints;
            parsed.compare = ParseCompare(parsed.name, out parsed.compareValue);
            parsed.typeLiteral = parsed.type.IndexOfAny(wildcardChars) < 0;
            parsed.nameLiteral = parsed.name != null && parsed.name.IndexOfAny(wildcardChars) < 0;
            return parsed;
        }

        public static bool CheckConstraints(ConfigNode node, string constraints)
        {
            return CheckConstraints(node, GetParsedConstraints(constraints));
        }

        public static bool CheckConstraints(ConfigNode node, ParsedConstraints parsed)
        {
            if (parsed.empty)
                return true;

            if (parsed.list != null)
            {
                // Like ret = ret && Check(...) over the list: once one fails, the rest are not checked
                int count = parsed.list.Count;
                for (int i = 0; i < count; i++)
                {
                    if (!CheckConstraints(node, parsed.ListItem(i)))
                        return false;
                }
                return true;
            }

            string type = parsed.type;
            string name = parsed.name;

            switch (parsed.op)
            {
                case '@':
                case '!':

                    // @MODULE[ModuleAlternator] or !MODULE[ModuleAlternator]
                    bool not = (parsed.op == '!');

                    // Each subnode FindConfigNodeIn (node, type, name, index) returns for index = 0, 1, ...
                    // until it repeats one, in a single pass.
                    bool any = false;
                    ConfigNode last = null;
                    int c = node.nodes.Count;
                    for (int i = 0; i < c && !any; ++i)
                    {
                        ConfigNode subNode = node.nodes[i];
                        string subType = subNode.name;
                        if (parsed.typeLiteral && subType != null ? !LiteralMatch(subType, type) : !WildcardMatch(subType, type))
                            continue;
                        if (name != null && !(subNode.GetValue("name") is string subName && (parsed.nameLiteral ? LiteralMatch(subName, name) : WildcardMatch(subName, name))))
                            continue;
                        if (subNode == last)
                            break;
                        any = CheckConstraints(subNode, parsed.Remaining);
                        last = subNode;
                    }
                    if (last != null)
                        return not ^ any;
                    return not ^ false;

                case '#':

                    // #module[Winglet]. No value of that name means no match, as HasValue(type) was checked first.
                    if (WildcardMatchValues(node, type, name, parsed.compare, parsed.compareValue))
                        return CheckConstraints(node, parsed.Remaining);
                    return false;

                case '~':

                    // ~breakingForce[]  breakingForce is not present
                    // or: ~breakingForce[100]  will be true if it's present but not 100, too.
                    if (name == "" && node.HasValue(type))
                        return false;
                    if (name != "" && WildcardMatchValues(node, type, name, parsed.compare, parsed.compareValue))
                        return false;
                    return CheckConstraints(node, parsed.Remaining);

                default:
                    return false;
            }
        }

        // Whether value is a number comparison (<10, >0.5) and its number
        private static bool ParseCompare(string value, out double val)
        {
            val = 0;
            bool compare = value != null && value.Length > 1 && (value[0] == '<' || value[0] == '>');
            return compare && double.TryParse(value.Substring(1), NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out val);
        }

        public static bool WildcardMatchValues(ConfigNode node, string type, string value)
        {
            bool compare = ParseCompare(value, out double val);
            return WildcardMatchValues(node, type, value, compare, val);
        }

        // Walks the values once, in order, instead of collecting them with GetValues(type)
        private static bool WildcardMatchValues(ConfigNode node, string type, string value, bool compare, double val)
        {
            ConfigNode.ValueList values = node.values;
            int count = values.Count;
            for (int i = 0; i < count; i++)
            {
                ConfigNode.Value v = values[i];
                if (v.name != type)
                    continue;

                if (!compare && WildcardMatch(v.value, value))
                    return true;

                if (compare && double.TryParse(v.value, NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out double val2)
                    && ((value[0] == '<' && val2 < val) || (value[0] == '>' && val2 > val)))
                {
                    return true;
                }
            }
            return false;
        }

        private static readonly KeyValueCache<string, Regex> wildcardRegexCache = new KeyValueCache<string, Regex>();
        private static readonly char[] wildcardChars = { '*', '?' };

        public static bool WildcardMatch(string s, string wildcard)
        {
            if (wildcard == null)
                return true;
            if (s == null)
                throw new ArgumentNullException("input");

            if (wildcard.IndexOfAny(wildcardChars) < 0)
                return LiteralMatch(s, wildcard);

            // The pattern is the regex ^...$ with * as .* and ? as ., where . is any character but
            // a newline. Without a newline in s that is a plain glob match.
            if (s.IndexOf('\n') < 0)
                return GlobMatch(s, wildcard);

            Regex regex = wildcardRegexCache.FetchWithKey(wildcard, w =>
                new Regex("^" + Regex.Escape(w).Replace(@"\*", ".*").Replace(@"\?", ".") + "$"));
            return regex.IsMatch(s);
        }

        // WildcardMatch for a pattern without wildcards, whose regex is ^literal$: $ also matches
        // before a final newline. s must not be null.
        private static bool LiteralMatch(string s, string literal)
        {
            return s == literal
                || (s.Length == literal.Length + 1 && s[literal.Length] == '\n' && string.CompareOrdinal(s, 0, literal, 0, literal.Length) == 0);
        }

        // Whole-string match of s against a pattern where * is any run of characters and ? any one
        // character, compared ordinally. Backtracks only to the last *, which is enough for *.
        private static bool GlobMatch(string s, string pattern)
        {
            int si = 0, pi = 0;
            int starPi = -1, starSi = 0;
            int sLen = s.Length, pLen = pattern.Length;
            while (si < sLen)
            {
                if (pi < pLen)
                {
                    char pc = pattern[pi];
                    if (pc == '*')
                    {
                        starPi = pi++;
                        starSi = si;
                        continue;
                    }
                    if (pc == '?' || pc == s[si])
                    {
                        si++;
                        pi++;
                        continue;
                    }
                }
                if (starPi < 0)
                    return false;
                pi = starPi + 1;
                si = ++starSi;
            }
            while (pi < pLen && pattern[pi] == '*')
                pi++;
            return pi == pLen;
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

        private static int CountValues(ConfigNode node, string name)
        {
            ConfigNode.ValueList values = node.values;
            int count = 0;
            for (int i = 0; i < values.Count; i++)
                if (values[i].name == name)
                    count++;
            return count;
        }

        private static void InsertValue(ConfigNode newNode, int index, string name, string value)
        {
            string[] oldValues = newNode.GetValues(name);
            if (index < oldValues.Length)
            {
                newNode.RemoveValues(name);
                int i = 0;
                for (; i < index; ++i)
                    newNode.AddValueSafe(name, oldValues[i]);
                newNode.AddValueSafe(name, value);
                for (; i < oldValues.Length; ++i)
                    newNode.AddValueSafe(name, oldValues[i]);
                return;
            }
            newNode.AddValueSafe(name, value);
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
            // Among the subnodes whose type matches nodeType: without nodeName, the index-th (from
            // the end if negative, clamped to the first or last); with nodeName, the index-th of
            // those whose name matches, or the last one checked if there are fewer than that.
            int c = src.nodes.Count;
            ConfigNode last = null;
            if (nodeName == null)
            {
                if (index >= 0)
                {
                    for (int i = 0; i < c; ++i)
                    {
                        ConfigNode n = src.nodes[i];
                        if (WildcardMatch(n.name, nodeType))
                        {
                            last = n;
                            if (index-- == 0)
                                return n;
                        }
                    }
                    return last;
                }
                for (int i = c - 1; i >= 0; --i)
                {
                    ConfigNode n = src.nodes[i];
                    if (WildcardMatch(n.name, nodeType))
                    {
                        last = n;
                        if (++index == 0)
                            return n;
                    }
                }
                return last;
            }
            if (index >= 0)
            {
                for (int i = 0; i < c; ++i)
                {
                    ConfigNode n = src.nodes[i];
                    if (WildcardMatch(n.name, nodeType) && n.GetValue("name") is string name && WildcardMatch(name, nodeName))
                    {
                        last = n;
                        if (--index < 0)
                            return last;
                    }
                }
                return last;
            }
            for (int i = c - 1; i >= 0; --i)
            {
                ConfigNode n = src.nodes[i];
                if (WildcardMatch(n.name, nodeType) && n.GetValue("name") is string name && WildcardMatch(name, nodeName))
                {
                    last = n;
                    if (++index >= 0)
                        return last;
                }
            }
            return last;
        }

        // Walks the nodes FindConfigNodeIn(src, nodeType, nodeName, i) returns for i = index,
        // index + 1, ... until it returns null or the node it returned before, and keeps those
        // that pass the constraints: all of them in into, or only the first, which it returns.
        // From an index of 0 or more those are the matching subnodes from that index on, or the
        // last one if there are no more than index, found here in one pass.
        private static ConfigNode FindConfigNodesFrom(
            ConfigNode src,
            string nodeType,
            string nodeName,
            int index,
            ParsedConstraints constraints,
            bool firstOnly,
            List<ConfigNode> into)
        {
            if (index < 0)
            {
                // From the end then on from the start: rare, so as it always was
                ConfigNode n, last = null;
                while (true)
                {
                    n = FindConfigNodeIn(src, nodeType, nodeName, index++);
                    if (n == last || n == null)
                        return null;
                    if (CheckConstraints(n, constraints))
                    {
                        if (firstOnly) return n;
                        into.Add(n);
                    }
                    last = n;
                }
            }

            int matched = 0;
            ConfigNode lastMatch = null;
            int c = src.nodes.Count;
            for (int i = 0; i < c; ++i)
            {
                ConfigNode n = src.nodes[i];
                if (!WildcardMatch(n.name, nodeType))
                    continue;
                if (nodeName != null && !(n.GetValue("name") is string name && WildcardMatch(name, nodeName)))
                    continue;
                lastMatch = n;
                if (matched++ < index)
                    continue;
                if (CheckConstraints(n, constraints))
                {
                    if (firstOnly) return n;
                    into.Add(n);
                }
            }

            // Fewer matches than index: FindConfigNodeIn gave the last one, once
            if (matched > 0 && matched <= index && CheckConstraints(lastMatch, constraints))
            {
                if (firstOnly) return lastMatch;
                into.Add(lastMatch);
            }
            return null;
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

        #endregion Config Node Utilities
    }
}
