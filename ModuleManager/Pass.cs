using System;
using System.Collections;
using System.Collections.Generic;

namespace ModuleManager
{
    public interface IPass : IEnumerable<Patch>
    {
        string Name { get; }
    }

    public class Pass : IPass
    {
        private readonly string name;
        private readonly List<Patch> patches = new List<Patch>(0);

        public Pass(string name)
        {
            this.name = name ?? throw new ArgumentNullException(nameof(name));
            if (name == string.Empty) throw new ArgumentException("can't be empty", nameof(name));
        }

        public string Name => name;

        public void Add(Patch patch) => patches.Add(patch);

        public List<Patch>.Enumerator GetEnumerator() => patches.GetEnumerator();
        IEnumerator<Patch> IEnumerable<Patch>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
