using System;
using System.Collections.Generic;
using Xunit;
using TestUtils;
using ModuleManager;

namespace ModuleManagerTests
{
    public class PatchTypeIndexTest
    {
        private readonly UrlDir.UrlFile file = UrlBuilder.CreateFile("abc/def.cfg");

        [Fact]
        public void TestConstructor__ConfigsNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new PatchTypeIndex(null);
            });

            Assert.Equal("configs", ex.ParamName);
        }

        [Fact]
        public void TestNodesOfType__TypeNull()
        {
            PatchTypeIndex index = new PatchTypeIndex(new LinkedList<IProtoUrlConfig>());
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                index.NodesOfType(null);
            });

            Assert.Equal("type", ex.ParamName);
        }

        [Fact]
        public void TestNodesOfType()
        {
            LinkedList<IProtoUrlConfig> configs = new LinkedList<IProtoUrlConfig>();
            LinkedListNode<IProtoUrlConfig> a1 = configs.AddLast(new ProtoUrlConfig(file, new ConfigNode("A")));
            LinkedListNode<IProtoUrlConfig> b1 = configs.AddLast(new ProtoUrlConfig(file, new ConfigNode("B")));
            LinkedListNode<IProtoUrlConfig> a2 = configs.AddLast(new ProtoUrlConfig(file, new ConfigNode("A")));
            configs.AddLast(new ProtoUrlConfig(file, new ConfigNode("a")));

            PatchTypeIndex index = new PatchTypeIndex(configs);

            Assert.Equal(new[] { a1, a2 }, index.NodesOfType("A"));
            Assert.Equal(new[] { b1 }, index.NodesOfType("B"));
            Assert.Empty(index.NodesOfType("C"));
        }

        [Fact]
        public void TestInvalidate()
        {
            LinkedList<IProtoUrlConfig> configs = new LinkedList<IProtoUrlConfig>();
            LinkedListNode<IProtoUrlConfig> a1 = configs.AddLast(new ProtoUrlConfig(file, new ConfigNode("A")));
            LinkedListNode<IProtoUrlConfig> a2 = configs.AddLast(new ProtoUrlConfig(file, new ConfigNode("A")));

            PatchTypeIndex index = new PatchTypeIndex(configs);
            Assert.Equal(new[] { a1, a2 }, index.NodesOfType("A"));

            LinkedListNode<IProtoUrlConfig> a3 = configs.AddAfter(a1, new ProtoUrlConfig(file, new ConfigNode("A")));
            configs.Remove(a2);

            // Not seen until invalidated
            Assert.Equal(new[] { a1, a2 }, index.NodesOfType("A"));

            index.Invalidate();
            Assert.Equal(new[] { a1, a3 }, index.NodesOfType("A"));
        }

        [Fact]
        public void TestInvalidate__Type()
        {
            LinkedList<IProtoUrlConfig> configs = new LinkedList<IProtoUrlConfig>();
            LinkedListNode<IProtoUrlConfig> a1 = configs.AddLast(new ProtoUrlConfig(file, new ConfigNode("A")));
            LinkedListNode<IProtoUrlConfig> b1 = configs.AddLast(new ProtoUrlConfig(file, new ConfigNode("B")));

            PatchTypeIndex index = new PatchTypeIndex(configs);
            List<LinkedListNode<IProtoUrlConfig>> bNodes = index.NodesOfType("B");

            LinkedListNode<IProtoUrlConfig> a2 = configs.AddFirst(new ProtoUrlConfig(file, new ConfigNode("A")));
            LinkedListNode<IProtoUrlConfig> c1 = configs.AddLast(new ProtoUrlConfig(file, new ConfigNode("C")));
            index.Invalidate("A");
            index.Invalidate("C");

            Assert.Equal(new[] { a2, a1 }, index.NodesOfType("A"));
            Assert.Equal(new[] { c1 }, index.NodesOfType("C"));
            Assert.Same(bNodes, index.NodesOfType("B"));

            configs.Remove(c1);
            index.Invalidate("C");
            Assert.Empty(index.NodesOfType("C"));
        }

        [Fact]
        public void TestInvalidate__TypeNull()
        {
            PatchTypeIndex index = new PatchTypeIndex(new LinkedList<IProtoUrlConfig>());
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                index.Invalidate(null);
            });

            Assert.Equal("type", ex.ParamName);
        }
    }
}
