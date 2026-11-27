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

        // The type index of databaseConfigs, if it stays valid while the patch runs, for lookups
        // of root nodes (#$@PART[foo]/mass$). Null to search databaseConfigs.
        public readonly PatchTypeIndex typeIndex;

        public PatchContext(UrlDir.UrlConfig patchUrl, IEnumerable<IProtoUrlConfig> databaseConfigs, IBasicLogger logger, IPatchProgress progress)
            : this(patchUrl, databaseConfigs, logger, progress, null)
        {
        }

        public PatchContext(UrlDir.UrlConfig patchUrl, IEnumerable<IProtoUrlConfig> databaseConfigs, IBasicLogger logger, IPatchProgress progress, PatchTypeIndex typeIndex)
        {
            this.patchUrl = patchUrl;
            this.databaseConfigs = databaseConfigs;
            this.logger = logger;
            this.progress = progress;
            this.typeIndex = typeIndex;
        }
    }
}
