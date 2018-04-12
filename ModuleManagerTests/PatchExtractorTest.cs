using System;
using System.Collections.Generic;
using Xunit;
using NSubstitute;
using TestUtils;
using ModuleManager;
using ModuleManager.Logging;
using ModuleManager.Progress;

namespace ModuleManagerTests
{
    public class PatchExtractorTest
    {
        private UrlDir root;
        private UrlDir.UrlFile file;

        private IPatchProgress progress;
        private IPatchList patchList;
        private IBasicLogger logger;
        private PatchExtractor extractor;

        public PatchExtractorTest()
        {
            root = UrlBuilder.CreateRoot();
            file = UrlBuilder.CreateFile("abc/def.cfg", root);

            patchList = Substitute.For<IPatchList>();
            progress = Substitute.For<IPatchProgress>();
            logger = Substitute.For<IBasicLogger>();
            extractor = new PatchExtractor(patchList, progress, logger);
        }

        [Fact]
        public void TestConstructor__PatchListNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new PatchExtractor(null, progress, logger);
            });

            Assert.Equal("patchList", ex.ParamName);
        }

        [Fact]
        public void TestConstructor__ProgressNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new PatchExtractor(patchList, null, logger);
            });

            Assert.Equal("progress", ex.ParamName);
        }

        [Fact]
        public void TestConstructor__LoggerNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new PatchExtractor(patchList, progress, null);
            });

            Assert.Equal("logger", ex.ParamName);
        }

        [Fact]
        public void TestExtractPatch__Insert()
        {
            UrlDir.UrlConfig patchConfig = CreateConfig("NODE");

            extractor.ExtractPatch(patchConfig);

            AssertNoErrors();

            Assert.Equal(new[] { patchConfig }, root.AllConfigs);

            patchList.DidNotReceiveWithAnyArgs().AddFirstPatch(null);
            patchList.DidNotReceiveWithAnyArgs().AddLegacyPatch(null);
            patchList.DidNotReceiveWithAnyArgs().AddBeforePatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddForPatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddAfterPatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddFinalPatch(null);

            progress.DidNotReceive().PatchAdded();

            EnsureNeedsSatisfied();
        }

        [Fact]
        public void TestExtractPatch__First()
        {
            UrlDir.UrlConfig patchConfig1 = CreateConfig("@NODE[foo]:HAS[#bar]:FIRST");
            UrlDir.UrlConfig patchConfig2 = CreateConfig("@NODE:FIRST");
            UrlDir.UrlConfig patchConfig3 = CreateConfig("@NODE:First");
            UrlDir.UrlConfig patchConfig4 = CreateConfig("@NODE:first");

            List<Patch> patches = new List<Patch>();
            patchList.AddFirstPatch(Arg.Do<Patch>(patch => patches.Add(patch)));

            extractor.ExtractPatch(patchConfig1);
            extractor.ExtractPatch(patchConfig2);
            extractor.ExtractPatch(patchConfig3);
            extractor.ExtractPatch(patchConfig4);

            AssertNoErrors();

            Assert.Empty(root.AllConfigs);

            Assert.Equal(4, patches.Count);
            AssertPatchCorrect(patches[0], patchConfig1, Command.Edit, "NODE[foo]:HAS[#bar]");
            AssertPatchCorrect(patches[1], patchConfig2, Command.Edit, "NODE");
            AssertPatchCorrect(patches[2], patchConfig3, Command.Edit, "NODE");
            AssertPatchCorrect(patches[3], patchConfig4, Command.Edit, "NODE");

            Received.InOrder(delegate
            {
                patchList.Received().AddFirstPatch(patches[0]);
                progress.Received().PatchAdded();
                patchList.Received().AddFirstPatch(patches[1]);
                progress.Received().PatchAdded();
                patchList.Received().AddFirstPatch(patches[2]);
                progress.Received().PatchAdded();
                patchList.Received().AddFirstPatch(patches[3]);
                progress.Received().PatchAdded();
            });

            patchList.DidNotReceiveWithAnyArgs().AddLegacyPatch(null);
            patchList.DidNotReceiveWithAnyArgs().AddBeforePatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddForPatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddAfterPatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddFinalPatch(null);

            EnsureNeedsSatisfied();
        }

        [Fact]
        public void TestExtractPatch__Legacy()
        {
            UrlDir.UrlConfig patchConfig1 = CreateConfig("@NODE[foo]:HAS[#bar]");
            UrlDir.UrlConfig patchConfig2 = CreateConfig("@NODE");

            List<Patch> patches = new List<Patch>();
            patchList.AddLegacyPatch(Arg.Do<Patch>(patch => patches.Add(patch)));

            extractor.ExtractPatch(patchConfig1);
            extractor.ExtractPatch(patchConfig2);

            AssertNoErrors();

            Assert.Empty(root.AllConfigs);

            Assert.Equal(2, patches.Count);
            AssertPatchCorrect(patches[0], patchConfig1, Command.Edit, "NODE[foo]:HAS[#bar]");
            AssertPatchCorrect(patches[1], patchConfig2, Command.Edit, "NODE");

            Received.InOrder(delegate
            {
                patchList.Received().AddLegacyPatch(patches[0]);
                progress.Received().PatchAdded();
                patchList.Received().AddLegacyPatch(patches[1]);
                progress.Received().PatchAdded();
            });

            patchList.DidNotReceiveWithAnyArgs().AddFirstPatch(null);
            patchList.DidNotReceiveWithAnyArgs().AddBeforePatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddForPatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddAfterPatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddFinalPatch(null);

            EnsureNeedsSatisfied();
        }

        [Fact]
        public void TestExtractPatch__BeforeMod()
        {
            patchList.HasMod("mod1").Returns(true);

            UrlDir.UrlConfig patchConfig1 = CreateConfig("@NODE[foo]:HAS[#bar]:BEFORE[mod1]");
            UrlDir.UrlConfig patchConfig2 = CreateConfig("@NODE:BEFORE[mod1]");
            UrlDir.UrlConfig patchConfig3 = CreateConfig("@NODE:Before[mod1]");
            UrlDir.UrlConfig patchConfig4 = CreateConfig("@NODE:before[MOD1]");

            List<Patch> patches = new List<Patch>();
            patchList.AddBeforePatch("mod1", Arg.Do<Patch>(patch => patches.Add(patch)));

            extractor.ExtractPatch(patchConfig1);
            extractor.ExtractPatch(patchConfig2);
            extractor.ExtractPatch(patchConfig3);
            extractor.ExtractPatch(patchConfig4);

            AssertNoErrors();

            Assert.Empty(root.AllConfigs);

            Assert.Equal(4, patches.Count);
            AssertPatchCorrect(patches[0], patchConfig1, Command.Edit, "NODE[foo]:HAS[#bar]");
            AssertPatchCorrect(patches[1], patchConfig2, Command.Edit, "NODE");
            AssertPatchCorrect(patches[2], patchConfig3, Command.Edit, "NODE");
            AssertPatchCorrect(patches[3], patchConfig4, Command.Edit, "NODE");

            Received.InOrder(delegate
            {
                patchList.Received().AddBeforePatch("mod1", patches[0]);
                progress.Received().PatchAdded();
                patchList.Received().AddBeforePatch("mod1", patches[1]);
                progress.Received().PatchAdded();
                patchList.Received().AddBeforePatch("mod1", patches[2]);
                progress.Received().PatchAdded();
                patchList.Received().AddBeforePatch("mod1", patches[3]);
                progress.Received().PatchAdded();
            });

            patchList.DidNotReceiveWithAnyArgs().AddFirstPatch(null);
            patchList.DidNotReceiveWithAnyArgs().AddLegacyPatch(null);
            patchList.DidNotReceiveWithAnyArgs().AddForPatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddAfterPatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddFinalPatch(null);

            EnsureNeedsSatisfied();
        }

        [Fact]
        public void TestExtractPatch__BeforeMod__ModDoesNotExist()
        {
            UrlDir.UrlConfig patchConfig1 = CreateConfig("@NODE[foo]:HAS[#bar]:BEFORE[mod3]");
            UrlDir.UrlConfig patchConfig2 = CreateConfig("@NODE:BEFORE[mod3]");
            UrlDir.UrlConfig patchConfig3 = CreateConfig("@NODE:Before[mod3]");
            UrlDir.UrlConfig patchConfig4 = CreateConfig("@NODE:before[MOD3]");

            extractor.ExtractPatch(patchConfig1);
            extractor.ExtractPatch(patchConfig2);
            extractor.ExtractPatch(patchConfig3);
            extractor.ExtractPatch(patchConfig4);

            AssertNoErrors();

            Assert.Empty(root.AllConfigs);

            Received.InOrder(delegate
            {
                progress.Received().NeedsUnsatisfiedBefore(patchConfig1);
                progress.Received().NeedsUnsatisfiedBefore(patchConfig2);
                progress.Received().NeedsUnsatisfiedBefore(patchConfig3);
                progress.Received().NeedsUnsatisfiedBefore(patchConfig4);
            });

            progress.DidNotReceiveWithAnyArgs().NeedsUnsatisfiedFor(null);
            progress.DidNotReceiveWithAnyArgs().NeedsUnsatisfiedAfter(null);

            patchList.DidNotReceiveWithAnyArgs().AddFirstPatch(null);
            patchList.DidNotReceiveWithAnyArgs().AddLegacyPatch(null);
            patchList.DidNotReceiveWithAnyArgs().AddBeforePatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddForPatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddAfterPatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddFinalPatch(null);

            progress.DidNotReceive().PatchAdded();
        }

        [Fact]
        public void TestExtractPatch__ForMod()
        {
            patchList.HasMod("mod1").Returns(true);

            UrlDir.UrlConfig patchConfig1 = CreateConfig("@NODE[foo]:HAS[#bar]:FOR[mod1]");
            UrlDir.UrlConfig patchConfig2 = CreateConfig("@NODE:FOR[mod1]");
            UrlDir.UrlConfig patchConfig3 = CreateConfig("@NODE:For[mod1]");
            UrlDir.UrlConfig patchConfig4 = CreateConfig("@NODE:for[MOD1]");

            List<Patch> patches = new List<Patch>();
            patchList.AddForPatch("mod1", Arg.Do<Patch>(patch => patches.Add(patch)));

            extractor.ExtractPatch(patchConfig1);
            extractor.ExtractPatch(patchConfig2);
            extractor.ExtractPatch(patchConfig3);
            extractor.ExtractPatch(patchConfig4);

            AssertNoErrors();

            Assert.Empty(root.AllConfigs);

            Assert.Equal(4, patches.Count);
            AssertPatchCorrect(patches[0], patchConfig1, Command.Edit, "NODE[foo]:HAS[#bar]");
            AssertPatchCorrect(patches[1], patchConfig2, Command.Edit, "NODE");
            AssertPatchCorrect(patches[2], patchConfig3, Command.Edit, "NODE");
            AssertPatchCorrect(patches[3], patchConfig4, Command.Edit, "NODE");


            Received.InOrder(delegate
            {
                patchList.Received().AddForPatch("mod1", patches[0]);
                progress.Received().PatchAdded();
                patchList.Received().AddForPatch("mod1", patches[1]);
                progress.Received().PatchAdded();
                patchList.Received().AddForPatch("mod1", patches[2]);
                progress.Received().PatchAdded();
                patchList.Received().AddForPatch("mod1", patches[3]);
                progress.Received().PatchAdded();
            });

            patchList.DidNotReceiveWithAnyArgs().AddFirstPatch(null);
            patchList.DidNotReceiveWithAnyArgs().AddLegacyPatch(null);
            patchList.DidNotReceiveWithAnyArgs().AddBeforePatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddAfterPatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddFinalPatch(null);

            EnsureNeedsSatisfied();
        }

        [Fact]
        public void TestExtractPatch__ForMod__ModDoesNotExist()
        {
            UrlDir.UrlConfig patchConfig1 = CreateConfig("@NODE[foo]:HAS[#bar]:FOR[mod3]");
            UrlDir.UrlConfig patchConfig2 = CreateConfig("@NODE:FOR[mod3]");
            UrlDir.UrlConfig patchConfig3 = CreateConfig("@NODE:For[mod3]");
            UrlDir.UrlConfig patchConfig4 = CreateConfig("@NODE:for[MOD3]");

            extractor.ExtractPatch(patchConfig1);
            extractor.ExtractPatch(patchConfig2);
            extractor.ExtractPatch(patchConfig3);
            extractor.ExtractPatch(patchConfig4);

            AssertNoErrors();

            Assert.Empty(root.AllConfigs);

            Received.InOrder(delegate
            {
                progress.Received().NeedsUnsatisfiedFor(patchConfig1);
                progress.Received().NeedsUnsatisfiedFor(patchConfig2);
                progress.Received().NeedsUnsatisfiedFor(patchConfig3);
                progress.Received().NeedsUnsatisfiedFor(patchConfig4);
            });

            progress.DidNotReceiveWithAnyArgs().NeedsUnsatisfiedBefore(null);
            progress.DidNotReceiveWithAnyArgs().NeedsUnsatisfiedAfter(null);

            patchList.DidNotReceiveWithAnyArgs().AddFirstPatch(null);
            patchList.DidNotReceiveWithAnyArgs().AddLegacyPatch(null);
            patchList.DidNotReceiveWithAnyArgs().AddBeforePatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddForPatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddAfterPatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddFinalPatch(null);

            progress.DidNotReceive().PatchAdded();
        }

        [Fact]
        public void TestExtractPatch__AfterMod()
        {
            patchList.HasMod("mod1").Returns(true);

            UrlDir.UrlConfig patchConfig1 = CreateConfig("@NODE[foo]:HAS[#bar]:AFTER[mod1]");
            UrlDir.UrlConfig patchConfig2 = CreateConfig("@NODE:AFTER[mod1]");
            UrlDir.UrlConfig patchConfig3 = CreateConfig("@NODE:After[mod1]");
            UrlDir.UrlConfig patchConfig4 = CreateConfig("@NODE:after[MOD1]");

            List<Patch> patches = new List<Patch>();
            patchList.AddAfterPatch("mod1", Arg.Do<Patch>(patch => patches.Add(patch)));

            extractor.ExtractPatch(patchConfig1);
            extractor.ExtractPatch(patchConfig2);
            extractor.ExtractPatch(patchConfig3);
            extractor.ExtractPatch(patchConfig4);

            AssertNoErrors();

            Assert.Equal(4, patches.Count);
            AssertPatchCorrect(patches[0], patchConfig1, Command.Edit, "NODE[foo]:HAS[#bar]");
            AssertPatchCorrect(patches[1], patchConfig2, Command.Edit, "NODE");
            AssertPatchCorrect(patches[2], patchConfig3, Command.Edit, "NODE");
            AssertPatchCorrect(patches[3], patchConfig4, Command.Edit, "NODE");

            Assert.Empty(root.AllConfigs);

            Received.InOrder(delegate
            {
                patchList.Received().AddAfterPatch("mod1", patches[0]);
                progress.Received().PatchAdded();
                patchList.Received().AddAfterPatch("mod1", patches[1]);
                progress.Received().PatchAdded();
                patchList.Received().AddAfterPatch("mod1", patches[2]);
                progress.Received().PatchAdded();
                patchList.Received().AddAfterPatch("mod1", patches[3]);
                progress.Received().PatchAdded();
            });

            patchList.DidNotReceiveWithAnyArgs().AddFirstPatch(null);
            patchList.DidNotReceiveWithAnyArgs().AddLegacyPatch(null);
            patchList.DidNotReceiveWithAnyArgs().AddBeforePatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddForPatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddFinalPatch(null);

            EnsureNeedsSatisfied();
        }

        [Fact]
        public void TestExtractPatch__AfterMod__ModDoesNotExist()
        {
            UrlDir.UrlConfig patchConfig1 = CreateConfig("@NODE[foo]:HAS[#bar]:AFTER[mod3]");
            UrlDir.UrlConfig patchConfig2 = CreateConfig("@NODE:AFTER[mod3]");
            UrlDir.UrlConfig patchConfig3 = CreateConfig("@NODE:After[mod3]");
            UrlDir.UrlConfig patchConfig4 = CreateConfig("@NODE:after[MOD3]");

            extractor.ExtractPatch(patchConfig1);
            extractor.ExtractPatch(patchConfig2);
            extractor.ExtractPatch(patchConfig3);
            extractor.ExtractPatch(patchConfig4);

            AssertNoErrors();

            Assert.Empty(root.AllConfigs);

            Received.InOrder(delegate
            {
                progress.Received().NeedsUnsatisfiedAfter(patchConfig1);
                progress.Received().NeedsUnsatisfiedAfter(patchConfig2);
                progress.Received().NeedsUnsatisfiedAfter(patchConfig3);
                progress.Received().NeedsUnsatisfiedAfter(patchConfig4);
            });

            progress.DidNotReceiveWithAnyArgs().NeedsUnsatisfiedBefore(null);
            progress.DidNotReceiveWithAnyArgs().NeedsUnsatisfiedFor(null);

            patchList.DidNotReceiveWithAnyArgs().AddFirstPatch(null);
            patchList.DidNotReceiveWithAnyArgs().AddLegacyPatch(null);
            patchList.DidNotReceiveWithAnyArgs().AddBeforePatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddForPatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddAfterPatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddFinalPatch(null);

            progress.DidNotReceive().PatchAdded();
        }

        [Fact]
        public void TestExtractPatch__Final()
        {
            UrlDir.UrlConfig patchConfig1 = CreateConfig("@NODE[foo]:HAS[#bar]:FINAL");
            UrlDir.UrlConfig patchConfig2 = CreateConfig("@NODE:FINAL");
            UrlDir.UrlConfig patchConfig3 = CreateConfig("@NODE:Final");
            UrlDir.UrlConfig patchConfig4 = CreateConfig("@NODE:final");

            List<Patch> patches = new List<Patch>();
            patchList.AddFinalPatch(Arg.Do<Patch>(patch => patches.Add(patch)));

            extractor.ExtractPatch(patchConfig1);
            extractor.ExtractPatch(patchConfig2);
            extractor.ExtractPatch(patchConfig3);
            extractor.ExtractPatch(patchConfig4);

            AssertNoErrors();

            Assert.Empty(root.AllConfigs);

            Assert.Equal(4, patches.Count);
            AssertPatchCorrect(patches[0], patchConfig1, Command.Edit, "NODE[foo]:HAS[#bar]");
            AssertPatchCorrect(patches[1], patchConfig2, Command.Edit, "NODE");
            AssertPatchCorrect(patches[2], patchConfig3, Command.Edit, "NODE");
            AssertPatchCorrect(patches[3], patchConfig4, Command.Edit, "NODE");

            Received.InOrder(delegate
            {
                patchList.Received().AddFinalPatch(patches[0]);
                progress.Received().PatchAdded();
                patchList.Received().AddFinalPatch(patches[1]);
                progress.Received().PatchAdded();
                patchList.Received().AddFinalPatch(patches[2]);
                progress.Received().PatchAdded();
                patchList.Received().AddFinalPatch(patches[3]);
                progress.Received().PatchAdded();
            });

            patchList.DidNotReceiveWithAnyArgs().AddFirstPatch(null);
            patchList.DidNotReceiveWithAnyArgs().AddLegacyPatch(null);
            patchList.DidNotReceiveWithAnyArgs().AddBeforePatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddForPatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddAfterPatch(null, null);

            EnsureNeedsSatisfied();
        }

        [Fact]
        public void TestExtractPatch__InsertWithPass()
        {
            UrlDir.UrlConfig patchConfig1 = CreateConfig("NODE:FOR[mod1]");
            UrlDir.UrlConfig patchConfig2 = CreateConfig("NODE:FOR[mod2]");
            UrlDir.UrlConfig patchConfig3 = CreateConfig("NODE:FINAL");

            extractor.ExtractPatch(patchConfig1);
            extractor.ExtractPatch(patchConfig2);
            extractor.ExtractPatch(patchConfig3);

            Assert.Empty(root.AllConfigs);

            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);

            patchList.DidNotReceiveWithAnyArgs().AddFirstPatch(null);
            patchList.DidNotReceiveWithAnyArgs().AddLegacyPatch(null);
            patchList.DidNotReceiveWithAnyArgs().AddBeforePatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddForPatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddAfterPatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddFinalPatch(null);

            Received.InOrder(delegate
            {
                progress.Received().Error(patchConfig1, "Error - pass specifier detected on an insert node (not a patch): abc/def/NODE:FOR[mod1]");
                progress.Received().Error(patchConfig2, "Error - pass specifier detected on an insert node (not a patch): abc/def/NODE:FOR[mod2]");
                progress.Received().Error(patchConfig3, "Error - pass specifier detected on an insert node (not a patch): abc/def/NODE:FINAL");
            });

            EnsureNeedsSatisfied();

            progress.DidNotReceive().PatchAdded();
        }

        [Fact]
        public void TestExtractPatch__MoreThanOnePass()
        {
            UrlDir.UrlConfig patchConfig1 = CreateConfig("@NODE:FIRST:FIRST");
            UrlDir.UrlConfig patchConfig2 = CreateConfig("@NODE:FIRST:FOR[mod1]");
            UrlDir.UrlConfig patchConfig3 = CreateConfig("@NODE:FOR[mod1]:AFTER[mod2]");

            extractor.ExtractPatch(patchConfig1);
            extractor.ExtractPatch(patchConfig2);
            extractor.ExtractPatch(patchConfig3);

            Assert.Empty(root.AllConfigs);

            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);

            patchList.DidNotReceiveWithAnyArgs().AddFirstPatch(null);
            patchList.DidNotReceiveWithAnyArgs().AddLegacyPatch(null);
            patchList.DidNotReceiveWithAnyArgs().AddBeforePatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddForPatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddAfterPatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddFinalPatch(null);

            Received.InOrder(delegate
            {
                progress.Received().Error(patchConfig1, "Error - more than one pass specifier on a node: abc/def/@NODE:FIRST:FIRST");
                progress.Received().Error(patchConfig2, "Error - more than one pass specifier on a node: abc/def/@NODE:FIRST:FOR[mod1]");
                progress.Received().Error(patchConfig3, "Error - more than one pass specifier on a node: abc/def/@NODE:FOR[mod1]:AFTER[mod2]");
            });

            EnsureNeedsSatisfied();

            progress.DidNotReceive().PatchAdded();
        }

        [Fact]
        public void TestExtractPatch__Exception()
        {
            Exception e = new Exception("an exception was thrown");
            progress.WhenForAnyArgs(p => p.Error(null, null)).Throw(e);

            UrlDir.UrlConfig patchConfig1 = CreateConfig("@NODE:FIRST:FIRST");

            extractor.ExtractPatch(patchConfig1);

            Assert.Empty(root.AllConfigs);

            patchList.DidNotReceiveWithAnyArgs().AddFirstPatch(null);
            patchList.DidNotReceiveWithAnyArgs().AddLegacyPatch(null);
            patchList.DidNotReceiveWithAnyArgs().AddBeforePatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddForPatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddAfterPatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddFinalPatch(null);
            
            progress.DidNotReceiveWithAnyArgs().Exception(null, null);

            progress.Received().Exception(patchConfig1, "Exception while parsing pass for config: abc/def/@NODE:FIRST:FIRST", e);

            EnsureNeedsSatisfied();

            progress.DidNotReceive().PatchAdded();
        }

        [Fact]
        public void TestExtractPatch__NotBracketBalanced()
        {
            UrlDir.UrlConfig config1 = CreateConfig("@NODE:FOR[");
            UrlDir.UrlConfig config2 = CreateConfig("NODE:HAS[#foo[]");

            extractor.ExtractPatch(config1);
            extractor.ExtractPatch(config2);

            Assert.Empty(root.AllConfigs);

            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);

            patchList.DidNotReceiveWithAnyArgs().AddFirstPatch(null);
            patchList.DidNotReceiveWithAnyArgs().AddLegacyPatch(null);
            patchList.DidNotReceiveWithAnyArgs().AddBeforePatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddForPatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddAfterPatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddFinalPatch(null);

            Received.InOrder(delegate
            {
                progress.Received().Error(config1, "Error - node name does not have balanced brackets (or a space - if so replace with ?):\nabc/def/@NODE:FOR[");
                progress.Received().Error(config2, "Error - node name does not have balanced brackets (or a space - if so replace with ?):\nabc/def/NODE:HAS[#foo[]");
            });

            EnsureNeedsSatisfied();

            progress.DidNotReceive().PatchAdded();
        }

        [Fact]
        public void TestExtractPatch__BadlyFormed()
        {
            UrlDir.UrlConfig config1 = CreateConfig("@NODE[foo]:HAS[#bar]:FOR[]");
            UrlDir.UrlConfig config2 = CreateConfig("@NODE:BEFORE");
            UrlDir.UrlConfig config3 = CreateConfig("@NODE:AFTER");
            
            extractor.ExtractPatch(config1);
            extractor.ExtractPatch(config2);
            extractor.ExtractPatch(config3);

            Assert.Empty(root.AllConfigs);

            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);

            patchList.DidNotReceiveWithAnyArgs().AddFirstPatch(null);
            patchList.DidNotReceiveWithAnyArgs().AddLegacyPatch(null);
            patchList.DidNotReceiveWithAnyArgs().AddBeforePatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddForPatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddAfterPatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddFinalPatch(null);

            Received.InOrder(delegate
            {
                progress.Received().Error(config1, "Error - malformed :FOR patch specifier detected: abc/def/@NODE[foo]:HAS[#bar]:FOR[]");
                progress.Received().Error(config2, "Error - malformed :BEFORE patch specifier detected: abc/def/@NODE:BEFORE");
                progress.Received().Error(config3, "Error - malformed :AFTER patch specifier detected: abc/def/@NODE:AFTER");
            });

            EnsureNeedsSatisfied();

            progress.DidNotReceive().PatchAdded();
        }

        [Fact]
        public void TestExtractPatch__Command()
        {
            patchList.HasMod("mod1").Returns(true);

            UrlDir.UrlConfig config01 = CreateConfig("@NODE:FOR[mod1]");
            UrlDir.UrlConfig config02 = CreateConfig("+NODE:FOR[mod1]");
            UrlDir.UrlConfig config03 = CreateConfig("$NODE:FOR[mod1]");
            UrlDir.UrlConfig config04 = CreateConfig("!NODE:FOR[mod1]");
            UrlDir.UrlConfig config05 = CreateConfig("-NODE:FOR[mod1]");
            UrlDir.UrlConfig config06 = CreateConfig("%NODE:FOR[mod1]");
            UrlDir.UrlConfig config07 = CreateConfig("&NODE:FOR[mod1]");
            UrlDir.UrlConfig config08 = CreateConfig("|NODE:FOR[mod1]");
            UrlDir.UrlConfig config09 = CreateConfig("#NODE:FOR[mod1]");
            UrlDir.UrlConfig config10 = CreateConfig("*NODE:FOR[mod1]");

            List<Patch> patches = new List<Patch>();
            patchList.AddForPatch("mod1", Arg.Do<Patch>(patch => patches.Add(patch)));

            extractor.ExtractPatch(config01);
            extractor.ExtractPatch(config02);
            extractor.ExtractPatch(config03);
            extractor.ExtractPatch(config04);
            extractor.ExtractPatch(config05);
            extractor.ExtractPatch(config06);
            extractor.ExtractPatch(config07);
            extractor.ExtractPatch(config08);
            extractor.ExtractPatch(config09);
            extractor.ExtractPatch(config10);

            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);

            Assert.Empty(root.AllConfigs);

            Assert.Equal(5, patches.Count);
            AssertPatchCorrect(patches[0], config01, Command.Edit, "NODE");
            AssertPatchCorrect(patches[1], config02, Command.Copy, "NODE");
            AssertPatchCorrect(patches[2], config03, Command.Copy, "NODE");
            AssertPatchCorrect(patches[3], config04, Command.Delete, "NODE");
            AssertPatchCorrect(patches[4], config05, Command.Delete, "NODE");
            progress.Received().PatchAdded();

            Received.InOrder(delegate
            {
                patchList.DidNotReceiveWithAnyArgs().AddForPatch("mod1", patches[0]);
                progress.Received().PatchAdded();
                patchList.DidNotReceiveWithAnyArgs().AddForPatch("mod1", patches[1]);
                progress.Received().PatchAdded();
                patchList.DidNotReceiveWithAnyArgs().AddForPatch("mod1", patches[2]);
                progress.Received().PatchAdded();
                patchList.DidNotReceiveWithAnyArgs().AddForPatch("mod1", patches[3]);
                progress.Received().PatchAdded();
                patchList.DidNotReceiveWithAnyArgs().AddForPatch("mod1", patches[4]);
                progress.Received().PatchAdded();
                progress.Received().Error(config06, "Error - replace command (%) is not valid on a root node: abc/def/%NODE:FOR[mod1]");
                progress.Received().Error(config07, "Error - create command (&) is not valid on a root node: abc/def/&NODE:FOR[mod1]");
                progress.Received().Error(config08, "Error - rename command (|) is not valid on a root node: abc/def/|NODE:FOR[mod1]");
                progress.Received().Error(config09, "Error - paste command (#) is not valid on a root node: abc/def/#NODE:FOR[mod1]");
                progress.Received().Error(config10, "Error - special command (*) is not valid on a root node: abc/def/*NODE:FOR[mod1]");
            });

            EnsureNeedsSatisfied();

            patchList.DidNotReceiveWithAnyArgs().AddFirstPatch(null);
            patchList.DidNotReceiveWithAnyArgs().AddLegacyPatch(null);
            patchList.DidNotReceiveWithAnyArgs().AddBeforePatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddAfterPatch(null, null);
            patchList.DidNotReceiveWithAnyArgs().AddFinalPatch(null);
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

        private void AssertPatchCorrect(Patch patch, UrlDir.UrlConfig originalUrl, Command expectedCommand, string expectedNodeName)
        {
            Assert.Same(originalUrl, patch.urlConfig);
            Assert.Equal(expectedCommand, patch.command);
            Assert.Equal(expectedNodeName, patch.node.name);

            ConfigNode originalNode = originalUrl.config;

            Assert.Equal(originalNode.id, patch.node.id);
            Assert.Equal(originalNode.values.Count, patch.node.values.Count);
            Assert.Equal(originalNode.nodes.Count, patch.node.nodes.Count);

            for (int i = 0; i < originalNode.values.Count; i++)
            {
                Assert.Same(originalNode.values[i], patch.node.values[i]);
            }

            for (int i = 0; i < originalNode.nodes.Count; i++)
            {
                Assert.Same(originalNode.nodes[i], patch.node.nodes[i]);
            }

            if (expectedNodeName == "NODE")
                AssertNodeMatcher__Bare(patch.nodeMatcher);
            else if (expectedNodeName == "NODE[foo]:HAS[#bar]")
                AssertNodeMatcher__Name__Has(patch.nodeMatcher);
            else
                throw new NotImplementedException();
        }

        private void AssertNoErrors()
        {
            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);
        }

        private void EnsureNeedsSatisfied()
        {
            progress.DidNotReceiveWithAnyArgs().NeedsUnsatisfiedBefore(null);
            progress.DidNotReceiveWithAnyArgs().NeedsUnsatisfiedFor(null);
            progress.DidNotReceiveWithAnyArgs().NeedsUnsatisfiedAfter(null);
        }

        private void AssertNodeMatcher__Bare(INodeMatcher matcher)
        {
            Assert.True(matcher.IsMatch(new ConfigNode("NODE")));

            Assert.True(matcher.IsMatch(new TestConfigNode("NODE")
            {
                { "name", "boo" },
                { "bar", "baz" },
            }));

            Assert.False(matcher.IsMatch(new ConfigNode("NADE")));
        }

        private void AssertNodeMatcher__Name__Has(INodeMatcher matcher)
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
    }
}
