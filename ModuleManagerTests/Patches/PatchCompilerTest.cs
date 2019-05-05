using System;
using System.Collections.Generic;
using System.Linq;
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
        public void TestCompilePatch__Insert()
        {
            ProtoPatch protoPatch = new ProtoPatch(
                UrlBuilder.CreateConfig(new TestConfigNode("NODEE")
                {
                    { "name", "foo" },
                    { "bar", "bleh" },
                }, file),
                Command.Insert,
                "NODE",
                "foo",
                null,
                "#bar",
                Substitute.For<IPassSpecifier>()
            );

            InsertPatch patch = Assert.IsType<InsertPatch>(patchCompiler.CompilePatch(protoPatch));

            Assert.Same(protoPatch.urlConfig, patch.UrlConfig);

            LinkedList<IProtoUrlConfig> configs = new LinkedList<IProtoUrlConfig>();

            patch.Apply(configs, progress, logger);

            Assert.Single(configs);
            Assert.NotSame(protoPatch.urlConfig.config, configs.First.Value.Node);
            AssertNodesEqual(new TestConfigNode("NODE")
            {
                { "name", "foo" },
                { "bar", "bleh" },
            }, configs.First.Value.Node);
            Assert.Same(file, configs.First.Value.UrlFile);
        }

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

            ConfigNode config = new TestConfigNode("NODE")
            {
                { "name", "foo" },
                { "bar", "baz" },
            };

            IProtoUrlConfig urlConfig = Substitute.For<IProtoUrlConfig>();
            urlConfig.Node.Returns(config);
            urlConfig.UrlFile.Returns(file);

            LinkedList<IProtoUrlConfig> configs = new LinkedList<IProtoUrlConfig>();
            configs.AddLast(urlConfig);

            patch.Apply(configs, progress, logger);

            AssertNoErrors();

            progress.Received().ApplyingUpdate(urlConfig, protoPatch.urlConfig);

            Assert.Single(configs);
            Assert.NotSame(config, configs.First.Value.Node);
            AssertNodesEqual(new TestConfigNode("NODE")
            {
                { "name", "foo" },
                { "bar", "bleh" },
            }, configs.First.Value.Node);
            Assert.Same(file, configs.First.Value.UrlFile);
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

            ConfigNode config = new TestConfigNode("NODE")
            {
                { "name", "foo" },
                { "bar", "baz" },
            };

            IProtoUrlConfig urlConfig = Substitute.For<IProtoUrlConfig>();
            urlConfig.Node.Returns(config);
            urlConfig.UrlFile.Returns(file);

            LinkedList<IProtoUrlConfig> configs = new LinkedList<IProtoUrlConfig>();
            configs.AddLast(urlConfig);

            patch.Apply(configs, progress, logger);

            AssertNoErrors();

            progress.Received().ApplyingCopy(urlConfig, protoPatch.urlConfig);

            IProtoUrlConfig[] newConfigs = configs.ToArray();

            Assert.Equal(2, newConfigs.Length);
            Assert.Same(config, newConfigs[0].Node);
            AssertNodesEqual(new TestConfigNode("NODE")
            {
                { "name", "foo" },
                { "bar", "baz" },
            }, newConfigs[0].Node);
            AssertNodesEqual(new TestConfigNode("NODE")
            {
                { "name", "boo" },
                { "bar", "bleh" },
            }, newConfigs[1].Node);
            Assert.Same(file, newConfigs[1].UrlFile);
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

            IProtoUrlConfig urlConfig = Substitute.For<IProtoUrlConfig>();
            urlConfig.Node.Returns(new TestConfigNode("NODE")
            {
                { "name", "foo" },
                { "bar", "baz" },
            });

            LinkedList<IProtoUrlConfig> configs = new LinkedList<IProtoUrlConfig>();
            configs.AddLast(urlConfig);

            patch.Apply(configs, progress, logger);

            AssertNoErrors();

            progress.Received().ApplyingDelete(urlConfig, protoPatch.urlConfig);

            Assert.Empty(configs);
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
