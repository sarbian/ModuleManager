using System;
using Xunit;
using NSubstitute;
using TestUtils;
using ModuleManager;
using ModuleManager.Logging;
using ModuleManager.Patches;
using ModuleManager.Progress;

namespace ModuleManagerTests.Patches
{
    public class PatchCompilerTest
    {
        private readonly IPatchProgress progress = Substitute.For<IPatchProgress>();
        private readonly IBasicLogger logger = Substitute.For<IBasicLogger>();
        private readonly UrlDir.UrlFile file = UrlBuilder.CreateFile("abc/def.cfg");

        [Fact]
        public void TestCompilePatch__Edit()
        {
            UrlDir.UrlConfig patchUrlConfig = UrlBuilder.CreateConfig("ghi/jkl", new TestConfigNode("@NODE")
            {
                { "@bar", "bleh" },
            });

            EditPatch patch = Assert.IsType<EditPatch>(PatchCompiler.CompilePatch(patchUrlConfig, Command.Edit, "NODE[foo]:HAS[#bar]"));

            Assert.Same(patchUrlConfig, patch.UrlConfig);
            AssertNodeMatcher(patch.NodeMatcher);

            UrlDir.UrlConfig urlConfig = UrlBuilder.CreateConfig(new TestConfigNode("NODE")
            {
                { "name", "foo" },
                { "bar", "baz" },
            }, file);

            patch.Apply(file, progress, logger);

            AssertNoErrors();

            progress.Received().ApplyingUpdate(urlConfig, patchUrlConfig);

            Assert.Equal(1, file.configs.Count);
            Assert.NotSame(urlConfig, file.configs[0]);
            AssertNodesEqual(new TestConfigNode("NODE")
            {
                { "name", "foo" },
                { "bar", "bleh" },
            }, file.configs[0].config);
        }

        [Fact]
        public void TestCompilePatch__Copy()
        {
            UrlDir.UrlConfig patchUrlConfig = UrlBuilder.CreateConfig("ghi/jkl", new TestConfigNode("+NODE")
            {
                { "@name", "boo" },
                { "@bar", "bleh" },
            });

            CopyPatch patch = Assert.IsType<CopyPatch>(PatchCompiler.CompilePatch(patchUrlConfig, Command.Copy, "NODE[foo]:HAS[#bar]"));

            Assert.Same(patchUrlConfig, patch.UrlConfig);
            AssertNodeMatcher(patch.NodeMatcher);

            UrlDir.UrlConfig urlConfig = UrlBuilder.CreateConfig(new TestConfigNode("NODE")
            {
                { "name", "foo" },
                { "bar", "baz" },
            }, file);

            patch.Apply(file, progress, logger);

            AssertNoErrors();

            progress.Received().ApplyingCopy(urlConfig, patchUrlConfig);

            Assert.Equal(2, file.configs.Count);
            Assert.Same(urlConfig, file.configs[0]);
            AssertNodesEqual(new TestConfigNode("NODE")
            {
                { "name", "foo" },
                { "bar", "baz" },
            }, file.configs[0].config);
            AssertNodesEqual(new TestConfigNode("NODE")
            {
                { "name", "boo" },
                { "bar", "bleh" },
            }, file.configs[1].config);
        }

        [Fact]
        public void TestCompilePatch__Delete()
        {
            UrlDir.UrlConfig patchUrlConfig = UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode("-NODE"));

            DeletePatch patch = Assert.IsType<DeletePatch>(PatchCompiler.CompilePatch(patchUrlConfig, Command.Delete, "NODE[foo]:HAS[#bar]"));

            Assert.Same(patchUrlConfig, patch.UrlConfig);
            AssertNodeMatcher(patch.NodeMatcher);

            UrlDir.UrlConfig urlConfig = UrlBuilder.CreateConfig(new TestConfigNode("NODE")
            {
                { "name", "foo" },
                { "bar", "baz" },
            }, file);

            patch.Apply(file, progress, logger);

            AssertNoErrors();

            progress.Received().ApplyingDelete(urlConfig, patchUrlConfig);

            Assert.Equal(0, file.configs.Count);
        }

