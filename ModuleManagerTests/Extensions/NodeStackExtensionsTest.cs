using System;
using Xunit;
using ModuleManager.Collections;
using ModuleManager.Extensions;

namespace ModuleManagerTests.Extensions
{
    public class NodeStackExtensionsTest
    {
        [Fact]
        public void TestGetPath()
        {
            ConfigNode node1 = new ConfigNode("NODE1");
            ConfigNode node2 = new ConfigNode("NODE2");
            ConfigNode node3 = new ConfigNode("NODE3");

            ImmutableStack<ConfigNode> stack = new ImmutableStack<ConfigNode>(node1).Push(node2).Push(node3);

            Assert.Equal("NODE1/NODE2/NODE3", stack.GetPath());
        }
    }
}
