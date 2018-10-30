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

        public string Activity { get; private set; }

        public PatchApplier(IPatchProgress progress, IBasicLogger logger)
        {
            this.progress = progress ?? throw new ArgumentNullException(nameof(progress));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IEnumerable<IProtoUrlConfig> ApplyPatches(IEnumerable<IPass> patches)
        {
            if (patches == null) throw new ArgumentNullException(nameof(patches));

            LinkedList<IProtoUrlConfig> databaseConfigs = new LinkedList<IProtoUrlConfig>();

            foreach (IPass pass in patches)
            {
                ApplyPatches(databaseConfigs, pass);
            }

            return databaseConfigs; 
        }

        private void ApplyPatches(LinkedList<IProtoUrlConfig> databaseConfigs, IPass pass)
        {
            logger.Info(pass.Name + " pass");
            Activity = "ModuleManager " + pass.Name;

            foreach (IPatch patch in pass)
            {
                try
                {
                    patch.Apply(databaseConfigs, progress, logger);
                    progress.PatchApplied();
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
