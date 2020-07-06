using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModuleManager.Collections;
using ModuleManager.Patches;
using ModuleManager.Patches.PassSpecifiers;
using ModuleManager.Progress;

namespace ModuleManager
{
    public class PatchList : IEnumerable<IPass>
    {
        private class ModPass
        {
            public readonly string name;
            public readonly Pass beforePass;
            public readonly Pass forPass;
            public readonly Pass afterPass;
            public readonly Pass lastPass;

            public ModPass(string name)
            {
                if (name == null) throw new ArgumentNullException(nameof(name));
                if (name == string.Empty) throw new ArgumentException("can't be blank", nameof(name));
                this.name = name.ToUpperInvariant();

                beforePass = new Pass($":BEFORE[{this.name}]");
                forPass = new Pass($":FOR[{this.name}]");
                afterPass = new Pass($":AFTER[{this.name}]");
                lastPass = new Pass($":LAST[{this.name}]");
            }

            public void AddBeforePatch(IPatch patch) => beforePass.Add(patch ?? throw new ArgumentNullException(nameof(patch)));
            public void AddForPatch(IPatch patch) => forPass.Add(patch ?? throw new ArgumentNullException(nameof(patch)));
            public void AddAfterPatch(IPatch patch) => afterPass.Add(patch ?? throw new ArgumentNullException(nameof(patch)));
            public void AddLastPatch(IPatch patch) => lastPass.Add(patch ?? throw new ArgumentNullException(nameof(patch)));
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

        private readonly Pass insertPatches = new Pass(":INSERT (initial)");
        private readonly Pass firstPatches = new Pass(":FIRST");
        private readonly Pass legacyPatches = new Pass(":LEGACY (default)");
        private readonly Pass finalPatches = new Pass(":FINAL");

        private readonly ModPassCollection modPasses;
        private readonly SortedDictionary<string, Pass> lastPasses = new SortedDictionary<string, Pass>(StringComparer.InvariantCultureIgnoreCase);

        public PatchList(IEnumerable<string> modList, IEnumerable<IPatch> patches, IPatchProgress progress)
        {
            modPasses = new ModPassCollection(modList ?? throw new ArgumentNullException(nameof(modList)));
            if (patches == null) throw new ArgumentNullException(nameof(patches));
            if (progress == null) throw new ArgumentNullException(nameof(progress));

            foreach (IPatch patch in patches)
            {
                if (patch.PassSpecifier is InsertPassSpecifier)
                {
                    insertPatches.Add(patch);
                }
                else if (patch.PassSpecifier is FirstPassSpecifier)
                {
                    firstPatches.Add(patch);
                }
                else if (patch.PassSpecifier is LegacyPassSpecifier)
                {
                    legacyPatches.Add(patch);
                }
                else if (patch.PassSpecifier is BeforePassSpecifier beforePassSpecifier)
                {
                    EnsureMod(beforePassSpecifier.mod);
                    modPasses[beforePassSpecifier.mod].AddBeforePatch(patch);
                }
                else if (patch.PassSpecifier is ForPassSpecifier forPassSpecifier)
                {
                    EnsureMod(forPassSpecifier.mod);
                    modPasses[forPassSpecifier.mod].AddForPatch(patch);
                }
                else if (patch.PassSpecifier is AfterPassSpecifier afterPassSpecifier)
                {
                    EnsureMod(afterPassSpecifier.mod);
                    modPasses[afterPassSpecifier.mod].AddAfterPatch(patch);
                }
                else if (patch.PassSpecifier is LastPassSpecifier lastPassSpecifier)
                {
                    if (!lastPasses.TryGetValue(lastPassSpecifier.mod, out Pass thisPass))
                    {
                        thisPass = new Pass($":LAST[{lastPassSpecifier.mod.ToUpperInvariant()}]");
                        lastPasses.Add(lastPassSpecifier.mod.ToLowerInvariant(), thisPass);
                    }
                    thisPass.Add(patch);
                }
                else if (patch.PassSpecifier is FinalPassSpecifier)
                {
                    finalPatches.Add(patch);
                }
                else
                {
                    throw new NotImplementedException("Don't know what to do with pass specifier: " + patch.PassSpecifier.Descriptor);
                }

                if (patch.CountsAsPatch) progress.PatchAdded();
            }
        }

        public IEnumerator<IPass> GetEnumerator()
        {
            yield return insertPatches;
            yield return firstPatches;
            yield return legacyPatches;

            foreach (ModPass modPass in modPasses)
            {
                yield return modPass.beforePass;
                yield return modPass.forPass;
                yield return modPass.afterPass;
            }

            foreach (Pass lastPass in lastPasses.Values)
            {
                yield return lastPass;
            }

            yield return finalPatches;
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private void EnsureMod(string mod)
        {
            if (mod == null) throw new ArgumentNullException(nameof(mod));
            if (mod == string.Empty) throw new ArgumentException("can't be empty", nameof(mod));
            if (!modPasses.HasMod(mod)) throw new KeyNotFoundException($"Mod '{mod}' not found");
        }
    }
}
