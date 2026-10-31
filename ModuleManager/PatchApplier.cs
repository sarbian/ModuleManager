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
                    // Edit patches, most of them, need only the nodes of one type. Other patches
                    // add or remove nodes, after which the index rebuilds the groups they can
                    // affect when next needed: copies and deletes only touch their matcher's
                    // type, and inserts add one node of their type.
                    if (patch is EditPatch editPatch)
                    {
                        editPatch.Apply(databaseConfigs, index, progress, logger);
                    }
                    else
                    {
                        if (patch is CopyPatch copyPatch && copyPatch.NodeMatcher is NodeMatcher copyMatcher)
                            index.Invalidate(copyMatcher.Type);
                        else if (patch is DeletePatch deletePatch && deletePatch.NodeMatcher is NodeMatcher deleteMatcher)
                            index.Invalidate(deleteMatcher.Type);
                        else if (patch is InsertPatch insertPatch)
                            index.Invalidate(insertPatch.NodeType);
                        else
                            index.Invalidate();
                        patch.Apply(databaseConfigs, progress, logger);
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
    }
}
