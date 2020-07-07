using System;
using Xunit;
using NSubstitute;
using TestUtils;
using ModuleManager;
using ModuleManager.Logging;
using ModuleManager.Patches;
using ModuleManager.Patches.PassSpecifiers;
using ModuleManager.Progress;
using ModuleManager.Tags;

namespace ModuleManagerTests
{
    public class PatchExtractorTest
    {
        private readonly UrlDir root;
        private readonly UrlDir.UrlFile file;

        private readonly IPatchProgress progress;
        private readonly IBasicLogger logger;
        private readonly INeedsChecker needsChecker;
        private readonly ITagListParser tagListParser;
        private readonly IProtoPatchBuilder protoPatchBuilder;
        private readonly IPatchCompiler patchCompiler;
        private readonly PatchExtractor patchExtractor;

        public PatchExtractorTest()
        {
            root = UrlBuilder.CreateRoot();
            file = UrlBuilder.CreateFile("abc/def.cfg", root);
            
            progress = Substitute.For<IPatchProgress>();
            logger = Substitute.For<IBasicLogger>();
            needsChecker = Substitute.For<INeedsChecker>();
            tagListParser = Substitute.For<ITagListParser>();
            protoPatchBuilder = Substitute.For<IProtoPatchBuilder>();
            patchCompiler = Substitute.For<IPatchCompiler>();
            patchExtractor = new PatchExtractor(progress, logger, needsChecker, tagListParser, protoPatchBuilder, patchCompiler);
        }

