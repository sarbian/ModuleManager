using System;
using System.Collections.Generic;
using ModuleManager.Logging;
using ModuleManager.Patches.PassSpecifiers;
using ModuleManager.Progress;

namespace ModuleManager.Patches
{
    public interface IPatch
    {
        UrlDir.UrlConfig UrlConfig { get; }
        IPassSpecifier PassSpecifier { get; }
        void Apply(LinkedList<IProtoUrlConfig> configs, IPatchProgress progress, IBasicLogger logger);
    }
}
