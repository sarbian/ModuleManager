using System;
using ModuleManager.Logging;

namespace ModuleManager
{
    public struct PatchContext
    {
        public readonly UrlDir.UrlConfig patchUrl;
        public readonly UrlDir databaseRoot;
        public readonly IBasicLogger logger;
        public readonly IPatchProgress progress;

        public PatchContext(UrlDir.UrlConfig patchUrl, UrlDir databaseRoot, IBasicLogger logger, IPatchProgress progress)
        {
            this.patchUrl = patchUrl;
            this.databaseRoot = databaseRoot;
            this.logger = logger;
            this.progress = progress;
        }
    }
}
