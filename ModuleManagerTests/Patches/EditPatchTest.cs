using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using NSubstitute;
using UnityEngine;
using TestUtils;
using ModuleManager;
using ModuleManager.Logging;
using ModuleManager.Patches;
using ModuleManager.Patches.PassSpecifiers;
using ModuleManager.Progress;

namespace ModuleManagerTests.Patches
{
    public class EditPatchTest
    {
        [Fact]
        public void TestConstructor__urlConfigNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new EditPatch(null, Substitute.For<INodeMatcher>(), Substitute.For<IPassSpecifier>());
            });

            Assert.Equal("urlConfig", ex.ParamName);
        }

        [Fact]
        public void TestConstructor__nodeMatcherNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new EditPatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), null, Substitute.For<IPassSpecifier>());
            });

            Assert.Equal("nodeMatcher", ex.ParamName);
        }

        [Fact]
        public void TestConstructor__passSpecifierNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new EditPatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), Substitute.For<INodeMatcher>(), null);
            });

            Assert.Equal("passSpecifier", ex.ParamName);
        }

        [Fact]
        public void TestUrlConfig()
        {
            UrlDir.UrlConfig urlConfig = UrlBuilder.CreateConfig("abc/def", new ConfigNode());
            EditPatch patch = new EditPatch(urlConfig, Substitute.For<INodeMatcher>(), Substitute.For<IPassSpecifier>());

            Assert.Same(urlConfig, patch.UrlConfig);
        }

        [Fact]
        public void TestNodeMatcher()
        {
            INodeMatcher nodeMatcher = Substitute.For<INodeMatcher>();
            EditPatch patch = new EditPatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), nodeMatcher, Substitute.For<IPassSpecifier>());

            Assert.Same(nodeMatcher, patch.NodeMatcher);
        }

        [Fact]
        public void TestPassSpecifier()
        {
            IPassSpecifier passSpecifier = Substitute.For<IPassSpecifier>();
            EditPatch patch = new EditPatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), Substitute.For<INodeMatcher>(), passSpecifier);

            Assert.Same(passSpecifier, patch.PassSpecifier);
        }

        [Fact]
        public void TestCountsAsPatch()
        {
            EditPatch patch = new EditPatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), Substitute.For<INodeMatcher>(), Substitute.For<IPassSpecifier>());
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

            EditPatch patch = new EditPatch(UrlBuilder.CreateConfig("ghi/jkl", new TestConfigNode("@NODE")
            {
                { "@foo", "baz" },
                { "pqr", "stw" },
            }), nodeMatcher, Substitute.For<IPassSpecifier>());

            IProtoUrlConfig urlConfig1 = Substitute.For<IProtoUrlConfig>();
            IProtoUrlConfig urlConfig2 = Substitute.For<IProtoUrlConfig>();
            IProtoUrlConfig urlConfig3 = Substitute.For<IProtoUrlConfig>();
            IProtoUrlConfig urlConfig4 = Substitute.For<IProtoUrlConfig>();

            urlConfig1.Node.Returns(config1);
            urlConfig2.Node.Returns(config2);
            urlConfig3.Node.Returns(config3);
            urlConfig4.Node.Returns(config4);

            urlConfig1.UrlFile.Returns(file);
            urlConfig2.UrlFile.Returns(file);
            urlConfig3.UrlFile.Returns(file);
            urlConfig4.UrlFile.Returns(file);

            LinkedList<IProtoUrlConfig> configs = new LinkedList<IProtoUrlConfig>();
            configs.AddLast(urlConfig1);
            configs.AddLast(urlConfig2);
            configs.AddLast(urlConfig3);
            configs.AddLast(urlConfig4);

            IPatchProgress progress = Substitute.For<IPatchProgress>();
            IBasicLogger logger = Substitute.For<IBasicLogger>();

            patch.Apply(configs, progress, logger);

            IProtoUrlConfig[] newConfigs = configs.ToArray();

            Assert.Equal(4, newConfigs.Length);

            Assert.Same(urlConfig1, newConfigs[0]);
            AssertNodesEqual(new TestConfigNode("NODE")
            {
                { "foo", "bar" },
            }, newConfigs[0].Node);

            AssertNodesEqual(new TestConfigNode("NODE")
            {
                { "foo", "baz" },
                { "pqr", "stw" },
            }, newConfigs[1].Node);
            Assert.Same(file, newConfigs[1].UrlFile);

            Assert.Same(urlConfig3, newConfigs[2]);
            AssertNodesEqual(new ConfigNode("NODE"), newConfigs[2].Node);

            AssertNodesEqual(new TestConfigNode("NODE")
            {
                { "pqr", "stw" },
            }, newConfigs[3].Node);
            Assert.Same(file, newConfigs[3].UrlFile);

            Received.InOrder(delegate
            {
                progress.ApplyingUpdate(urlConfig2, patch.UrlConfig);
                progress.ApplyingUpdate(urlConfig4, patch.UrlConfig);
            });

            progress.DidNotReceiveWithAnyArgs().ApplyingCopy(null, null);
            progress.DidNotReceiveWithAnyArgs().ApplyingDelete(null, null);

            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);
        }

        [Fact]
        public void TestApply__Loop()
        {
            UrlDir.UrlFile file = UrlBuilder.CreateFile("abc/def.cfg");

            ConfigNode config = new TestConfigNode("NODE")
            {
                { "name", "000" },
                { "aaa", "1" },
            };
            
            INodeMatcher nodeMatcher = Substitute.For<INodeMatcher>();

            nodeMatcher.IsMatch(Arg.Is<ConfigNode>(node => int.Parse(node.GetValue("aaa")) < 10)).Returns(true);

            EditPatch patch = new EditPatch(UrlBuilder.CreateConfig("ghi/jkl", new TestConfigNode("@NODE")
            {
                { "@aaa *", "2" },
                { "bbb", "002" },
                new ConfigNode("MM_PATCH_LOOP"),
            }), nodeMatcher, Substitute.For<IPassSpecifier>());

            IProtoUrlConfig urlConfig = Substitute.For<IProtoUrlConfig>();
            urlConfig.Node.Returns(config);
            urlConfig.UrlFile.Returns(file);
            urlConfig.FullUrl.Returns("abc/def.cfg/NODE");

            LinkedList<IProtoUrlConfig> configs = new LinkedList<IProtoUrlConfig>();
            configs.AddLast(urlConfig);

            IPatchProgress progress = Substitute.For<IPatchProgress>();
            IBasicLogger logger = Substitute.For<IBasicLogger>();

            List<IProtoUrlConfig> modifiedUrlConfigs = new List<IProtoUrlConfig>();
            progress.ApplyingUpdate(Arg.Do<IProtoUrlConfig>(url => modifiedUrlConfigs.Add(url)), patch.UrlConfig);

            patch.Apply(configs, progress, logger);

            Assert.Single(configs);
            AssertNodesEqual(new TestConfigNode("NODE")
            {
                { "name", "000" },
                { "aaa", "16" },
                { "bbb", "002" },
                { "bbb", "002" },
                { "bbb", "002" },
                { "bbb", "002" },
            }, configs.First.Value.Node);
            Assert.Same(file, configs.First.Value.UrlFile);

            Assert.Same(urlConfig, modifiedUrlConfigs[0]);
            Assert.NotSame(urlConfig, modifiedUrlConfigs[1]);
            Assert.NotSame(urlConfig, modifiedUrlConfigs[2]);
            Assert.NotSame(urlConfig, modifiedUrlConfigs[3]);

            Received.InOrder(delegate
            {
                logger.AssertInfo("Looping on ghi/jkl/@NODE to abc/def.cfg/NODE");
                progress.ApplyingUpdate(urlConfig, patch.UrlConfig);
                progress.ApplyingUpdate(modifiedUrlConfigs[1], patch.UrlConfig);
                progress.ApplyingUpdate(modifiedUrlConfigs[2], patch.UrlConfig);
                progress.ApplyingUpdate(modifiedUrlConfigs[3], patch.UrlConfig);
            });

            progress.DidNotReceiveWithAnyArgs().ApplyingCopy(null, null);
            progress.DidNotReceiveWithAnyArgs().ApplyingDelete(null, null);

            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);
        }

        [Fact]
        public void TestApply__DatabaseConfigsNull()
        {
            EditPatch patch = new EditPatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), Substitute.For<INodeMatcher>(), Substitute.For<IPassSpecifier>());
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                patch.Apply(null, Substitute.For<IPatchProgress>(), Substitute.For<IBasicLogger>());
            });

            Assert.Equal("databaseConfigs", ex.ParamName);
        }

        [Fact]
        public void TestApply__ProgressNull()
        {
            EditPatch patch = new EditPatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), Substitute.For<INodeMatcher>(), Substitute.For<IPassSpecifier>());
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                patch.Apply(new LinkedList<IProtoUrlConfig>(), null, Substitute.For<IBasicLogger>());
            });

            Assert.Equal("progress", ex.ParamName);
        }

        [Fact]
        public void TestApply__LoggerNull()
        {
            EditPatch patch = new EditPatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), Substitute.For<INodeMatcher>(), Substitute.For<IPassSpecifier>());
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
