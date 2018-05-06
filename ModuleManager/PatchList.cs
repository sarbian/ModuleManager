using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModuleManager.Collections;
using ModuleManager.Patches;

namespace ModuleManager
{
    public interface IPatchList : IEnumerable<IPass>
    {
        bool HasMod(string mod);
        void AddFirstPatch(IPatch patch);
        void AddLegacyPatch(IPatch patch);
        void AddBeforePatch(string mod, IPatch patch);
        void AddForPatch(string mod, IPatch patch);
        void AddAfterPatch(string mod, IPatch patch);
        void AddFinalPatch(IPatch patch);
    }

    public class PatchList : IPatchList
    {
        private class ModPass
        {
            public readonly string name;
            public readonly Pass beforePass;
            public readonly Pass forPass;
            public readonly Pass afterPass;
            
            public ModPass(string name)
            {
                if (name == null) throw new ArgumentNullException(nameof(name));
                if (name == string.Empty) throw new ArgumentException("can't be blank", nameof(name));
                this.name = name.ToUpperInvariant();

                beforePass = new Pass($":BEFORE[{this.name}]");
                forPass = new Pass($":FOR[{this.name}]");
                afterPass = new Pass($":AFTER[{this.name}]");
            }

            public void AddBeforePatch(IPatch patch) => beforePass.Add(patch ?? throw new ArgumentNullException(nameof(patch)));
            public void AddForPatch(IPatch patch) => forPass.Add(patch ?? throw new ArgumentNullException(nameof(patch)));
            public void AddAfterPatch(IPatch patch) => afterPass.Add(patch ?? throw new ArgumentNullException(nameof(patch)));
        }

        private class ModPassCollection : IEnumerable<ModPass>
        {
            private readonly ModPass[] passesArray;
            private readonly Dictionary<string, ModPass> passesDict;

            public ModPassCollection(IEnumerable<string> modList)
            {
                int count = modList.Count();
                passesArray = new ModPass[count];
                passesDict = new Dictionary<string, ModPass>(count);

                int i = 0;
                foreach (string mod in modList)
                {
                    ModPass pass = new ModPass(mod);
                    passesArray[i] = pass;
                    passesDict.Add(mod.ToLowerInvariant(), pass);
                    i++;
                }
            }

            public ModPass this[string name] => passesDict[name.ToLowerInvariant()];
            public ModPass this[int index] => passesArray[index];

            public bool HasMod(string name) => passesDict.ContainsKey(name.ToLowerInvariant());

            public int Count => passesArray.Length;

            public ArrayEnumerator<ModPass> GetEnumerator() => new ArrayEnumerator<ModPass>(passesArray);
            IEnumerator<ModPass> IEnumerable<ModPass>.GetEnumerator() => GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private readonly Pass firstPatches = new Pass(":FIRST");
        private readonly Pass legacyPatches = new Pass(":LEGACY (default)");
        private readonly Pass finalPatches = new Pass(":FINAL");

        private readonly ModPassCollection modPasses;

        public PatchList(IEnumerable<string> modList)
        {
            modPasses = new ModPassCollection(modList);
        }

        public ArrayEnumerator<IPass> GetEnumerator() => new ArrayEnumerator<IPass>(EnumeratePasses());
        IEnumerator<IPass> IEnumerable<IPass>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool HasMod(string mod)
        {
            if (mod == null) throw new ArgumentNullException(nameof(mod));
            if (mod == string.Empty) throw new ArgumentException("can't be empty", nameof(mod));
            return modPasses.HasMod(mod);
        }

        public void AddFirstPatch(IPatch patch)
        {
            firstPatches.Add(patch ?? throw new ArgumentNullException(nameof(patch)));
        }

        public void AddLegacyPatch(IPatch patch)
        {
            legacyPatches.Add(patch ?? throw new ArgumentNullException(nameof(patch)));
        }

        public void AddBeforePatch(string mod, IPatch patch)
        {
            EnsureMod(mod);
            modPasses[mod].AddBeforePatch(patch ?? throw new ArgumentNullException(nameof(patch)));
        }

        public void AddForPatch(string mod, IPatch patch)
        {
            EnsureMod(mod);
            modPasses[mod].AddForPatch(patch ?? throw new ArgumentNullException(nameof(patch)));
        }

        public void AddAfterPatch(string mod, IPatch patch)
        {
            EnsureMod(mod);
            modPasses[mod].AddAfterPatch(patch ?? throw new ArgumentNullException(nameof(patch)));
        }

        public void AddFinalPatch(IPatch patch)
        {
            finalPatches.Add(patch ?? throw new ArgumentNullException(nameof(patch)));
        }

        private IPass[] EnumeratePasses()
        {
            IPass[] result = new IPass[modPasses.Count * 3 + 3];

            result[0] = firstPatches;
            result[1] = legacyPatches;

            for (int i = 0; i < modPasses.Count; i++)
            {
                result[i * 3 + 2] = modPasses[i].beforePass;
                result[i * 3 + 3] = modPasses[i].forPass;
                result[i * 3 + 4] = modPasses[i].afterPass;
            }

            result[result.Length - 1] = finalPatches;

            return result;
        }

        private void EnsureMod(string mod)
        {
            if (mod == null) throw new ArgumentNullException(nameof(mod));
            if (mod == string.Empty) throw new ArgumentException("can't be empty", nameof(mod));
            if (!HasMod(mod)) throw new KeyNotFoundException($"Mod '{mod}' not found");
        }
    }
}
