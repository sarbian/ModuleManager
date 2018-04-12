using System;
using Xunit;
using NSubstitute;
using TestUtils;
using ModuleManager;

namespace ModuleManagerTests
{
    public class PatchTest
    {
        [Fact]
        public void TestConstructor()
        {
            UrlDir.UrlConfig urlConfig = UrlBuilder.CreateConfig("abc/def", new ConfigNode("NODE"));
            ConfigNode node = new ConfigNode("NADE");
            Patch patch = new Patch(urlConfig, Command.Edit, node);

            Assert.Same(urlConfig, patch.urlConfig);
            Assert.Equal(Command.Edit, patch.command);
            Assert.Same(node, patch.node);
        }

        [Fact]
        public void TestConstructor__ValidCommands()
        {
            UrlDir.UrlConfig urlConfig = UrlBuilder.CreateConfig("abc/def", new ConfigNode("NODE"));
            ConfigNode node = new ConfigNode("NADE");

            Patch patch1 = new Patch(urlConfig, Command.Edit, node);
            Assert.Same(urlConfig, patch1.urlConfig);
            Assert.Equal(Command.Edit, patch1.command);
            Assert.Same(node, patch1.node);

            Patch patch2 = new Patch(urlConfig, Command.Copy, node);
            Assert.Same(urlConfig, patch2.urlConfig);
            Assert.Equal(Command.Copy, patch2.command);
            Assert.Same(node, patch2.node);

            Patch patch3 = new Patch(urlConfig, Command.Delete, node);
            Assert.Same(urlConfig, patch3.urlConfig);
            Assert.Equal(Command.Delete, patch3.command);
            Assert.Same(node, patch3.node);

            ArgumentException ex1 = Assert.Throws<ArgumentException>(() => new Patch(urlConfig, Command.Create, node));
            Assert.Equal("Must be Edit, Copy, or Delete (got Create)\r\nParameter name: command", ex1.Message);

            ArgumentException ex2 = Assert.Throws<ArgumentException>(() => new Patch(urlConfig, Command.Insert, node));
            Assert.Equal("Must be Edit, Copy, or Delete (got Insert)\r\nParameter name: command", ex2.Message);

            ArgumentException ex3 = Assert.Throws<ArgumentException>(() => new Patch(urlConfig, Command.Paste, node));
            Assert.Equal("Must be Edit, Copy, or Delete (got Paste)\r\nParameter name: command", ex3.Message);

            ArgumentException ex4 = Assert.Throws<ArgumentException>(() => new Patch(urlConfig, Command.Rename, node));
            Assert.Equal("Must be Edit, Copy, or Delete (got Rename)\r\nParameter name: command", ex4.Message);

            ArgumentException ex5 = Assert.Throws<ArgumentException>(() => new Patch(urlConfig, Command.Replace, node));
            Assert.Equal("Must be Edit, Copy, or Delete (got Replace)\r\nParameter name: command", ex5.Message);

            ArgumentException ex6 = Assert.Throws<ArgumentException>(() => new Patch(urlConfig, Command.Special, node));
            Assert.Equal("Must be Edit, Copy, or Delete (got Special)\r\nParameter name: command", ex6.Message);
        }

        [Fact]
        public void TestConstructor__NullUrlConfig()
        {
            Assert.Throws<ArgumentNullException>(() => new Patch(null, Command.Edit, new ConfigNode("BLAH")));
        }

        [Fact]
        public void TestConstructor__NullNode()
        {
            UrlDir.UrlConfig urlConfig = UrlBuilder.CreateConfig("abc/def", new ConfigNode("NODE"));
            Assert.Throws<ArgumentNullException>(() => new Patch(urlConfig, Command.Edit, null));
        }
        
        [Fact]
        public void TestNodeMatcher()
        {
            UrlDir.UrlConfig urlConfig = UrlBuilder.CreateConfig("abc/def", new ConfigNode("NODE[blah]:HAS[@FOO[bar*],#something[else]]"));
            Patch patch = new Patch(urlConfig, Command.Edit, urlConfig.config);
            INodeMatcher matcher = patch.nodeMatcher;

            Assert.True(matcher.IsMatch(new TestConfigNode("NODE")
            {
                { "name", "blah" },
                { "something", "else" },
                new TestConfigNode("FOO")
                {
                    { "name", "barbar" },
                },
            }));

            Assert.False(matcher.IsMatch(new TestConfigNode("NODE")
            {
                { "name", "blah" },
            }));

            Assert.False(matcher.IsMatch(new TestConfigNode("NODE")
            {
                { "name", "blah" },
                { "something", "else" },
            }));

            Assert.False(matcher.IsMatch(new TestConfigNode("NODE")
            {
                { "name", "blah" },
                new TestConfigNode("FOO")
                {
                    { "name", "barbar" },
                },
            }));

            Assert.False(matcher.IsMatch(new TestConfigNode("NODE")
            {
                { "name", "bleh" },
                { "something", "else" },
                new TestConfigNode("FOO")
                {
                    { "name", "barbar" },
                },
            }));

            Assert.False(matcher.IsMatch(new TestConfigNode("NADE")
            {
                { "name", "blah" },
                { "something", "else" },
                new TestConfigNode("FOO")
                {
                    { "name", "barbar" },
                },
            }));
        }
    }
}
