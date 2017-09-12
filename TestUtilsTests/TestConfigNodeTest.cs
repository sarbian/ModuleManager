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

            Assert.Equal("something", node.GetValue("value1"));
            Assert.Equal("something else", node.GetValue("value2"));
            Assert.Equal(new[] { "first", "second" }, node.GetValues("multiple"));

            ConfigNode innerNode1 = node.GetNode("NODE_1");
            Assert.NotNull(innerNode1);

            Assert.Equal("NODE_1", innerNode1.name);
            Assert.Equal("something", innerNode1.GetValue("name"));
            Assert.Equal("something else", innerNode1.GetValue("stuff"));

            ConfigNode[] innerNodes2 = node.GetNodes("MULTIPLE");
            Assert.NotNull(innerNodes2);
            Assert.Equal(2, innerNodes2.Length);

            ConfigNode innerNode2a = innerNodes2[0];
            Assert.NotNull(innerNode2a);
            Assert.Equal("blah", innerNode2a.GetValue("value3"));
            Assert.Equal("bleh", innerNode2a.GetValue("value4"));

            ConfigNode innerNode2b = innerNodes2[1];
            Assert.NotNull(innerNode2b);
            Assert.Equal("blih", innerNode2b.GetValue("value3"));
            Assert.Equal("bloh", innerNode2b.GetValue("value4"));
        }
    }
}
