using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModuleManager.Collections;

namespace ModuleManager
{
    public class PatchList
    {
        public class ModPass
        {
            public readonly List<Patch> beforePatches = new List<Patch>(0);
            public readonly List<Patch> forPatches = new List<Patch>(0);
            public readonly List<Patch> afterPatches = new List<Patch>(0);

            public readonly string name;

            public ModPass(string name)
            {
                this.name = name;
            }
        }

        public class ModPassCollection : IEnumerable<ModPass>
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

            public bool HasMod(string name) => passesDict.ContainsKey(name.ToLowerInvariant());

            public ArrayEnumerator<ModPass> GetEnumerator() => new ArrayEnumerator<ModPass>(passesArray);
            IEnumerator<ModPass> IEnumerable<ModPass>.GetEnumerator() => GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public readonly List<Patch> firstPatches = new List<Patch> ();
        public readonly List<Patch> legacyPatches = new List<Patch>();
        public readonly List<Patch> finalPatches = new List<Patch>();

        public readonly ModPassCollection modPasses;

        public PatchList(IEnumerable<string> modList)
        {
            modPasses = new ModPassCollection(modList);
        }
    }
}
