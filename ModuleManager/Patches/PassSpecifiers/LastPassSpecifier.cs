using System;
using ModuleManager.Progress;

namespace ModuleManager.Patches.PassSpecifiers
{
    public class LastPassSpecifier : IPassSpecifier
    {
        public readonly string mod;

        public LastPassSpecifier(string mod)
        {
            if (mod == string.Empty) throw new ArgumentException("can't be empty", nameof(mod));
            this.mod = mod ?? throw new ArgumentNullException(nameof(mod));
        }

        public bool CheckNeeds(INeedsChecker needsChecker, IPatchProgress progress) => true;
        public string Descriptor => $":LAST[{mod.ToUpper()}]";
    }
}
