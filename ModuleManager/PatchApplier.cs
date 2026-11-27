using System;
using System.Collections.Generic;
using ModuleManager.Logging;
using ModuleManager.Extensions;
using ModuleManager.Patches;
using ModuleManager.Progress;

namespace ModuleManager
{
    public class PatchApplier
    {
        private readonly IBasicLogger logger;
        private readonly IPatchProgress progress;

        public PatchApplier(IPatchProgress progress, IBasicLogger logger)
        {
            this.progress = progress ?? throw new ArgumentNullException(nameof(progress));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IEnumerable<IProtoUrlConfig> ApplyPatches(IEnumerable<IPass> patches)
        {
            if (patches == null) throw new ArgumentNullException(nameof(patches));

            LinkedList<IProtoUrlConfig> databaseConfigs = new LinkedList<IProtoUrlConfig>();
            MMPatchLoader.NeedWholeCopies = false;
            PatchTypeIndex index = new PatchTypeIndex(databaseConfigs);

            foreach (IPass pass in patches)
            {
                ApplyPatches(databaseConfigs, index, pass);
            }

            return databaseConfigs; 
        }

        private void ApplyPatches(LinkedList<IProtoUrlConfig> databaseConfigs, PatchTypeIndex index, IPass pass)
        {
            progress.PassStarted(pass);

            foreach (IPatch patch in pass)
            {
                try
                {
                    // Edit, copy and delete patches, nearly all of them, need only the nodes of one
                    // type, and keep the index up to date themselves. Inserts add one node of
                    // their type, after which the index rebuilds that group when next needed.
                    if (patch is EditPatch editPatch)
                    {
                        editPatch.Apply(databaseConfigs, index, progress, logger);
                    }
                    else if (patch is CopyPatch copyPatch)
                    {
                        copyPatch.Apply(databaseConfigs, index, progress, logger);
                    }
                    else if (patch is DeletePatch deletePatch)
                    {
                        deletePatch.Apply(databaseConfigs, index, progress, logger);
                    }
                    else
                    {
                        InvalidateFor(index, patch);
                        try
                        {
                            patch.Apply(databaseConfigs, progress, logger);
                        }
                        finally
                        {
                            // If anything reads the index while the patch is still adding or
                            // removing nodes, the stale group is rebuilt half way: mark it again.
                            InvalidateFor(index, patch);
                        }
                    }
                    if (patch.CountsAsPatch) progress.PatchApplied();
                }
                catch (Exception e)
                {
                    progress.Exception(patch.UrlConfig, "Exception while processing node : " + patch.UrlConfig.SafeUrl(), e);
                    logger.Error("Processed node was\n" + patch.UrlConfig.PrettyPrint());
                }
            }
        }

        private static void InvalidateFor(PatchTypeIndex index, IPatch patch)
        {
            if (patch is InsertPatch insertPatch)
                index.Invalidate(insertPatch.NodeType);
            else
                index.Invalidate();
        }
    }
}
