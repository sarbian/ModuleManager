using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using TestUtils;
using ModuleManager.Extensions;

namespace ModuleManagerTests.Extensions
{
    public class ConfigNodeExtensionsTest
    {
        public void TestShallowCopyFrom()
        {
            ConfigNode fromNode = new TestConfigNode("SOME_NODE")
            {
                { "abc", "def" },
                { "ghi", "jkl" },
                new TestConfigNode("INNER_NODE_1")
                {
                    { "mno", "pqr" },
                    new TestConfigNode("INNER_INNER_NODE_1"),
                },
                new TestConfigNode("INNER_NODE_2")
                {
                    { "stu", "vwx" },
                    new TestConfigNode("INNER_INNER_NODE_2"),
                },
            };

            ConfigNode.Value value1 = fromNode.values[0];
            ConfigNode.Value value2 = fromNode.values[1];

            ConfigNode innerNode1 = fromNode.nodes[0];
            ConfigNode innerNode2 = fromNode.nodes[1];

            ConfigNode toNode = new TestConfigNode("SOME_OTHER_NODE")
            {
                { "value", "will be removed" },
                new TestConfigNode("NODE_WILL_BE_REMOVED"),
            };

            toNode.ShallowCopyFrom(fromNode);

            Assert.Equal("SOME_NODE", fromNode.name);
            Assert.Equal("SOME_OTHER_NODE", toNode.name);

            Assert.Equal(2, fromNode.values.Count);
            Assert.Equal(2, toNode.values.Count);

            Assert.Same(value1, fromNode.values[0]);
            Assert.Same(value1, toNode.values[0]);
            Assert.Equal("abc", value1.name);
            Assert.Equal("def", value1.value);

            Assert.Same(value2, fromNode.values[1]);
            Assert.Same(value2, toNode.values[1]);
            Assert.Equal("ghi", value2.name);
            Assert.Equal("jkl", value2.value);

            Assert.Equal(2, fromNode.nodes.Count);
            Assert.Equal(2, toNode.nodes.Count);

            Assert.Same(innerNode1, fromNode.nodes[0]);
            Assert.Same(innerNode1, toNode.nodes[0]);
            Assert.Equal("INNER_NODE_1", innerNode1.name);
            Assert.Equal(1, innerNode1.values.Count);
            Assert.Equal("mno", innerNode1.values[0].name);
            Assert.Equal("pqr", innerNode1.values[0].value);
            Assert.Equal(1, innerNode1.nodes.Count);
            Assert.Equal("INNER_INNER_NODE_1", innerNode1.nodes[0].name);
            Assert.Equal(0, innerNode1.nodes[0].values.Count);
            Assert.Equal(0, innerNode1.nodes[0].nodes.Count);

            Assert.Same(innerNode2, fromNode.nodes[1]);
            Assert.Same(innerNode2, toNode.nodes[1]);
            Assert.Equal("INNER_NODE_2", innerNode2.name);
            Assert.Equal(1, innerNode2.values.Count);
            Assert.Equal("stu", innerNode2.values[0].name);
            Assert.Equal("vwx", innerNode2.values[0].value);
            Assert.Equal(1, innerNode2.nodes.Count);
            Assert.Equal("INNER_INNER_NODE_2", innerNode2.nodes[0].name);
            Assert.Equal(0, innerNode2.nodes[0].values.Count);
            Assert.Equal(0, innerNode2.nodes[0].nodes.Count);
        }

        [Fact]
        public void TestDeepCopy()
        {
            ConfigNode fromNode = new TestConfigNode("SOME_NODE")
            {
                { "abc", "def" },
                { "ghi", "jkl" },
                new TestConfigNode("INNER_NODE_1")
                {
                    { "mno", "pqr" },
                    new TestConfigNode("INNER_INNER_NODE_1"),
                },
                new TestConfigNode("INNER_NODE_2")
                {
                    { "stu", "vwx" },
                    new TestConfigNode("INNER_INNER_NODE_2"),
                },
            };
            
            ConfigNode toNode = fromNode.DeepCopy();
            
            Assert.Equal("SOME_NODE", toNode.name);
            
            Assert.Equal(2, toNode.values.Count);
            
            Assert.NotSame(fromNode.values[0], toNode.values[0]);
            Assert.Equal("abc", toNode.values[0].name);
            Assert.Equal("def", toNode.values[0].value);
            
            Assert.NotSame(fromNode.values[1], toNode.values[1]);
            Assert.Equal("ghi", toNode.values[1].name);
            Assert.Equal("jkl", toNode.values[1].value);
            
            Assert.Equal(2, toNode.nodes.Count);

            ConfigNode innerNode1 = toNode.nodes[0];
            Assert.NotSame(fromNode.nodes[0], innerNode1);
            Assert.Equal("INNER_NODE_1", innerNode1.name);
            Assert.Equal(1, innerNode1.values.Count);
            Assert.NotSame(fromNode.nodes[0].values[0], innerNode1.values[0]);
            Assert.Equal("mno", innerNode1.values[0].name);
            Assert.Equal("pqr", innerNode1.values[0].value);
            Assert.Equal(1, toNode.nodes[0].nodes.Count);
            Assert.NotSame(fromNode.nodes[0].nodes[0], innerNode1.nodes[0]);
            Assert.Equal("INNER_INNER_NODE_1", innerNode1.nodes[0].name);
            Assert.Equal(0, innerNode1.nodes[0].values.Count);
            Assert.Equal(0, innerNode1.nodes[0].nodes.Count);

            ConfigNode innerNode2 = toNode.nodes[1];
            Assert.NotSame(fromNode.nodes[1], innerNode2);
            Assert.Equal("INNER_NODE_2", innerNode2.name);
            Assert.Equal(1, innerNode2.values.Count);
            Assert.NotSame(fromNode.nodes[1].values[0], innerNode2.values[0]);
            Assert.Equal("stu", innerNode2.values[0].name);
            Assert.Equal("vwx", innerNode2.values[0].value);
            Assert.Equal(1, innerNode2.nodes.Count);
            Assert.NotSame(fromNode.nodes[1].nodes[0], innerNode2.nodes[0]);
            Assert.Equal("INNER_INNER_NODE_2", innerNode2.nodes[0].name);
            Assert.Equal(0, innerNode2.nodes[0].values.Count);
            Assert.Equal(0, innerNode2.nodes[0].nodes.Count);
        }
    }
}
