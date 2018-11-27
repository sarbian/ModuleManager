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
    public class CopyPatchTest
    {
        [Fact]
        public void TestConstructor__urlConfigNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new CopyPatch(null, Substitute.For<INodeMatcher>(), Substitute.For<IPassSpecifier>());
            });

            Assert.Equal("urlConfig", ex.ParamName);
        }

        [Fact]
        public void TestConstructor__nodeMatcherNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new CopyPatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), null, Substitute.For<IPassSpecifier>());
            });

            Assert.Equal("nodeMatcher", ex.ParamName);
        }

        [Fact]
        public void TestConstructor__passSpecifierNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new CopyPatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), Substitute.For<INodeMatcher>(), null);
            });

            Assert.Equal("passSpecifier", ex.ParamName);
        }

        [Fact]
        public void TestUrlConfig()
        {
            UrlDir.UrlConfig urlConfig = UrlBuilder.CreateConfig("abc/def", new ConfigNode());
            CopyPatch patch = new CopyPatch(urlConfig, Substitute.For<INodeMatcher>(), Substitute.For<IPassSpecifier>());

            Assert.Same(urlConfig, patch.UrlConfig);
        }

        [Fact]
        public void TestNodeMatcher()
        {
            INodeMatcher nodeMatcher = Substitute.For<INodeMatcher>();
            CopyPatch patch = new CopyPatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), nodeMatcher, Substitute.For<IPassSpecifier>());

            Assert.Same(nodeMatcher, patch.NodeMatcher);
        }

        [Fact]
        public void TestPassSpecifier()
        {
            IPassSpecifier passSpecifier = Substitute.For<IPassSpecifier>();
            CopyPatch patch = new CopyPatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), Substitute.For<INodeMatcher>(), passSpecifier);

            Assert.Same(passSpecifier, patch.PassSpecifier);
        }

        [Fact]
        public void TestCountsAsPatch()
        {
            CopyPatch patch = new CopyPatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), Substitute.For<INodeMatcher>(), Substitute.For<IPassSpecifier>());
            Assert.True(patch.CountsAsPatch);
        }

        [Fact]
        public void TestApply()
        {
            UrlDir.UrlFile file = UrlBuilder.CreateFile("abc/def.cfg");

            ConfigNode config1 = new TestConfigNode("NODE")
            {
                { "foo", "bar" },
            };

            ConfigNode config2 = new TestConfigNode("NODE")
            {
                { "foo", "bar" },
            };

            ConfigNode config3 = new ConfigNode("NODE");
            ConfigNode config4 = new ConfigNode("NODE");

            INodeMatcher nodeMatcher = Substitute.For<INodeMatcher>();

            nodeMatcher.IsMatch(config1).Returns(false);
            nodeMatcher.IsMatch(config2).Returns(true);
            nodeMatcher.IsMatch(config3).Returns(false);
            nodeMatcher.IsMatch(config4).Returns(true);

            CopyPatch patch = new CopyPatch(UrlBuilder.CreateConfig("ghi/jkl", new TestConfigNode("@NODE")
            {
                { "@foo", "baz" },
                { "pqr", "stw" },
            }), nodeMatcher, Substitute.For<IPassSpecifier>());

            IProtoUrlConfig protoUrlConfig1 = Substitute.For<IProtoUrlConfig>();
            IProtoUrlConfig protoUrlConfig2 = Substitute.For<IProtoUrlConfig>();
            IProtoUrlConfig protoUrlConfig3 = Substitute.For<IProtoUrlConfig>();
            IProtoUrlConfig protoUrlConfig4 = Substitute.For<IProtoUrlConfig>();

            protoUrlConfig1.Node.Returns(config1);
            protoUrlConfig2.Node.Returns(config2);
            protoUrlConfig3.Node.Returns(config3);
            protoUrlConfig4.Node.Returns(config4);

            protoUrlConfig1.UrlFile.Returns(file);
            protoUrlConfig2.UrlFile.Returns(file);
            protoUrlConfig3.UrlFile.Returns(file);
            protoUrlConfig4.UrlFile.Returns(file);

            LinkedList<IProtoUrlConfig> configs = new LinkedList<IProtoUrlConfig>();
            configs.AddLast(protoUrlConfig1);
            configs.AddLast(protoUrlConfig2);
            configs.AddLast(protoUrlConfig3);
            configs.AddLast(protoUrlConfig4);

            IPatchProgress progress = Substitute.For<IPatchProgress>();
            IBasicLogger logger = Substitute.For<IBasicLogger>();

            patch.Apply(configs, progress, logger);

            IProtoUrlConfig[] newConfigs = configs.ToArray();

            Assert.Equal(6, newConfigs.Length);

            Assert.Same(protoUrlConfig1, newConfigs[0]);
            AssertNodesEqual(new TestConfigNode("NODE")
            {
                { "foo", "bar" },
            }, newConfigs[0].Node);

            Assert.Same(protoUrlConfig2, newConfigs[1]);
            AssertNodesEqual(new TestConfigNode("NODE")
            {
                { "foo", "bar" },
            }, newConfigs[1].Node);

            AssertNodesEqual(new TestConfigNode("NODE")
            {
                { "foo", "baz" },
                { "pqr", "stw" },
            }, newConfigs[2].Node);
            Assert.Same(file, newConfigs[2].UrlFile);

            Assert.Same(protoUrlConfig3, newConfigs[3]);
            AssertNodesEqual(new ConfigNode("NODE"), newConfigs[3].Node);

            Assert.Same(protoUrlConfig4, newConfigs[4]);
            AssertNodesEqual(new ConfigNode("NODE"), newConfigs[4].Node);
            
            AssertNodesEqual(new TestConfigNode("NODE")
            {
                { "pqr", "stw" },
            }, newConfigs[5].Node);
            Assert.Same(file, newConfigs[5].UrlFile);

            Received.InOrder(delegate
            {
                progress.ApplyingCopy(protoUrlConfig2, patch.UrlConfig);
                progress.ApplyingCopy(protoUrlConfig4, patch.UrlConfig);
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

            ConfigNode config = new TestConfigNode("NODE")
            {
                { "name", "000" },
                { "foo", "bar" },
            };

            INodeMatcher nodeMatcher = Substitute.For<INodeMatcher>();

            nodeMatcher.IsMatch(config).Returns(true);

            CopyPatch patch = new CopyPatch(UrlBuilder.CreateConfig("ghi/jkl", new TestConfigNode("@NODE")
            {
                { "@name", "001" },
                { "@foo", "baz" },
                { "pqr", "stw" },
            }), nodeMatcher, Substitute.For<IPassSpecifier>());

            IProtoUrlConfig protoConfig = Substitute.For<IProtoUrlConfig>();
            protoConfig.Node.Returns(config);
            protoConfig.UrlFile.Returns(file);

            LinkedList<IProtoUrlConfig> configs = new LinkedList<IProtoUrlConfig>();
            configs.AddLast(protoConfig);

            IPatchProgress progress = Substitute.For<IPatchProgress>();
            IBasicLogger logger = Substitute.For<IBasicLogger>();

            patch.Apply(configs, progress, logger);

            IProtoUrlConfig[] newConfigs = configs.ToArray();

            Assert.Equal(2, newConfigs.Length);

            Assert.Same(protoConfig, newConfigs[0]);
            AssertNodesEqual(new TestConfigNode("NODE")
            {
                { "name", "000" },
                { "foo", "bar" },
            }, newConfigs[0].Node);
            
            AssertNodesEqual(new TestConfigNode("NODE")
            {
                { "name", "001" },
                { "foo", "baz" },
                { "pqr", "stw" },
            }, newConfigs[1].Node);
            Assert.Same(file, newConfigs[1].UrlFile);
            
            progress.Received().ApplyingCopy(protoConfig, patch.UrlConfig);

            progress.DidNotReceiveWithAnyArgs().ApplyingUpdate(null, null);
            progress.DidNotReceiveWithAnyArgs().ApplyingDelete(null, null);

            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);
        }

        [Fact]
        public void TestApply__NameNotChanged()
        {
            ConfigNode config = new TestConfigNode("NODE")
            {
                { "name", "000" },
                { "foo", "bar" },
            };

            INodeMatcher nodeMatcher = Substitute.For<INodeMatcher>();

            nodeMatcher.IsMatch(config).Returns(true);

            CopyPatch patch = new CopyPatch(UrlBuilder.CreateConfig("ghi/jkl", new TestConfigNode("+NODE")
            {
                { "@foo", "baz" },
                { "pqr", "stw" },
            }), nodeMatcher, Substitute.For<IPassSpecifier>());

            IProtoUrlConfig protoConfig = Substitute.For<IProtoUrlConfig>();
            protoConfig.Node.Returns(config);
            protoConfig.FullUrl.Returns("abc/def.cfg/NODE");

            LinkedList<IProtoUrlConfig> configs = new LinkedList<IProtoUrlConfig>();
            configs.AddLast(protoConfig);

            IPatchProgress progress = Substitute.For<IPatchProgress>();
            IBasicLogger logger = Substitute.For<IBasicLogger>();

            patch.Apply(configs, progress, logger);

            Assert.Single(configs);

            Assert.Same(protoConfig, configs.First.Value);
            AssertNodesEqual(new TestConfigNode("NODE")
            {
                { "name", "000" },
                { "foo", "bar" },
            }, configs.First.Value.Node);

            progress.Received().Error(patch.UrlConfig, "Error - when applying copy ghi/jkl/+NODE to abc/def.cfg/NODE - the copy needs to have a different name than the parent (use @name = xxx)");

            progress.DidNotReceiveWithAnyArgs().ApplyingUpdate(null, null);
            progress.DidNotReceiveWithAnyArgs().ApplyingCopy(null, null);
            progress.DidNotReceiveWithAnyArgs().ApplyingDelete(null, null);
            
            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);
        }

        [Fact]
        public void TestApply__DatabaseConfigsNullNull()
        {
            CopyPatch patch = new CopyPatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), Substitute.For<INodeMatcher>(), Substitute.For<IPassSpecifier>());
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                patch.Apply(null, Substitute.For<IPatchProgress>(), Substitute.For<IBasicLogger>());
            });

            Assert.Equal("databaseConfigs", ex.ParamName);
        }

        [Fact]
        public void TestApply__ProgressNull()
        {
            CopyPatch patch = new CopyPatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), Substitute.For<INodeMatcher>(), Substitute.For<IPassSpecifier>());
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                patch.Apply(new LinkedList<IProtoUrlConfig>(), null, Substitute.For<IBasicLogger>());
            });

            Assert.Equal("progress", ex.ParamName);
        }

        [Fact]
        public void TestApply__LoggerNull()
        {
            CopyPatch patch = new CopyPatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), Substitute.For<INodeMatcher>(), Substitute.For<IPassSpecifier>());
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                patch.Apply(new LinkedList<IProtoUrlConfig>(), Substitute.For<IPatchProgress>(), null);
            });

            Assert.Equal("logger", ex.ParamName);
        }

        private void AssertNodesEqual(ConfigNode expected, ConfigNode actual)
        {
            Assert.Equal(expected.ToString(), actual.ToString());
        }
    }
}