        [Fact]
        public void TestCompilePatch__NullUrlConfig()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                PatchCompiler.CompilePatch(null, Command.Edit, "NODE[foo]:HAS[#bar]");
            });

            Assert.Equal("urlConfig", ex.ParamName);
        }

        [Fact]
        public void TestCompilePatch__NullName()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                PatchCompiler.CompilePatch(UrlBuilder.CreateConfig(new ConfigNode(), file), Command.Edit, null);
            });

            Assert.Equal("name", ex.ParamName);
        }

        [Fact]
        public void TestCompilePatch__BlankName()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(delegate
            {
                PatchCompiler.CompilePatch(UrlBuilder.CreateConfig(new ConfigNode(), file), Command.Edit, "");
            });

            Assert.Equal("name", ex.ParamName);
            Assert.Equal("can't be empty\r\nParameter name: name", ex.Message);
        }

        [Fact]
        public void TestCompilePatch__InvalidCommand__Replace()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(delegate
            {
                PatchCompiler.CompilePatch(UrlBuilder.CreateConfig(new ConfigNode(), file), Command.Replace, "NODE[foo]:HAS[#bar]");
            });

            Assert.Equal("command", ex.ParamName);
            Assert.Equal("invalid command for a root node: Replace\r\nParameter name: command", ex.Message);
        }

        [Fact]
        public void TestCompilePatch__InvalidCommand__Create()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(delegate
            {
                PatchCompiler.CompilePatch(UrlBuilder.CreateConfig(new ConfigNode(), file), Command.Create, "NODE[foo]:HAS[#bar]");
            });

            Assert.Equal("command", ex.ParamName);
            Assert.Equal("invalid command for a root node: Create\r\nParameter name: command", ex.Message);
        }

        [Fact]
        public void TestCompilePatch__InvalidCommand__Rename()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(delegate
            {
                PatchCompiler.CompilePatch(UrlBuilder.CreateConfig(new ConfigNode(), file), Command.Rename, "NODE[foo]:HAS[#bar]");
            });

            Assert.Equal("command", ex.ParamName);
            Assert.Equal("invalid command for a root node: Rename\r\nParameter name: command", ex.Message);
        }

        [Fact]
        public void TestCompilePatch__InvalidCommand__Paste()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(delegate
            {
                PatchCompiler.CompilePatch(UrlBuilder.CreateConfig(new ConfigNode(), file), Command.Paste, "NODE[foo]:HAS[#bar]");
            });

            Assert.Equal("command", ex.ParamName);
            Assert.Equal("invalid command for a root node: Paste\r\nParameter name: command", ex.Message);
        }

        [Fact]
        public void TestCompilePatch__InvalidCommand__Special()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(delegate
            {
                PatchCompiler.CompilePatch(UrlBuilder.CreateConfig(new ConfigNode(), file), Command.Special, "NODE[foo]:HAS[#bar]");
            });

            Assert.Equal("command", ex.ParamName);
            Assert.Equal("invalid command for a root node: Special\r\nParameter name: command", ex.Message);
        }

        private void AssertNodeMatcher(INodeMatcher matcher)
        {
            Assert.True(matcher.IsMatch(new TestConfigNode("NODE")
            {
                { "name", "foo" },
                { "bar", "baz" },
            }));

            Assert.False(matcher.IsMatch(new TestConfigNode("NODE")
            {
                { "name", "foo" },
            }));

            Assert.False(matcher.IsMatch(new TestConfigNode("NODE")
            {
                { "name", "boo" },
                { "bar", "baz" },
            }));

            Assert.False(matcher.IsMatch(new ConfigNode("NODE")));

            Assert.False(matcher.IsMatch(new TestConfigNode("NADE")
            {
                { "name", "foo" },
                { "bar", "baz" },
            }));

            Assert.False(matcher.IsMatch(new TestConfigNode("NODE")
            {
                { "name", "boo" },
                { "bar", "baz" },
            }));
        }

        private void AssertNodesEqual(ConfigNode expected, ConfigNode actual)
        {
            Assert.Equal(expected.ToString(), actual.ToString());
        }

        private void AssertNoErrors()
        {
            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);
        }
    }
}
