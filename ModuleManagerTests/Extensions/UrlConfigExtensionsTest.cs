using System;
using Xunit;
using TestUtils;
using ModuleManager.Extensions;

namespace ModuleManagerTests.Extensions
{
    public class UrlConfigExtensionsTest
    {
        [Fact]
        public void TestSafeUrl()
        {
            ConfigNode node = new TestConfigNode("SOME_NODE")
            {
                { "name", "this shouldn't show up" },
            };
            UrlDir.UrlConfig url = UrlBuilder.CreateConfig("abc/def", node);
            Assert.Equal("abc/def/SOME_NODE", url.SafeUrl());
        }

        [Fact]
        public void TestSafeUrl__Null()
        {
            UrlDir.UrlConfig url = null;
            Assert.Equal("<null>", url.SafeUrl());
        }

        [Fact]
        public void TestSafeUrl__NullParent()
        {
            UrlDir.UrlConfig url = new UrlDir.UrlConfig(null, new ConfigNode("SOME_NODE"));
            Assert.Equal("SOME_NODE", url.SafeUrl());
        }

        [Fact]
        public void TestSafeUrl__NullParent__NullName()
        {
            ConfigNode node = new ConfigNode
            {
                name = null
            };
            UrlDir.UrlConfig url = new UrlDir.UrlConfig(null, node);
            Assert.Equal("<blank>", url.SafeUrl());
        }

        [Fact]
        public void TestSafeUrl__NullParent__BlankName()
        {
            UrlDir.UrlConfig url = new UrlDir.UrlConfig(null, new ConfigNode(" "));
            Assert.Equal("<blank>", url.SafeUrl());
        }

        [Fact]
        public void TestSafeUrl__NullName()
        {
            ConfigNode node = new TestConfigNode()
            {
                { "name", "this shouldn't show up" },
            };
            node.name = null;
            UrlDir.UrlConfig url = UrlBuilder.CreateConfig("abc/def", node);
            Assert.Equal("abc/def/<blank>", url.SafeUrl());
        }

        [Fact]
        public void TestSafeUrl__BlankName()
        {
            ConfigNode node = new TestConfigNode(" ")
            {
                { "name", "this shouldn't show up" },
            };
            UrlDir.UrlConfig url = UrlBuilder.CreateConfig("abc/def", node);
            Assert.Equal("abc/def/<blank>", url.SafeUrl());
        }
    }
}
