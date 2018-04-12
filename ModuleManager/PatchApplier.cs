using System;
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
        private readonly IPatchList patchList;

        private readonly UrlDir.UrlFile[] allConfigFiles;

        public string Activity { get; private set; }

        public PatchApplier(IPatchList patchList, UrlDir databaseRoot, IPatchProgress progress, IBasicLogger logger)
        {
            this.patchList = patchList;
            this.databaseRoot = databaseRoot;
            this.progress = progress;
            this.logger = logger;

            allConfigFiles = databaseRoot.AllConfigFiles.ToArray();
        }

        public void ApplyPatches()
        {
            foreach (IPass pass in patchList)
            {
                ApplyPatches(pass);
            }
        }

        private void ApplyPatches(IPass pass)
        {
            logger.Info(pass.Name + " pass");
            Activity = "ModuleManager " + pass.Name;

            foreach (Patch patch in pass)
            {
                try
                {
                    string name = patch.node.name.RemoveWS();

                    if (patch.command == Command.Insert)
                    {
                        logger.Warning("Warning - Encountered insert node that should not exist at this stage: " + patch.urlConfig.SafeUrl());
                        continue;
                    }
                    else if (patch.command != Command.Edit && patch.command != Command.Copy && patch.command != Command.Delete)
                    {
                        logger.Warning("Invalid command encountered on a patch: " + patch.urlConfig.SafeUrl());
                        continue;
                    }

                    string upperName = name.ToUpper();
                    PatchContext context = new PatchContext(patch.urlConfig, databaseRoot, logger, progress);
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
                    string type = splits[0];

                    bool loop = patch.node.HasNode("MM_PATCH_LOOP");

                    foreach (UrlDir.UrlFile file in allConfigFiles)
                    {
                        if (patch.command == Command.Edit)
                        {
                            foreach (UrlDir.UrlConfig url in file.configs)
                            {
                                if (!patch.nodeMatcher.IsMatch(url.config)) continue;
                                if (loop) logger.Info($"Looping on {patch.urlConfig.SafeUrl()} to {url.SafeUrl()}");

                                do
                                {
                                    progress.ApplyingUpdate(url, patch.urlConfig);
                                    url.config = MMPatchLoader.ModifyNode(new NodeStack(url.config), patch.node, context);
                                } while (loop && patch.nodeMatcher.IsMatch(url.config));

                                if (loop) url.config.RemoveNodes("MM_PATCH_LOOP");
                            }
                        }
                        else if (patch.command == Command.Copy)
                        {
                            // Avoid checking the new configs we are creating
                            int count = file.configs.Count;
                            for (int i = 0; i < count; i++)
                            {
                                UrlDir.UrlConfig url = file.configs[i];
                                if (!patch.nodeMatcher.IsMatch(url.config)) continue;

                                ConfigNode clone = MMPatchLoader.ModifyNode(new NodeStack(url.config), patch.node, context);
                                if (url.config.HasValue("name") && url.config.GetValue("name") == clone.GetValue("name"))
                                {
                                    progress.Error(patch.urlConfig, $"Error - when applying copy {patch.urlConfig.SafeUrl()} to {url.SafeUrl()} - the copy needs to have a different name than the parent (use @name = xxx)");
                                }
                                else
                                {
                                    progress.ApplyingCopy(url, patch.urlConfig);
                                    file.AddConfig(clone);
                                }
                            }
                        }
                        else if (patch.command == Command.Delete)
                        {
                            int i = 0;
                            while (i < file.configs.Count)
                            {
                                UrlDir.UrlConfig url = file.configs[i];

                                if (patch.nodeMatcher.IsMatch(url.config))
                                {
                                    progress.ApplyingDelete(url, patch.urlConfig);
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
                    progress.Exception(patch.urlConfig, "Exception while processing node : " + patch.urlConfig.SafeUrl(), e);

                    try
                    {
                        logger.Error("Processed node was\n" + patch.urlConfig.PrettyPrint());
                    }
                    catch (Exception ex2)
                    {
                        logger.Exception("Exception while attempting to print a node", ex2);
                    }
                }
            }
        }
    }
}
