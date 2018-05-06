using System;
using ModuleManager.Logging;
using ModuleManager.Progress;

namespace ModuleManager.Patches
{
    public interface IPatch
    {
        UrlDir.UrlConfig UrlConfig { get; }
        INodeMatcher NodeMatcher { get; }
        void Apply(UrlDir.UrlFile file, IPatchProgress progress, IBasicLogger logger);
    }
}
