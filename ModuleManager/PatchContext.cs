using System;
using System.Collections.Generic;
using ModuleManager.Logging;
using ModuleManager.Progress;

namespace ModuleManager
{
    public struct PatchContext
    {
        public readonly UrlDir.UrlConfig patchUrl;
        public readonly IEnumerable<IProtoUrlConfig> databaseConfigs;
        public readonly IBasicLogger logger;
        public readonly IPatchProgress progress;

        public PatchContext(UrlDir.UrlConfig patchUrl, IEnumerable<IProtoUrlConfig> databaseConfigs, IBasicLogger logger, IPatchProgress progress)
        {
            this.patchUrl = patchUrl;
            this.databaseConfigs = databaseConfigs;
            this.logger = logger;
            this.progress = progress;
        }
    }
}
