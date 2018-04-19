using System;
using System.Collections;
using System.Collections.Generic;
using ModuleManager.Patches;

namespace ModuleManager
{
    public interface IPass : IEnumerable<IPatch>
    {
        string Name { get; }
    }

    public class Pass : IPass
    {
        private readonly string name;
        private readonly List<IPatch> patches = new List<IPatch>(0);

        public Pass(string name)
        {
            this.name = name ?? throw new ArgumentNullException(nameof(name));
            if (name == string.Empty) throw new ArgumentException("can't be empty", nameof(name));
        }

        public string Name => name;

        public void Add(IPatch patch) => patches.Add(patch);

        public List<IPatch>.Enumerator GetEnumerator() => patches.GetEnumerator();
        IEnumerator<IPatch> IEnumerable<IPatch>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