        [Fact]
        public void TestConstructor__ProgressNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new PatchExtractor(null, logger, needsChecker, tagListParser, protoPatchBuilder, patchCompiler);
            });

            Assert.Equal("progress", ex.ParamName);
        }

        [Fact]
        public void TestConstructor__LoggerNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new PatchExtractor(progress, null, needsChecker, tagListParser, protoPatchBuilder, patchCompiler);
            });

            Assert.Equal("logger", ex.ParamName);
        }

        [Fact]
        public void TestConstructor__NeedsCheckerNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new PatchExtractor(progress, logger, null, tagListParser, protoPatchBuilder, patchCompiler);
            });

            Assert.Equal("needsChecker", ex.ParamName);
        }

        [Fact]
        public void TestConstructor__TagListParserNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new PatchExtractor(progress, logger, needsChecker, null, protoPatchBuilder, patchCompiler);
            });

            Assert.Equal("tagListParser", ex.ParamName);
        }

        [Fact]
        public void TestConstructor__ProtoPatchBuilderNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new PatchExtractor(progress, logger, needsChecker, tagListParser, null, patchCompiler);
            });

            Assert.Equal("protoPatchBuilder", ex.ParamName);
        }

        [Fact]
        public void TestConstructor__PatchCompilerNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new PatchExtractor(progress, logger, needsChecker, tagListParser, protoPatchBuilder, null);
            });

            Assert.Equal("patchCompiler", ex.ParamName);
        }

        [Fact]
        public void TestExtractPatch__ProtoPatchNull()
        {
            UrlDir.UrlConfig patchConfig = UrlBuilder.CreateConfig("abc/def", new ConfigNode("NODE"), root);

            protoPatchBuilder.Build(patchConfig, Command.Insert, Arg.Any<ITagList>()).Returns(null, new ProtoPatch[0]);

            Assert.Null(patchExtractor.ExtractPatch(patchConfig));

            needsChecker.DidNotReceiveWithAnyArgs().CheckNeedsExpression(null);

            AssertNoErrors();

            progress.DidNotReceive().PatchAdded();
            progress.DidNotReceiveWithAnyArgs().NeedsUnsatisfiedRoot(null);
        }

        [Fact]
        public void TestExtractPatch()
        {
            UrlDir.UrlConfig urlConfig = CreateConfig("@NODE_TYPE");

            ITagList tagList = Substitute.For<ITagList>();
            tagListParser.Parse("NODE_TYPE", urlConfig).Returns(tagList);

            IPassSpecifier passSpecifier = Substitute.For<IPassSpecifier>();
            ProtoPatch protoPatch = new ProtoPatch(
                urlConfig,
                Command.Edit,
                "NODE_TYPE",
                "nodeName",
                null,
                "has",
                passSpecifier
            );

            protoPatchBuilder.Build(urlConfig, Command.Edit, tagList).Returns(protoPatch);
            passSpecifier.CheckNeeds(needsChecker, progress).Returns(true);

            IPatch patch = Substitute.For<IPatch>();
            patchCompiler.CompilePatch(protoPatch).Returns(patch);

            Assert.Same(patch, patchExtractor.ExtractPatch(urlConfig));

            AssertNoErrors();

            needsChecker.Received().CheckNeedsRecursive(urlConfig.config, urlConfig);
            needsChecker.DidNotReceiveWithAnyArgs().CheckNeedsExpression(null);
            progress.DidNotReceiveWithAnyArgs().NeedsUnsatisfiedRoot(null);
        }

        [Fact]
        public void TestExtractPatch__Needs()
        {
            UrlDir.UrlConfig urlConfig = CreateConfig("@NODE_TYPE");

            ITagList tagList = Substitute.For<ITagList>();
            tagListParser.Parse("NODE_TYPE", urlConfig).Returns(tagList);

            IPassSpecifier passSpecifier = Substitute.For<IPassSpecifier>();
            ProtoPatch protoPatch = new ProtoPatch(
                urlConfig,
                Command.Edit,
                "NODE_TYPE",
                "nodeName",
                "needs",
                "has",
                passSpecifier
            );

            protoPatchBuilder.Build(urlConfig, Command.Edit, tagList).Returns(protoPatch);
            needsChecker.CheckNeedsExpression("needs").Returns(true);
            passSpecifier.CheckNeeds(needsChecker, progress).Returns(true);

            IPatch patch = Substitute.For<IPatch>();
            patchCompiler.CompilePatch(protoPatch).Returns(patch);

            Assert.Same(patch, patchExtractor.ExtractPatch(urlConfig));

            AssertNoErrors();

            needsChecker.Received().CheckNeedsRecursive(urlConfig.config, urlConfig);
            progress.DidNotReceiveWithAnyArgs().NeedsUnsatisfiedRoot(null);
        }

        [Fact]
        public void TestExtractPatch__NeedsUnsatisfied()
        {
            UrlDir.UrlConfig urlConfig = CreateConfig("@NODE_TYPE");

            ITagList tagList = Substitute.For<ITagList>();
            tagListParser.Parse("NODE_TYPE", urlConfig).Returns(tagList);

            IPassSpecifier passSpecifier = Substitute.For<IPassSpecifier>();
            ProtoPatch protoPatch = new ProtoPatch(
                urlConfig,
                Command.Edit,
                "NODE_TYPE",
                "nodeName",
                "needs",
                "has",
                passSpecifier
            );

            protoPatchBuilder.Build(urlConfig, Command.Edit, tagList).Returns(protoPatch);
            needsChecker.CheckNeedsExpression("needs").Returns(false);

            Assert.Null(patchExtractor.ExtractPatch(urlConfig));

            AssertNoErrors();

            passSpecifier.DidNotReceiveWithAnyArgs().CheckNeeds(null, null);
            needsChecker.DidNotReceiveWithAnyArgs().CheckNeedsRecursive(null, null);
            patchCompiler.DidNotReceiveWithAnyArgs().CompilePatch(null);

            progress.Received().NeedsUnsatisfiedRoot(urlConfig);
        }

        [Fact]
        public void TestExtractPatch__NeedsUnsatisfiedPassSpecifier()
        {
            UrlDir.UrlConfig urlConfig = CreateConfig("@NODE_TYPE");

            ITagList tagList = Substitute.For<ITagList>();
            tagListParser.Parse("NODE_TYPE", urlConfig).Returns(tagList);

            IPassSpecifier passSpecifier = Substitute.For<IPassSpecifier>();
            ProtoPatch protoPatch = new ProtoPatch(
                urlConfig,
                Command.Edit,
                "NODE_TYPE",
                "nodeName",
                "needs",
                "has",
                passSpecifier
            );

            protoPatchBuilder.Build(urlConfig, Command.Edit, tagList).Returns(protoPatch);
            needsChecker.CheckNeedsExpression("needs").Returns(true);
            passSpecifier.CheckNeeds(needsChecker, progress).Returns(false);

            Assert.Null(patchExtractor.ExtractPatch(urlConfig));

            AssertNoErrors();

            needsChecker.DidNotReceiveWithAnyArgs().CheckNeedsRecursive(null, null);
            patchCompiler.DidNotReceiveWithAnyArgs().CompilePatch(null);

            progress.DidNotReceiveWithAnyArgs().NeedsUnsatisfiedRoot(null);
        }

        [Fact]
        public void TestExtractPatch__Null()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                patchExtractor.ExtractPatch(null);
            });

            Assert.Equal("urlConfig", ex.ParamName);
        }

        [Fact]
        public void TestExtractPatch__NotBracketBalanced()
        {
            UrlDir.UrlConfig config1 = CreateConfig("@NODE:FOR[");
            UrlDir.UrlConfig config2 = CreateConfig("NODE:HAS[#foo[]");

            patchExtractor.ExtractPatch(config1);
            patchExtractor.ExtractPatch(config2);
        
            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);
        
            Received.InOrder(delegate
            {
                progress.Received().Error(config1, "Error - node name does not have balanced brackets (or a space - if so replace with ?):\nabc/def/@NODE:FOR[");
                progress.Received().Error(config2, "Error - node name does not have balanced brackets (or a space - if so replace with ?):\nabc/def/NODE:HAS[#foo[]");
            });

            progress.DidNotReceiveWithAnyArgs().NeedsUnsatisfiedRoot(null);
        }

        [Fact]
        public void TestExtractPatch__InvalidCommand__Replace()
        {
            UrlDir.UrlConfig urlConfig = CreateConfig("%NODE");
            Assert.Null(patchExtractor.ExtractPatch(urlConfig));

            progress.Received().Error(urlConfig, "Error - replace command (%) is not valid on a root node: abc/def/%NODE");
        }

        [Fact]
        public void TestExtractPatch__InvalidCommand__Create()
        {
            UrlDir.UrlConfig urlConfig = CreateConfig("&NODE");
            Assert.Null(patchExtractor.ExtractPatch(urlConfig));

            progress.Received().Error(urlConfig, "Error - create command (&) is not valid on a root node: abc/def/&NODE");
        }

        [Fact]
        public void TestExtractPatch__InvalidCommand__Rename()
        {
            UrlDir.UrlConfig urlConfig = CreateConfig("|NODE");
            Assert.Null(patchExtractor.ExtractPatch(urlConfig));

            progress.Received().Error(urlConfig, "Error - rename command (|) is not valid on a root node: abc/def/|NODE");
        }

        [Fact]
        public void TestExtractPatch__InvalidCommand__Paste()
        {
            UrlDir.UrlConfig urlConfig = CreateConfig("#NODE");
            Assert.Null(patchExtractor.ExtractPatch(urlConfig));

            progress.Received().Error(urlConfig, "Error - paste command (#) is not valid on a root node: abc/def/#NODE");
        }

        [Fact]
        public void TestExtractPatch__InvalidCommand__Special()
        {
            UrlDir.UrlConfig urlConfig = CreateConfig("*NODE");
            Assert.Null(patchExtractor.ExtractPatch(urlConfig));

            progress.Received().Error(urlConfig, "Error - special command (*) is not valid on a root node: abc/def/*NODE");
        }

        [Fact]
        public void TestExtractPatch__TagListBadlyFormatted()
        {
            UrlDir.UrlConfig urlConfig = CreateConfig("badSomehow");
            tagListParser.When(t => t.Parse("badSomehow", urlConfig)).Throw(new FormatException("badly formatted"));
            Assert.Null(patchExtractor.ExtractPatch(urlConfig));

            progress.Received().Error(urlConfig, "Cannot parse node name as tag list: badly formatted\non: abc/def/badSomehow");
        }

        [Fact]
        public void TestExtractPatch__ProtoPatchFailed()
        {
            UrlDir.UrlConfig urlConfig = CreateConfig("NODE");
            protoPatchBuilder.Build(urlConfig, Command.Insert, Arg.Any<ITagList>()).Returns((ProtoPatch)null);
            Assert.Null(patchExtractor.ExtractPatch(urlConfig));

            AssertNoErrors();
        }

        [Fact]
        public void TestExtractPatch__Exception()
        {
            UrlDir.UrlConfig urlConfig = CreateConfig("NODE");
            Exception ex = new Exception();
            tagListParser.When(t => t.Parse("NODE", urlConfig)).Throw(ex);
            Assert.Null(patchExtractor.ExtractPatch(urlConfig));

            progress.Received().Exception(urlConfig, "Exception while attempting to create patch from config: abc/def/NODE", ex);
        }

        private UrlDir.UrlConfig CreateConfig(string name)
        {
            ConfigNode node = new TestConfigNode(name)
            {
                { "name", "snack" },
                { "cheese", "gouda" },
                { "bread", "sourdough" },
                new ConfigNode("wine"),
                new ConfigNode("fruit"),
            };
        
            node.id = "hungry?";
        
            return UrlBuilder.CreateConfig(node, file);
        }

        private void AssertNoErrors()
        {
            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);
        }
    }
}
