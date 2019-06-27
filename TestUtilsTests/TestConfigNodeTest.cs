using System;
using Xunit;
using TestUtils;

namespace TestUtilsTests
{
    public class TestConfigNodeTest
    {
        [Fact]
        public void TestTestConfigNode()
        {
            ConfigNode node = new TestConfigNode("NODE")
            {
                { "value1", "something" },
                { "value2", "something else" },
                { "multiple", "first" },
                { "multiple", "second" },
                new ConfigNode.Value("foo", "bar"),
                { "weird_values", "some\r\n\tstuff" },
                { "NODE_1", new TestConfigNode
                    {
                        { "name", "something" },
                        { "stuff", "something else" },
                    }
                },
                new TestConfigNode("MULTIPLE")
                {
                    { "value3", "blah" },
                    { "value4", "bleh" },
                },
                new TestConfigNode("MULTIPLE")
                {
                    { "value3", "blih" },
                    { "value4", "bloh" },
                },
            };

            Assert.Equal(6, node.values.Count);
            AssertValue("value1", "something", node.values[0]);
            AssertValue("value2", "something else", node.values[1]);
            AssertValue("multiple", "first", node.values[2]);
            AssertValue("multiple", "second", node.values[3]);
            AssertValue("foo", "bar", node.values[4]);
            AssertValue("weird_values", "some\r\n\tstuff", node.values[5]);

            Assert.Equal(3, node.nodes.Count);
            ConfigNode innerNode1 = node.GetNode("NODE_1");
            Assert.NotNull(innerNode1);

            Assert.Equal("NODE_1", node.nodes[0].name);
            Assert.Equal(2, node.nodes[0].values.Count);
            AssertValue("name", "something", node.nodes[0].values[0]);
            AssertValue("stuff", "something else", node.nodes[0].values[1]);
            Assert.Empty(node.nodes[0].nodes);

            Assert.Equal("MULTIPLE", node.nodes[1].name);
            Assert.Equal(2, node.nodes[1].values.Count);
            AssertValue("value3", "blah", node.nodes[1].values[0]);
            AssertValue("value4", "bleh", node.nodes[1].values[1]);
            Assert.Empty(node.nodes[1].nodes);

            Assert.Equal("MULTIPLE", node.nodes[2].name);
            Assert.Equal(2, node.nodes[2].values.Count);
            AssertValue("value3", "blih", node.nodes[2].values[0]);
            AssertValue("value4", "bloh", node.nodes[2].values[1]);
            Assert.Empty(node.nodes[2].nodes);
        }

        private void AssertValue(string name, string value, ConfigNode.Value nodeValue)
        {
            Assert.Equal(name, nodeValue.name);
            Assert.Equal(value, nodeValue.value);
        }
    }
}
