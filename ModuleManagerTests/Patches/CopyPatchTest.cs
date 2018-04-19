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
    public class CopyPatchTest
    {
        [Fact]
        public void TestConstructor__urlConfigNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new CopyPatch(null, Substitute.For<INodeMatcher>());
            });

            Assert.Equal("urlConfig", ex.ParamName);
        }

        [Fact]
        public void TestConstructor__nodeMatcherNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new CopyPatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), null);
            });

            Assert.Equal("nodeMatcher", ex.ParamName);
        }

        [Fact]
        public void TestUrlConfig()
        {
            UrlDir.UrlConfig urlConfig = UrlBuilder.CreateConfig("abc/def", new ConfigNode());
            CopyPatch patch = new CopyPatch(urlConfig, Substitute.For<INodeMatcher>());

            Assert.Same(urlConfig, patch.UrlConfig);
        }

        [Fact]
        public void TestNodeMatcher()
        {
            INodeMatcher nodeMatcher = Substitute.For<INodeMatcher>();
            CopyPatch patch = new CopyPatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), nodeMatcher);

            Assert.Same(nodeMatcher, patch.NodeMatcher);
        }

        [Fact]
        public void TestApply()
        {
            UrlDir.UrlFile file = UrlBuilder.CreateFile("abc/def.cfg");

            UrlDir.UrlConfig urlConfig1 = UrlBuilder.CreateConfig(new TestConfigNode("NODE")
            {
                { "foo", "bar" },
            }, file);

            UrlDir.UrlConfig urlConfig2 = UrlBuilder.CreateConfig(new TestConfigNode("NODE")
            {
                { "foo", "bar" },
            }, file);

            UrlDir.UrlConfig urlConfig3 = UrlBuilder.CreateConfig(new ConfigNode("NODE"), file);
            UrlDir.UrlConfig urlConfig4 = UrlBuilder.CreateConfig(new ConfigNode("NODE"), file);

            INodeMatcher nodeMatcher = Substitute.For<INodeMatcher>();

            nodeMatcher.IsMatch(urlConfig1.config).Returns(false);
            nodeMatcher.IsMatch(urlConfig2.config).Returns(true);
            nodeMatcher.IsMatch(urlConfig3.config).Returns(false);
            nodeMatcher.IsMatch(urlConfig4.config).Returns(true);

            CopyPatch patch = new CopyPatch(UrlBuilder.CreateConfig("ghi/jkl", new TestConfigNode("@NODE")
            {
                { "@foo", "baz" },
                { "pqr", "stw" },
            }), nodeMatcher);
            
            IPatchProgress progress = Substitute.For<IPatchProgress>();
            IBasicLogger logger = Substitute.For<IBasicLogger>();

            patch.Apply(file, progress, logger);

            Assert.Equal(6, file.configs.Count);

            Assert.Same(urlConfig1, file.configs[0]);
            AssertNodesEqual(new TestConfigNode("NODE")
            {
                { "foo", "bar" },
            }, file.configs[0].config);

            Assert.Same(urlConfig2, file.configs[1]);
            AssertNodesEqual(new TestConfigNode("NODE")
            {
                { "foo", "bar" },
            }, file.configs[1].config);

            Assert.Same(urlConfig3, file.configs[2]);
            AssertNodesEqual(new ConfigNode("NODE"), file.configs[2].config);

            Assert.Same(urlConfig4, file.configs[3]);
            AssertNodesEqual(new ConfigNode("NODE"), file.configs[3].config);
            
            AssertNodesEqual(new TestConfigNode("NODE")
            {
                { "foo", "baz" },
                { "pqr", "stw" },
            }, file.configs[4].config);
            
            AssertNodesEqual(new TestConfigNode("NODE")
            {
                { "pqr", "stw" },
            }, file.configs[5].config);

            Received.InOrder(delegate
            {
                progress.ApplyingCopy(urlConfig2, patch.UrlConfig);
                progress.ApplyingCopy(urlConfig4, patch.UrlConfig);
            });

            progress.DidNotReceiveWithAnyArgs().ApplyingUpdate(null, null);
            progress.DidNotReceiveWithAnyArgs().ApplyingDelete(null, null);

            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);
        }

        [Fact]
        public void TestApply__NameChanged()
        {
            UrlDir.UrlFile file = UrlBuilder.CreateFile("abc/def.cfg");

            UrlDir.UrlConfig urlConfig = UrlBuilder.CreateConfig(new TestConfigNode("NODE")
            {
                { "name", "000" },
                { "foo", "bar" },
            }, file);

            INodeMatcher nodeMatcher = Substitute.For<INodeMatcher>();

            nodeMatcher.IsMatch(urlConfig.config).Returns(true);

            CopyPatch patch = new CopyPatch(UrlBuilder.CreateConfig("ghi/jkl", new TestConfigNode("@NODE")
            {
                { "@name", "001" },
                { "@foo", "baz" },
                { "pqr", "stw" },
            }), nodeMatcher);

            IPatchProgress progress = Substitute.For<IPatchProgress>();
            IBasicLogger logger = Substitute.For<IBasicLogger>();

            patch.Apply(file, progress, logger);

            Assert.Equal(2, file.configs.Count);

            Assert.Same(urlConfig, file.configs[0]);
            AssertNodesEqual(new TestConfigNode("NODE")
            {
                { "name", "000" },
                { "foo", "bar" },
            }, file.configs[0].config);
            
            AssertNodesEqual(new TestConfigNode("NODE")
            {
                { "name", "001" },
                { "foo", "baz" },
                { "pqr", "stw" },
            }, file.configs[1].config);
            
            progress.Received().ApplyingCopy(urlConfig, patch.UrlConfig);

            progress.DidNotReceiveWithAnyArgs().ApplyingUpdate(null, null);
            progress.DidNotReceiveWithAnyArgs().ApplyingDelete(null, null);

            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);
        }

        [Fact]
        public void TestApply__NameNotChanged()
        {
            UrlDir.UrlFile file = UrlBuilder.CreateFile("abc/def.cfg");

            UrlDir.UrlConfig urlConfig = UrlBuilder.CreateConfig(new TestConfigNode("NODE")
            {
                { "name", "000" },
                { "foo", "bar" },
            }, file);

            INodeMatcher nodeMatcher = Substitute.For<INodeMatcher>();

            nodeMatcher.IsMatch(urlConfig.config).Returns(true);

            CopyPatch patch = new CopyPatch(UrlBuilder.CreateConfig("ghi/jkl", new TestConfigNode("+NODE")
            {
                { "@foo", "baz" },
                { "pqr", "stw" },
            }), nodeMatcher);

            IPatchProgress progress = Substitute.For<IPatchProgress>();
            IBasicLogger logger = Substitute.For<IBasicLogger>();

            patch.Apply(file, progress, logger);

            Assert.Equal(1, file.configs.Count);

            Assert.Same(urlConfig, file.configs[0]);
            AssertNodesEqual(new TestConfigNode("NODE")
            {
                { "name", "000" },
                { "foo", "bar" },
            }, file.configs[0].config);

            progress.Received().Error(patch.UrlConfig, "Error - when applying copy ghi/jkl/+NODE to abc/def/NODE - the copy needs to have a different name than the parent (use @name = xxx)");

            progress.DidNotReceiveWithAnyArgs().ApplyingUpdate(null, null);
            progress.DidNotReceiveWithAnyArgs().ApplyingCopy(null, null);
            progress.DidNotReceiveWithAnyArgs().ApplyingDelete(null, null);
            
            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);
        }

        [Fact]
        public void TestApply__FileNull()
        {
            CopyPatch patch = new CopyPatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), Substitute.For<INodeMatcher>());
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                patch.Apply(null, Substitute.For<IPatchProgress>(), Substitute.For<IBasicLogger>());
            });

            Assert.Equal("file", ex.ParamName);
        }

        [Fact]
        public void TestApply__ProgressNull()
        {
            CopyPatch patch = new CopyPatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), Substitute.For<INodeMatcher>());
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                patch.Apply(UrlBuilder.CreateFile("abc/def.cfg"), null, Substitute.For<IBasicLogger>());
            });

            Assert.Equal("progress", ex.ParamName);
        }

        [Fact]
        public void TestApply__LoggerNull()
        {
            CopyPatch patch = new CopyPatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), Substitute.For<INodeMatcher>());
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                patch.Apply(UrlBuilder.CreateFile("abc/def.cfg"), Substitute.For<IPatchProgress>(), null);
            });

            Assert.Equal("logger", ex.ParamName);
        }

        private void AssertNodesEqual(ConfigNode expected, ConfigNode actual)
        {
            Assert.Equal(expected.ToString(), actual.ToString());
        }
    }
}
