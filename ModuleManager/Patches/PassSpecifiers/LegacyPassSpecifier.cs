using System;
using ModuleManager.Progress;

namespace ModuleManager.Patches.PassSpecifiers
{
    public class LegacyPassSpecifier : IPassSpecifier
    {
        public bool CheckNeeds(INeedsChecker needsChecker, IPatchProgress progress)
        {
            if (needsChecker == null) throw new ArgumentNullException(nameof(needsChecker));
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            return true;
        }

        public string Descriptor => ":LEGACY (default)";
    }
}
