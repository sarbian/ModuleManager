using System;
using ModuleManager.Logging;
using ModuleManager.Patches.PassSpecifiers;
using ModuleManager.Progress;

namespace ModuleManager.Patches
{
    public interface IPatch
    {
        UrlDir.UrlConfig UrlConfig { get; }
        INodeMatcher NodeMatcher { get; }
        IPassSpecifier PassSpecifier { get; }
        void Apply(UrlDir.UrlFile file, IPatchProgress progress, IBasicLogger logger);
    }
}
