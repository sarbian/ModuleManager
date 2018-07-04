using System;
using System.Collections.Generic;
using System.Linq;
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

        public void ApplyPatches(IEnumerable<UrlDir.UrlFile> configFiles, IEnumerable<IPass> patches)
        {
            if (configFiles == null) throw new ArgumentNullException(nameof(configFiles));
            if (patches == null) throw new ArgumentNullException(nameof(patches));
            foreach (IPass pass in patches)
            {
                ApplyPatches(configFiles, pass);
            }
        }

        private void ApplyPatches(IEnumerable<UrlDir.UrlFile> configFiles, IPass pass)
        {
            logger.Info(pass.Name + " pass");
            Activity = "ModuleManager " + pass.Name;

            foreach (IPatch patch in pass)
            {
                try
                {
                    foreach (UrlDir.UrlFile file in configFiles)
                    {
                        patch.Apply(file, progress, logger);
                    }
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
