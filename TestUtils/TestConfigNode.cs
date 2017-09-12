using System;
using System.Collections;

namespace TestUtils
{
    public class TestConfigNode : ConfigNode, IEnumerable
    {
        public TestConfigNode() : base() { }
        public TestConfigNode(string name) : base(name) { }

        public void Add(string name, string value) => AddValue(name, value);
        public void Add(string name, ConfigNode node) => AddNode(name, node);
        public void Add(ConfigNode node) => AddNode(node);

        public IEnumerator GetEnumerator() => throw new NotImplementedException();
    }
}
