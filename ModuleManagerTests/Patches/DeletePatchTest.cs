using System;
using System.Collections.Generic;
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
    public class DeletePatchTest
    {
        [Fact]
        public void TestConstructor__urlConfigNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new DeletePatch(null, Substitute.For<INodeMatcher>(), Substitute.For<IPassSpecifier>());
            });

            Assert.Equal("urlConfig", ex.ParamName);
        }

        [Fact]
        public void TestConstructor__nodeMatcherNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new DeletePatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), null, Substitute.For<IPassSpecifier>());
            });

            Assert.Equal("nodeMatcher", ex.ParamName);
        }

        [Fact]
        public void TestConstructor__passSpecifierNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new DeletePatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), Substitute.For<INodeMatcher>(), null);
            });

            Assert.Equal("passSpecifier", ex.ParamName);
        }

        [Fact]
        public void TestUrlConfig()
        {
            UrlDir.UrlConfig urlConfig = UrlBuilder.CreateConfig("abc/def", new ConfigNode());
            DeletePatch patch = new DeletePatch(urlConfig, Substitute.For<INodeMatcher>(), Substitute.For<IPassSpecifier>());

            Assert.Same(urlConfig, patch.UrlConfig);
        }

        [Fact]
        public void TestNodeMatcher()
        {
            INodeMatcher nodeMatcher = Substitute.For<INodeMatcher>();
            DeletePatch patch = new DeletePatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), nodeMatcher, Substitute.For<IPassSpecifier>());

            Assert.Same(nodeMatcher, patch.NodeMatcher);
        }

        [Fact]
        public void TestPassSpecifier()
        {
            IPassSpecifier passSpecifier = Substitute.For<IPassSpecifier>();
            DeletePatch patch = new DeletePatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), Substitute.For<INodeMatcher>(), passSpecifier);

            Assert.Same(passSpecifier, patch.PassSpecifier);
        }

        [Fact]
        public void TestCountsAsPatch()
        {
            DeletePatch patch = new DeletePatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), Substitute.For<INodeMatcher>(), Substitute.For<IPassSpecifier>());
            Assert.True(patch.CountsAsPatch);
        }

        [Fact]
        public void TestApply()
        {
            ConfigNode config1 = new ConfigNode("NODE");
            ConfigNode config2 = new ConfigNode("NODE");
            ConfigNode config3 = new ConfigNode("NODE");
            ConfigNode config4 = new ConfigNode("NODE");

            INodeMatcher nodeMatcher = Substitute.For<INodeMatcher>();

            nodeMatcher.IsMatch(config1).Returns(false);
            nodeMatcher.IsMatch(config2).Returns(true);
            nodeMatcher.IsMatch(config3).Returns(false);
            nodeMatcher.IsMatch(config4).Returns(true);

            DeletePatch patch = new DeletePatch(UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode("!NODE")), nodeMatcher, Substitute.For<IPassSpecifier>());

            IProtoUrlConfig urlConfig1 = Substitute.For<IProtoUrlConfig>();
            IProtoUrlConfig urlConfig2 = Substitute.For<IProtoUrlConfig>();
            IProtoUrlConfig urlConfig3 = Substitute.For<IProtoUrlConfig>();
            IProtoUrlConfig urlConfig4 = Substitute.For<IProtoUrlConfig>();

            urlConfig1.Node.Returns(config1);
            urlConfig2.Node.Returns(config2);
            urlConfig3.Node.Returns(config3);
            urlConfig4.Node.Returns(config4);

            LinkedList<IProtoUrlConfig> configs = new LinkedList<IProtoUrlConfig>();
            configs.AddLast(urlConfig1);
            configs.AddLast(urlConfig2);
            configs.AddLast(urlConfig3);
            configs.AddLast(urlConfig4);

            IPatchProgress progress = Substitute.For<IPatchProgress>();
            IBasicLogger logger = Substitute.For<IBasicLogger>();

            patch.Apply(configs, progress, logger);

            Assert.Equal(new[] { urlConfig1, urlConfig3 }, configs);

            Received.InOrder(delegate
            {
                progress.ApplyingDelete(urlConfig2, patch.UrlConfig);
                progress.ApplyingDelete(urlConfig4, patch.UrlConfig);
            });

            progress.DidNotReceiveWithAnyArgs().ApplyingUpdate(null, null);
            progress.DidNotReceiveWithAnyArgs().ApplyingCopy(null, null);

            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);
        }

        [Fact]
        public void TestApply__DatabaseConfigsNull()
        {
            DeletePatch patch = new DeletePatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), Substitute.For<INodeMatcher>(), Substitute.For<IPassSpecifier>());
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                patch.Apply(null, Substitute.For<IPatchProgress>(), Substitute.For<IBasicLogger>());
            });

            Assert.Equal("databaseConfigs", ex.ParamName);
        }

        [Fact]
        public void TestApply__ProgressNull()
        {
            DeletePatch patch = new DeletePatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), Substitute.For<INodeMatcher>(), Substitute.For<IPassSpecifier>());
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                patch.Apply(new LinkedList<IProtoUrlConfig>(), null, Substitute.For<IBasicLogger>());
            });

            Assert.Equal("progress", ex.ParamName);
        }

        [Fact]
        public void TestApply__LoggerNull()
        {
            DeletePatch patch = new DeletePatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), Substitute.For<INodeMatcher>(), Substitute.For<IPassSpecifier>());
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                patch.Apply(new LinkedList<IProtoUrlConfig>(), Substitute.For<IPatchProgress>(), null);
            });

            Assert.Equal("logger", ex.ParamName);
        }
    }
}
