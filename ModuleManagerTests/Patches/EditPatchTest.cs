using System;
using System.Collections.Generic;
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

            EditPatch patch = new EditPatch(UrlBuilder.CreateConfig("ghi/jkl", new TestConfigNode("@NODE")
            {
                { "@foo", "baz" },
                { "pqr", "stw" },
            }), nodeMatcher, Substitute.For<IPassSpecifier>());
            
            IPatchProgress progress = Substitute.For<IPatchProgress>();
            IBasicLogger logger = Substitute.For<IBasicLogger>();

            patch.Apply(file, progress, logger);

            Assert.Equal(4, file.configs.Count);

            Assert.Same(urlConfig1, file.configs[0]);
            AssertNodesEqual(new TestConfigNode("NODE")
            {
                { "foo", "bar" },
            }, file.configs[0].config);

            Assert.NotSame(urlConfig2, file.configs[1]);
            AssertNodesEqual(new TestConfigNode("NODE")
            {
                { "foo", "baz" },
                { "pqr", "stw" },
            }, file.configs[1].config);

            Assert.Same(urlConfig3, file.configs[2]);
            AssertNodesEqual(new ConfigNode("NODE"), file.configs[2].config);

            Assert.NotSame(urlConfig4, file.configs[3]);
            AssertNodesEqual(new TestConfigNode("NODE")
            {
                { "pqr", "stw" },
            }, file.configs[3].config);

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

            UrlDir.UrlConfig urlConfig = UrlBuilder.CreateConfig(new TestConfigNode("NODE")
            {
                { "name", "000" },
                { "aaa", "1" },
            }, file);
            
            INodeMatcher nodeMatcher = Substitute.For<INodeMatcher>();

            nodeMatcher.IsMatch(Arg.Is<ConfigNode>(node => int.Parse(node.GetValue("aaa")) < 10)).Returns(true);

            EditPatch patch = new EditPatch(UrlBuilder.CreateConfig("ghi/jkl", new TestConfigNode("@NODE")
            {
                { "@aaa *", "2" },
                { "bbb", "002" },
                new ConfigNode("MM_PATCH_LOOP"),
            }), nodeMatcher, Substitute.For<IPassSpecifier>());

            IPatchProgress progress = Substitute.For<IPatchProgress>();
            IBasicLogger logger = Substitute.For<IBasicLogger>();

            List<UrlDir.UrlConfig> modifiedUrlConfigs = new List<UrlDir.UrlConfig>();
            progress.ApplyingUpdate(Arg.Do<UrlDir.UrlConfig>(url => modifiedUrlConfigs.Add(url)), patch.UrlConfig);

            patch.Apply(file, progress, logger);

            Assert.Single(file.configs);
            Assert.NotSame(urlConfig, file.configs[0]);
            AssertNodesEqual(new TestConfigNode("NODE")
            {
                { "name", "000" },
                { "aaa", "16" },
                { "bbb", "002" },
                { "bbb", "002" },
                { "bbb", "002" },
                { "bbb", "002" },
            }, file.configs[0].config);

            Assert.Same(urlConfig, modifiedUrlConfigs[0]);
            Assert.NotSame(urlConfig, modifiedUrlConfigs[1]);
            Assert.NotSame(urlConfig, modifiedUrlConfigs[2]);
            Assert.NotSame(urlConfig, modifiedUrlConfigs[3]);

            Received.InOrder(delegate
            {
                logger.Log(LogType.Log, "Looping on ghi/jkl/@NODE to abc/def/NODE");
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
        public void TestApply__FileNull()
        {
            EditPatch patch = new EditPatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), Substitute.For<INodeMatcher>(), Substitute.For<IPassSpecifier>());
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                patch.Apply(null, Substitute.For<IPatchProgress>(), Substitute.For<IBasicLogger>());
            });

            Assert.Equal("file", ex.ParamName);
        }

        [Fact]
        public void TestApply__ProgressNull()
        {
            EditPatch patch = new EditPatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), Substitute.For<INodeMatcher>(), Substitute.For<IPassSpecifier>());
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                patch.Apply(UrlBuilder.CreateFile("abc/def.cfg"), null, Substitute.For<IBasicLogger>());
            });

            Assert.Equal("progress", ex.ParamName);
        }

        [Fact]
        public void TestApply__LoggerNull()
        {
            EditPatch patch = new EditPatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), Substitute.For<INodeMatcher>(), Substitute.For<IPassSpecifier>());
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
