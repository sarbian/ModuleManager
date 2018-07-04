using System;
using ModuleManager.Progress;

namespace ModuleManager.Patches.PassSpecifiers
{
    public interface IPassSpecifier
    {
        bool CheckNeeds(INeedsChecker needsChecker, IPatchProgress progress);
        string Descriptor { get; }
    }
}
