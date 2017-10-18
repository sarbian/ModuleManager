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

        private readonly UrlDir.UrlFile[] allConfigFiles;

        public string Activity { get; private set; }

        public PatchApplier(PatchList patchList, UrlDir databaseRoot, IPatchProgress progress, IBasicLogger logger)
        {
            this.patchList = patchList;
            this.databaseRoot = databaseRoot;
            this.progress = progress;
            this.logger = logger;

            allConfigFiles = databaseRoot.AllConfigFiles.ToArray();
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
                    else if (cmd != Command.Edit && cmd != Command.Copy && cmd != Command.Delete)
                    {
                        logger.Warning("Invalid command encountered on a patch: " + mod.SafeUrl());
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
                    string[] patterns = splits.Length > 1 ? splits[1].Split(',', '|') : null;
                    string type = splits[0].Substring(1);

                    bool loop = mod.config.HasNode("MM_PATCH_LOOP");

                    foreach (UrlDir.UrlFile file in allConfigFiles)
                    {
                        if (cmd == Command.Edit)
                        {
                            foreach (UrlDir.UrlConfig url in file.configs)
                            {
                                if (!IsMatch(url, type, patterns, condition)) continue;
                                if (loop) logger.Info("Looping on " + mod.SafeUrl() + " to " + url.SafeUrl());

                                do
                                {
                                    progress.ApplyingUpdate(url, mod);
                                    url.config = MMPatchLoader.ModifyNode(new NodeStack(url.config), mod.config, context);
                                } while (loop && IsMatch(url, type, patterns, condition));

                                if (loop) url.config.RemoveNodes("MM_PATCH_LOOP");
                            }
                        }
                        else if (cmd == Command.Copy)
                        {
                            // Avoid checking the new configs we are creating
                            int count = file.configs.Count;
                            for (int i = 0; i < count; i++)
                            {
                                UrlDir.UrlConfig url = file.configs[i];
                                if (!IsMatch(url, type, patterns, condition)) continue;

                                ConfigNode clone = MMPatchLoader.ModifyNode(new NodeStack(url.config), mod.config, context);
                                if (url.config.HasValue("name") && url.config.GetValue("name") == clone.GetValue("name"))
                                {
                                    progress.Error(mod, $"Error - when applying copy {mod.SafeUrl()} to {url.SafeUrl()} - the copy needs to have a different name than the parent (use @name = xxx)");
                                }
                                else
                                {
                                    progress.ApplyingCopy(url, mod);
                                    file.AddConfig(clone);
                                }
                            }
                        }
                        else if (cmd == Command.Delete)
                        {
                            int i = 0;
                            while (i < file.configs.Count)
                            {
                                UrlDir.UrlConfig url = file.configs[i];

                                if (IsMatch(url, type, patterns, condition))
                                {
                                    progress.ApplyingDelete(url, mod);
                                    file.configs.RemoveAt(i);
                                }
                                else
                                {
                                    i++;
                                }
                            }
                        }
                        else
                        {
                            throw new NotImplementedException("This code should not be reachable");
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

        private static bool IsMatch(UrlDir.UrlConfig url, string type, string[] namePatterns, string constraints)
        {
            if (url.type != type) return false;

            if (namePatterns != null)
            {
                if (url.name == url.type) return false;

                bool match = false;
                foreach (string pattern in namePatterns)
                {
                    if (MMPatchLoader.WildcardMatch(url.name, pattern))
                    {
                        match = true;
                        break;
                    }
                }

                if (!match) return false;
            }

            return MMPatchLoader.CheckConstraints(url.config, constraints);
        }
    }
}
