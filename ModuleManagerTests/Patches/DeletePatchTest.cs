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
    public class DeletePatchTest
    {
        [Fact]
        public void TestConstructor__urlConfigNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new DeletePatch(null, Substitute.For<INodeMatcher>());
            });

            Assert.Equal("urlConfig", ex.ParamName);
        }

        [Fact]
        public void TestConstructor__nodeMatcherNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new DeletePatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), null);
            });

            Assert.Equal("nodeMatcher", ex.ParamName);
        }

        [Fact]
        public void TestUrlConfig()
        {
            UrlDir.UrlConfig urlConfig = UrlBuilder.CreateConfig("abc/def", new ConfigNode());
            DeletePatch patch = new DeletePatch(urlConfig, Substitute.For<INodeMatcher>());

            Assert.Same(urlConfig, patch.UrlConfig);
        }

        [Fact]
        public void TestNodeMatcher()
        {
            INodeMatcher nodeMatcher = Substitute.For<INodeMatcher>();
            DeletePatch patch = new DeletePatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), nodeMatcher);

            Assert.Same(nodeMatcher, patch.NodeMatcher);
        }

        [Fact]
        public void TestApply()
        {
            UrlDir.UrlFile file = UrlBuilder.CreateFile("abc/def.cfg");

            UrlDir.UrlConfig urlConfig1 = UrlBuilder.CreateConfig(new ConfigNode("NODE"), file);
            UrlDir.UrlConfig urlConfig2 = UrlBuilder.CreateConfig(new ConfigNode("NODE"), file);
            UrlDir.UrlConfig urlConfig3 = UrlBuilder.CreateConfig(new ConfigNode("NODE"), file);
            UrlDir.UrlConfig urlConfig4 = UrlBuilder.CreateConfig(new ConfigNode("NODE"), file);

            INodeMatcher nodeMatcher = Substitute.For<INodeMatcher>();

            nodeMatcher.IsMatch(urlConfig1.config).Returns(false);
            nodeMatcher.IsMatch(urlConfig2.config).Returns(true);
            nodeMatcher.IsMatch(urlConfig3.config).Returns(false);
            nodeMatcher.IsMatch(urlConfig4.config).Returns(true);

            DeletePatch patch = new DeletePatch(UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode("!NODE")), nodeMatcher);

            IPatchProgress progress = Substitute.For<IPatchProgress>();
            IBasicLogger logger = Substitute.For<IBasicLogger>();

            patch.Apply(file, progress, logger);

            Assert.Equal(new[] { urlConfig1, urlConfig3 }, file.configs);

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
        public void TestApply__FileNull()
        {
            DeletePatch patch = new DeletePatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), Substitute.For<INodeMatcher>());
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                patch.Apply(null, Substitute.For<IPatchProgress>(), Substitute.For<IBasicLogger>());
            });

            Assert.Equal("file", ex.ParamName);
        }

        [Fact]
        public void TestApply__ProgressNull()
        {
            DeletePatch patch = new DeletePatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), Substitute.For<INodeMatcher>());
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                patch.Apply(UrlBuilder.CreateFile("abc/def.cfg"), null, Substitute.For<IBasicLogger>());
            });

            Assert.Equal("progress", ex.ParamName);
        }

        [Fact]
        public void TestApply__LoggerNull()
        {
            DeletePatch patch = new DeletePatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), Substitute.For<INodeMatcher>());
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                patch.Apply(UrlBuilder.CreateFile("abc/def.cfg"), Substitute.For<IPatchProgress>(), null);
            });

            Assert.Equal("logger", ex.ParamName);
        }
    }
}
