using System;
using System.Collections;

namespace TestUtils
{
    public class TestConfigNode : ConfigNode, IEnumerable
    {
        public TestConfigNode() : base() { }
        public TestConfigNode(string name) : base(name) { }

        public void Add(string name, string value) => Add(new Value(name, value));
        public void Add(Value value) => values.Add(value);
        public void Add(string name, ConfigNode node) => AddNode(name, node);
        public void Add(ConfigNode node) => AddNode(node);

        public IEnumerator GetEnumerator() => throw new NotImplementedException();
    }
}
