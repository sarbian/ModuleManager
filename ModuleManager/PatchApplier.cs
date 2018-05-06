using System;
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
        
        private readonly IPatchList patchList;

        private readonly UrlDir.UrlFile[] allConfigFiles;

        public string Activity { get; private set; }

        public PatchApplier(IPatchList patchList, UrlDir databaseRoot, IPatchProgress progress, IBasicLogger logger)
        {
            this.patchList = patchList;
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

            foreach (IPatch patch in pass)
            {
                try
                {
                    foreach (UrlDir.UrlFile file in allConfigFiles)
                    {
                        patch.Apply(file, progress, logger);
                    }
                    progress.PatchApplied();
                }
                catch (Exception e)
                {
                    progress.Exception(patch.UrlConfig, "Exception while processing node : " + patch.UrlConfig.SafeUrl(), e);

                    try
                    {
                        logger.Error("Processed node was\n" + patch.UrlConfig.PrettyPrint());
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
