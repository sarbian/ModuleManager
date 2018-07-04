using System;
using Xunit;
using NSubstitute;
using TestUtils;
using ModuleManager;
using ModuleManager.Logging;
using ModuleManager.Patches;
using ModuleManager.Patches.PassSpecifiers;
using ModuleManager.Progress;

namespace ModuleManagerTests.Patches
{
    public class PatchCompilerTest
    {
        private readonly IPatchProgress progress = Substitute.For<IPatchProgress>();
        private readonly IBasicLogger logger = Substitute.For<IBasicLogger>();
        private readonly UrlDir.UrlFile file = UrlBuilder.CreateFile("abc/def.cfg");
        private readonly PatchCompiler patchCompiler = new PatchCompiler();

        [Fact]
        public void TestCompilePatch__Edit()
        {
            ProtoPatch protoPatch = new ProtoPatch(
                UrlBuilder.CreateConfig("ghi/jkl", new TestConfigNode("@NODE")
                {
                    { "@bar", "bleh" },
                }),
                Command.Edit,
                "NODE",
                "foo",
                null,
                "#bar",
                Substitute.For<IPassSpecifier>()
            );

            EditPatch patch = Assert.IsType<EditPatch>(patchCompiler.CompilePatch(protoPatch));

            Assert.Same(protoPatch.urlConfig, patch.UrlConfig);
            AssertNodeMatcher(patch.NodeMatcher);

            UrlDir.UrlConfig urlConfig = UrlBuilder.CreateConfig(new TestConfigNode("NODE")
            {
                { "name", "foo" },
                { "bar", "baz" },
            }, file);

            patch.Apply(file, progress, logger);

            AssertNoErrors();

            progress.Received().ApplyingUpdate(urlConfig, protoPatch.urlConfig);

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
            ProtoPatch protoPatch = new ProtoPatch(
                UrlBuilder.CreateConfig("ghi/jkl", new TestConfigNode("+NODE")
                {
                    { "@name", "boo" },
                    { "@bar", "bleh" },
                }),
                Command.Copy,
                "NODE",
                "foo",
                null,
                "#bar",
                Substitute.For<IPassSpecifier>()
            );

            CopyPatch patch = Assert.IsType<CopyPatch>(patchCompiler.CompilePatch(protoPatch));

            Assert.Same(protoPatch.urlConfig, patch.UrlConfig);
            AssertNodeMatcher(patch.NodeMatcher);

            UrlDir.UrlConfig urlConfig = UrlBuilder.CreateConfig(new TestConfigNode("NODE")
            {
                { "name", "foo" },
                { "bar", "baz" },
            }, file);

            patch.Apply(file, progress, logger);

            AssertNoErrors();

            progress.Received().ApplyingCopy(urlConfig, protoPatch.urlConfig);

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
            ProtoPatch protoPatch = new ProtoPatch(
                UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode("-NODE")),
                Command.Delete,
                "NODE",
                "foo",
                null,
                "#bar",
                Substitute.For<IPassSpecifier>()
            );

            DeletePatch patch = Assert.IsType<DeletePatch>(patchCompiler.CompilePatch(protoPatch));

            Assert.Same(protoPatch.urlConfig, patch.UrlConfig);
            AssertNodeMatcher(patch.NodeMatcher);

            UrlDir.UrlConfig urlConfig = UrlBuilder.CreateConfig(new TestConfigNode("NODE")
            {
                { "name", "foo" },
                { "bar", "baz" },
            }, file);

            patch.Apply(file, progress, logger);

            AssertNoErrors();

            progress.Received().ApplyingDelete(urlConfig, protoPatch.urlConfig);

            Assert.Equal(0, file.configs.Count);
        }

        [Fact]
        public void TestCompilePatch__NullProtoPatch()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                patchCompiler.CompilePatch(null);
            });

            Assert.Equal("protoPatch", ex.ParamName);
        }

        [Fact]
        public void TestCompilePatch__InvalidCommand__Replace()
        {
            ProtoPatch protoPatch = new ProtoPatch(
                UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode()),
                Command.Replace,
                "NODE",
                "foo",
                null,
                "#bar",
                Substitute.For<IPassSpecifier>()
            );

            ArgumentException ex = Assert.Throws<ArgumentException>(delegate
            {
                patchCompiler.CompilePatch(protoPatch);
            });

            Assert.Equal("protoPatch", ex.ParamName);
            Assert.Contains("invalid command for a root node: Replace", ex.Message);
        }

        [Fact]
        public void TestCompilePatch__InvalidCommand__Create()
        {
            ProtoPatch protoPatch = new ProtoPatch(
                UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode()),
                Command.Create,
                "NODE",
                "foo",
                null,
                "#bar",
                Substitute.For<IPassSpecifier>()
            );

            ArgumentException ex = Assert.Throws<ArgumentException>(delegate
            {
                patchCompiler.CompilePatch(protoPatch);
            });

            Assert.Equal("protoPatch", ex.ParamName);
            Assert.Contains("invalid command for a root node: Create", ex.Message);
        }

        [Fact]
        public void TestCompilePatch__InvalidCommand__Rename()
        {
            ProtoPatch protoPatch = new ProtoPatch(
                UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode()),
                Command.Rename,
                "NODE",
                "foo",
                null,
                "#bar",
                Substitute.For<IPassSpecifier>()
            );

            ArgumentException ex = Assert.Throws<ArgumentException>(delegate
            {
                patchCompiler.CompilePatch(protoPatch);
            });

            Assert.Equal("protoPatch", ex.ParamName);
            Assert.Contains("invalid command for a root node: Rename", ex.Message);
        }

        [Fact]
        public void TestCompilePatch__InvalidCommand__Paste()
        {
            ProtoPatch protoPatch = new ProtoPatch(
                UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode()),
                Command.Paste,
                "NODE",
                "foo",
                null,
                "#bar",
                Substitute.For<IPassSpecifier>()
            );

            ArgumentException ex = Assert.Throws<ArgumentException>(delegate
            {
                patchCompiler.CompilePatch(protoPatch);
            });

            Assert.Equal("protoPatch", ex.ParamName);
            Assert.Contains("invalid command for a root node: Paste", ex.Message);
        }

        [Fact]
        public void TestCompilePatch__InvalidCommand__Special()
        {
            ProtoPatch protoPatch = new ProtoPatch(
                UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode()),
                Command.Special,
                "NODE",
                "foo",
                null,
                "#bar",
                Substitute.For<IPassSpecifier>()
            );

            ArgumentException ex = Assert.Throws<ArgumentException>(delegate
            {
                patchCompiler.CompilePatch(protoPatch);
            });

            Assert.Equal("protoPatch", ex.ParamName);
            Assert.Contains("invalid command for a root node: Special", ex.Message);
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
