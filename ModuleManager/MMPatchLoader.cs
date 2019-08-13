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
using UnityEngine;
using Debug = UnityEngine.Debug;

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
    [SuppressMessage("ReSharper", "StringLastIndexOfIsCultureSpecific.1")]
    [SuppressMessage("ReSharper", "StringIndexOfIsCultureSpecific.1")]
    public class MMPatchLoader
    {
        private const string PHYSICS_NODE_NAME = "PHYSICSGLOBALS";
        private const string TECH_TREE_NODE_NAME = "TechTree";

        public string status = "";

        public string errors = "";

        public static bool keepPartDB = false;

        private static readonly KeyValueCache<string, Regex> regexCache = new KeyValueCache<string, Regex>();

        private string configSha;
        private Dictionary<string, string> filesSha = new Dictionary<string, string>();

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
                    using (StreamLogger streamLogger = new StreamLogger(new FileStream(patchLogPath, FileMode.Create)))
                    {
                        streamLogger.Info("Log started at " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                        logRunner.Run(streamLogger);
                        streamLogger.Info("Done!");
                    }
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

                while (loggingThreadStatus.IsRunning)
                {
                    System.Threading.Thread.Sleep(100);
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

            System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create();
            System.Security.Cryptography.SHA256 filesha = System.Security.Cryptography.SHA256.Create();
            UrlDir.UrlFile[] files = GameDatabase.Instance.root.AllConfigFiles.ToArray();

            filesSha.Clear();

            for (int i = 0; i < files.Length; i++)
            {
                string url = files[i].GetUrlWithExtension();
                // Hash the file path so the checksum change if files are moved
                byte[] pathBytes = Encoding.UTF8.GetBytes(url);
                sha.TransformBlock(pathBytes, 0, pathBytes.Length, pathBytes, 0);

                // hash the file content
                byte[] contentBytes = File.ReadAllBytes(files[i].fullPath);
                sha.TransformBlock(contentBytes, 0, contentBytes.Length, contentBytes, 0);

                filesha.ComputeHash(contentBytes);
                if (!filesSha.ContainsKey(url))
                {
                    filesSha.Add(url, BitConverter.ToString(filesha.Hash));
                }
                else
                {
                    logger.Warning("Duplicate fileSha key. This should not append. The key is " + url);
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
                string url = files[i].GetUrlWithExtension();
                ConfigNode fileNode = GetFileNode(shaConfigNode, url);
                string fileSha = fileNode?.GetValue("SHA");

                if (fileNode == null)
                    continue;

                if (fileSha == null || filesSha[url] != fileSha)
                {
                    changes.Append("Changed : " + fileNode.GetValue("filename") + ".cfg\n");
                    noChange = false;
                }
            }
            for (int i = 0; i < files.Length; i++)
            {
                string url = files[i].GetUrlWithExtension();
                ConfigNode fileNode = GetFileNode(shaConfigNode, url);

                if (fileNode == null)
                {
                    changes.Append("Added   : " + url + "\n");
                    noChange = false;
                }
                shaConfigNode.RemoveNode(fileNode);
            }
            foreach (ConfigNode fileNode in shaConfigNode.GetNodes())
            {
                changes.Append("Deleted : " + fileNode.GetValue("filename") + "\n");
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

                ConfigNode urlNode = urlConfig.Node.DeepCopy();
                urlNode.EscapeValuesRecursive();

                node.AddNode(urlNode);
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

            foreach (ConfigNode node in cache.nodes)
            {
                string parentUrl = node.GetValue("parentUrl");

                UrlDir.UrlFile parent = gameDataDir.Find(parentUrl);
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
                            subNodes[0].ShallowCopyFrom(newSubNode);
                            subNodes[0].name = newSubNode.name;
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
                                    newSubNode = ModifyNode(nodeStack.Push(subNode), subMod, context);
                                    subNode.ShallowCopyFrom(newSubNode);
                                    subNode.name = newSubNode.name;
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
                bool foundNodeType = false;
                foreach (IProtoUrlConfig urlConfig in context.databaseConfigs)
                {
                    ConfigNode node = urlConfig.Node;

                    if (node.name != nodeType) continue;

                    foundNodeType = true;

                    if (nodeName == null || (node.GetValue("name") is string testNodeName && WildcardMatch(testNodeName, nodeName)))
                    {
                        nodeStack = new NodeStack(node);
                        break;
                    }
                }

                if (!foundNodeType) context.logger.Warning("Can't find nodeType:" + nodeType);
                if (nodeStack == null) return null;
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

                bool foundNodeType = false;
                foreach (IProtoUrlConfig urlConfig in context.databaseConfigs)
                {
                    ConfigNode node = urlConfig.Node;

                    if (node.name != nodeType) continue;

                    foundNodeType = true;

                    if (nodeName == null || (node.GetValue("name") is string testNodeName && WildcardMatch(testNodeName, nodeName)))
                    {
                        return RecurseVariableSearch(path.Substring(nextSep + 1), new NodeStack(node), context);
                    }
                }

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

            Regex regex = regexCache.Fetch(pattern, delegate
            {
                return new Regex(pattern);
            });
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

        #endregion Config Node Utilities
    }
}
