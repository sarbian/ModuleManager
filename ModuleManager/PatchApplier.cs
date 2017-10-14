using System;
using System.Collections.Generic;
using System.Linq;
using ModuleManager.Logging;
using ModuleManager.Extensions;
using ModuleManager.Progress;
using NodeStack = ModuleManager.Collections.ImmutableStack<ConfigNode>;

namespace ModuleManager
{
    public class PatchApplier
    {
        private readonly IBasicLogger logger;
        private readonly IPatchProgress progress;

        private readonly UrlDir databaseRoot;
        private readonly PatchList patchList;

        public string Activity { get; private set; }

        public PatchApplier(PatchList patchList, UrlDir databaseRoot, IPatchProgress progress, IBasicLogger logger)
        {
            this.patchList = patchList;
            this.databaseRoot = databaseRoot;
            this.progress = progress;
            this.logger = logger;
        }

        public void ApplyPatches()
        {
            ApplyPatches(":FIRST", patchList.firstPatches);

            // any node without a :pass
            ApplyPatches(":LEGACY (default)", patchList.legacyPatches);

            foreach (PatchList.ModPass pass in patchList.modPasses)
            {
                string upperModName = pass.name.ToUpper();
                ApplyPatches($":BEFORE[{upperModName}]", pass.beforePatches);
                ApplyPatches($":FOR[{upperModName}]", pass.forPatches);
                ApplyPatches($":AFTER[{upperModName}]", pass.afterPatches);
            }

            // :Final node
            ApplyPatches(":FINAL", patchList.finalPatches);
        }

        private void ApplyPatches(string stage, IEnumerable<UrlDir.UrlConfig> patches)
        {
            logger.Info(stage + " pass");
            Activity = "ModuleManager " + stage;

            UrlDir.UrlConfig[] allConfigs = databaseRoot.AllConfigs.ToArray();

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
                    PatchContext context = new PatchContext(mod, databaseRoot, logger, progress);
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

                    foreach (UrlDir.UrlConfig url in allConfigs)
                    {
                        if (IsMatch(url.config, type, patterns, condition))
                        {
                            switch (cmd)
                            {
                                case Command.Edit:
                                    ConfigNode node = url.config;
                                    bool loop = mod.config.HasNode("MM_PATCH_LOOP");
                                    if (loop) logger.Info("Looping on " + mod.SafeUrl() + " to " + url.SafeUrl());

                                    do
                                    {
                                        progress.ApplyingUpdate(url, mod);
                                        node = MMPatchLoader.ModifyNode(new NodeStack(node), mod.config, context);
                                    } while (loop && IsMatch(node, type, patterns, condition));

                                    if (loop) node.RemoveNodes("MM_PATCH_LOOP");

                                    url.config = node;
                                    break;

                                case Command.Copy:
                                    ConfigNode clone = MMPatchLoader.ModifyNode(new NodeStack(url.config), mod.config, context);
                                    if (url.config.HasValue("name") && url.config.GetValue("name") == clone.GetValue("name"))
                                    {
                                        progress.Error(mod, $"Error - when applying copy {mod.SafeUrl()} to {url.SafeUrl()} - the copy needs to have a different name than the parent (use @name = xxx)");
                                    }
                                    else
                                    {
                                        progress.ApplyingCopy(url, mod);
                                        url.parent.configs.Add(new UrlDir.UrlConfig(url.parent, clone));
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
                        }
                    }
                    progress.PatchApplied();
                }
                catch (Exception e)
                {
                    progress.Exception(mod, "Exception while processing node : " + mod.SafeUrl(), e);

                    try
                    {
                        logger.Error("Processed node was\n" + mod.PrettyPrint());
                    }
                    catch (Exception ex2)
                    {
                        logger.Exception("Exception while attempting to print a node", ex2);
                    }
                }
            }
        }

        private static bool IsMatch(ConfigNode node, string type, string[] namePatterns, string constraints)
        {
            if (node.name != type) return false;

            if (namePatterns.Length > 0)
            {
                string name = node.GetValue("name");
                if (name == null) return false;

                if (!namePatterns.Any(pattern => MMPatchLoader.WildcardMatch(name, pattern))) return false;
            }

            return MMPatchLoader.CheckConstraints(node, constraints);
        }
    }
}
