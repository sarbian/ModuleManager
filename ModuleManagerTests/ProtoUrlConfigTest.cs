using System;
using Xunit;
using TestUtils;
using ModuleManager;

namespace ModuleManagerTests
{
    public class ProtoUrlConfigTest
    {
        [Fact]
        public void TestContructor__UrlFileNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new ProtoUrlConfig(null, new ConfigNode());
            });

            Assert.Equal("urlFile", ex.ParamName);
        }

        [Fact]
        public void TestContructor__NodeNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new ProtoUrlConfig(UrlBuilder.CreateFile("foo/bar"), null);
            });

            Assert.Equal("node", ex.ParamName);
        }

        [Fact]
        public void TestUrlFile()
        {
            UrlDir.UrlFile urlFile = UrlBuilder.CreateFile("abc/def.cfg");
            ProtoUrlConfig protoUrlConfig = new ProtoUrlConfig(urlFile, new ConfigNode());

            Assert.Same(urlFile, protoUrlConfig.UrlFile);
        }

        [Fact]
        public void TestNode()
        {
            ConfigNode node = new ConfigNode("NODE");
            ProtoUrlConfig protoUrlConfig = new ProtoUrlConfig(UrlBuilder.CreateFile("foo/bar"), node);

            Assert.Same(node, protoUrlConfig.Node);
        }

        [Fact]
        public void TestFileUrl()
        {
            ProtoUrlConfig protoUrlConfig = new ProtoUrlConfig(UrlBuilder.CreateFile("abc/def.cfg"), new ConfigNode());

            Assert.Equal("abc/def.cfg", protoUrlConfig.FileUrl);
        }

        [Fact]
        public void TestNodeType()
        {
            ProtoUrlConfig protoUrlConfig = new ProtoUrlConfig(UrlBuilder.CreateFile("abc/def"), new ConfigNode("SOME_NODE"));

            Assert.Equal("SOME_NODE", protoUrlConfig.NodeType);
        }

        [Fact]
        public void TestFullUrl()
        {
            ProtoUrlConfig protoUrlConfig = new ProtoUrlConfig(UrlBuilder.CreateFile("abc/def.cfg"), new ConfigNode("SOME_NODE"));

            Assert.Equal("abc/def.cfg/SOME_NODE", protoUrlConfig.FullUrl);
        }

        [Fact]
        public void TestFullUrl__NameValue()
        {
            ConfigNode node = new TestConfigNode("SOME_NODE")
            {
                { "name", "some_value" },
            };

            ProtoUrlConfig protoUrlConfig = new ProtoUrlConfig(UrlBuilder.CreateFile("abc/def.cfg"), node);

            Assert.Equal("abc/def.cfg/SOME_NODE[some_value]", protoUrlConfig.FullUrl);
        }
    }
}
