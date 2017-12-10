using System;
using System.Collections.Generic;
using Xunit;
using NSubstitute;
using TestUtils;
using ModuleManager;
using ModuleManager.Progress;

namespace ModuleManagerTests
{
    public class PatchExtractorTest
    {
        private UrlDir root;
        private UrlDir.UrlFile file;

        private IPatchProgress progress;

        public PatchExtractorTest()
        {
            root = UrlBuilder.CreateRoot();
            file = UrlBuilder.CreateFile("abc/def.cfg", root);

            progress = Substitute.For<IPatchProgress>();
        }

        [Fact]
        public void TestSortAndExtractPatches()
        {
            UrlDir.UrlConfig[] insertConfigs =
            {
                CreateConfig("NODE"),
                CreateConfig("NADE"),
            };

            UrlDir.UrlConfig[] legacyConfigs =
            {
                CreateConfig("@NODE"),
                CreateConfig("@NADE[foo]:HAS[#bar]"),
            };

            UrlDir.UrlConfig[] firstConfigs =
            {
                CreateConfig("@NODE:FIRST"),
                CreateConfig("@NODE[foo]:HAS[#bar]:FIRST"),
                CreateConfig("@NADE:First"),
                CreateConfig("@NADE:first"),
            };

            UrlDir.UrlConfig[] finalConfigs =
            {
                CreateConfig("@NODE:FINAL"),
                CreateConfig("@NODE[foo]:HAS[#bar]:FINAL"),
                CreateConfig("@NADE:Final"),
                CreateConfig("@NADE:final"),
            };

            UrlDir.UrlConfig[] beforeMod1Configs =
            {
                CreateConfig("@NODE:BEFORE[mod1]"),
                CreateConfig("@NODE[foo]:HAS[#bar]:BEFORE[mod1]"),
                CreateConfig("@NADE:before[mod1]"),
                CreateConfig("@NADE:BEFORE[MOD1]"),
            };

            UrlDir.UrlConfig[] forMod1Configs =
            {
                CreateConfig("@NODE:FOR[mod1]"),
                CreateConfig("@NODE[foo]:HAS[#bar]:FOR[mod1]"),
                CreateConfig("@NADE:for[mod1]"),
                CreateConfig("@NADE:FOR[MOD1]"),
            };

            UrlDir.UrlConfig[] afterMod1Configs =
            {
                CreateConfig("@NODE:AFTER[mod1]"),
                CreateConfig("@NODE[foo]:HAS[#bar]:AFTER[mod1]"),
                CreateConfig("@NADE:after[mod1]"),
                CreateConfig("@NADE:AFTER[MOD1]"),
            };

            UrlDir.UrlConfig[] beforeMod2Configs =
            {
                CreateConfig("@NODE:BEFORE[mod2]"),
                CreateConfig("@NODE[foo]:HAS[#bar]:BEFORE[mod2]"),
                CreateConfig("@NADE:before[mod2]"),
                CreateConfig("@NADE:BEFORE[MOD2]"),
            };

            UrlDir.UrlConfig[] forMod2Configs =
            {
                CreateConfig("@NODE:FOR[mod2]"),
                CreateConfig("@NODE[foo]:HAS[#bar]:FOR[mod2]"),
                CreateConfig("@NADE:for[mod2]"),
                CreateConfig("@NADE:FOR[MOD2]"),
            };

            UrlDir.UrlConfig[] afterMod2Configs =
            {
                CreateConfig("@NODE:AFTER[mod2]"),
                CreateConfig("@NODE[foo]:HAS[#bar]:AFTER[mod2]"),
                CreateConfig("@NADE:after[mod2]"),
                CreateConfig("@NADE:AFTER[MOD2]"),
            };

            UrlDir.UrlConfig[] beforeMod3Configs =
            {
                CreateConfig("@NODE:BEFORE[mod3]"),
                CreateConfig("@NODE[foo]:HAS[#bar]:BEFORE[mod3]"),
                CreateConfig("@NADE:before[mod3]"),
                CreateConfig("@NADE:BEFORE[MOD3]"),
            };

            UrlDir.UrlConfig[] forMod3Configs =
            {
                CreateConfig("@NODE:FOR[mod3]"),
                CreateConfig("@NODE[foo]:HAS[#bar]:FOR[mod3]"),
                CreateConfig("@NADE:for[mod3]"),
                CreateConfig("@NADE:FOR[MOD3]"),
            };

            UrlDir.UrlConfig[] afterMod3Configs =
            {
                CreateConfig("@NODE:AFTER[mod3]"),
                CreateConfig("@NODE[foo]:HAS[#bar]:AFTER[mod3]"),
                CreateConfig("@NADE:after[mod3]"),
                CreateConfig("@NADE:AFTER[MOD3]"),
            };

            string[] modList = { "mod1", "mod2" };
            PatchList list = PatchExtractor.SortAndExtractPatches(root, modList, progress);

            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);

            Assert.True(list.modPasses.HasMod("mod1"));
            Assert.True(list.modPasses.HasMod("mod2"));
            Assert.False(list.modPasses.HasMod("mod3"));

            Assert.Equal(insertConfigs, root.AllConfigs);

            List<Patch> currentPatches;

            currentPatches = list.legacyPatches;
            Assert.Equal(legacyConfigs.Length, currentPatches.Count);
            AssertPatchCorrect(currentPatches[0], legacyConfigs[0], Command.Edit, "NODE");
            AssertPatchCorrect(currentPatches[1], legacyConfigs[1], Command.Edit, "NADE[foo]:HAS[#bar]");

            currentPatches = list.firstPatches;
            Assert.Equal(firstConfigs.Length, currentPatches.Count);
            AssertPatchCorrect(currentPatches[0], firstConfigs[0], Command.Edit, "NODE");
            AssertPatchCorrect(currentPatches[1], firstConfigs[1], Command.Edit, "NODE[foo]:HAS[#bar]");
            AssertPatchCorrect(currentPatches[2], firstConfigs[2], Command.Edit, "NADE");
            AssertPatchCorrect(currentPatches[3], firstConfigs[3], Command.Edit, "NADE");

            currentPatches = list.finalPatches;
            Assert.Equal(finalConfigs.Length, currentPatches.Count);
            AssertPatchCorrect(currentPatches[0], finalConfigs[0], Command.Edit, "NODE");
            AssertPatchCorrect(currentPatches[1], finalConfigs[1], Command.Edit, "NODE[foo]:HAS[#bar]");
            AssertPatchCorrect(currentPatches[2], finalConfigs[2], Command.Edit, "NADE");
            AssertPatchCorrect(currentPatches[3], finalConfigs[3], Command.Edit, "NADE");

            currentPatches = list.modPasses["mod1"].beforePatches;
            Assert.Equal(beforeMod1Configs.Length, currentPatches.Count);
            AssertPatchCorrect(currentPatches[0], beforeMod1Configs[0], Command.Edit, "NODE");
            AssertPatchCorrect(currentPatches[1], beforeMod1Configs[1], Command.Edit, "NODE[foo]:HAS[#bar]");
            AssertPatchCorrect(currentPatches[2], beforeMod1Configs[2], Command.Edit, "NADE");
            AssertPatchCorrect(currentPatches[3], beforeMod1Configs[3], Command.Edit, "NADE");

            currentPatches = list.modPasses["mod1"].forPatches;
            Assert.Equal(forMod1Configs.Length, currentPatches.Count);
            AssertPatchCorrect(currentPatches[0], forMod1Configs[0], Command.Edit, "NODE");
            AssertPatchCorrect(currentPatches[1], forMod1Configs[1], Command.Edit, "NODE[foo]:HAS[#bar]");
            AssertPatchCorrect(currentPatches[2], forMod1Configs[2], Command.Edit, "NADE");
            AssertPatchCorrect(currentPatches[3], forMod1Configs[3], Command.Edit, "NADE");

            currentPatches = list.modPasses["mod1"].afterPatches;
            Assert.Equal(afterMod1Configs.Length, currentPatches.Count);
            AssertPatchCorrect(currentPatches[0], afterMod1Configs[0], Command.Edit, "NODE");
            AssertPatchCorrect(currentPatches[1], afterMod1Configs[1], Command.Edit, "NODE[foo]:HAS[#bar]");
            AssertPatchCorrect(currentPatches[2], afterMod1Configs[2], Command.Edit, "NADE");
            AssertPatchCorrect(currentPatches[3], afterMod1Configs[3], Command.Edit, "NADE");

            currentPatches = list.modPasses["mod2"].beforePatches;
            Assert.Equal(beforeMod2Configs.Length, currentPatches.Count);
            AssertPatchCorrect(currentPatches[0], beforeMod2Configs[0], Command.Edit, "NODE");
            AssertPatchCorrect(currentPatches[1], beforeMod2Configs[1], Command.Edit, "NODE[foo]:HAS[#bar]");
            AssertPatchCorrect(currentPatches[2], beforeMod2Configs[2], Command.Edit, "NADE");
            AssertPatchCorrect(currentPatches[3], beforeMod2Configs[3], Command.Edit, "NADE");

            currentPatches = list.modPasses["mod2"].forPatches;
            Assert.Equal(forMod2Configs.Length, currentPatches.Count);
            AssertPatchCorrect(currentPatches[0], forMod2Configs[0], Command.Edit, "NODE");
            AssertPatchCorrect(currentPatches[1], forMod2Configs[1], Command.Edit, "NODE[foo]:HAS[#bar]");
            AssertPatchCorrect(currentPatches[2], forMod2Configs[2], Command.Edit, "NADE");
            AssertPatchCorrect(currentPatches[3], forMod2Configs[3], Command.Edit, "NADE");

            currentPatches = list.modPasses["mod2"].afterPatches;
            Assert.Equal(afterMod2Configs.Length, currentPatches.Count);
            AssertPatchCorrect(currentPatches[0], afterMod2Configs[0], Command.Edit, "NODE");
            AssertPatchCorrect(currentPatches[1], afterMod2Configs[1], Command.Edit, "NODE[foo]:HAS[#bar]");
            AssertPatchCorrect(currentPatches[2], afterMod2Configs[2], Command.Edit, "NADE");
            AssertPatchCorrect(currentPatches[3], afterMod2Configs[3], Command.Edit, "NADE");

            progress.Received(34).PatchAdded();

            progress.Received().NeedsUnsatisfiedBefore(beforeMod3Configs[0]);
            progress.Received().NeedsUnsatisfiedBefore(beforeMod3Configs[1]);
            progress.Received().NeedsUnsatisfiedBefore(beforeMod3Configs[2]);
            progress.Received().NeedsUnsatisfiedBefore(beforeMod3Configs[3]);

            progress.Received().NeedsUnsatisfiedFor(forMod3Configs[0]);
            progress.Received().NeedsUnsatisfiedFor(forMod3Configs[1]);
            progress.Received().NeedsUnsatisfiedFor(forMod3Configs[2]);
            progress.Received().NeedsUnsatisfiedFor(forMod3Configs[3]);

            progress.Received().NeedsUnsatisfiedAfter(afterMod3Configs[0]);
            progress.Received().NeedsUnsatisfiedAfter(afterMod3Configs[1]);
            progress.Received().NeedsUnsatisfiedAfter(afterMod3Configs[2]);
            progress.Received().NeedsUnsatisfiedAfter(afterMod3Configs[3]);
        }

        [Fact]
        public void TestSortAndExtractPatches__InsertWithPass()
        {
            UrlDir.UrlConfig config1 = CreateConfig("NODE");
            UrlDir.UrlConfig config2 = CreateConfig("NODE:FOR[mod1]");
            UrlDir.UrlConfig config3 = CreateConfig("NODE:FOR[mod2]");
            UrlDir.UrlConfig config4 = CreateConfig("NODE:FINAL");

            string[] modList = { "mod1" };
            PatchList list = PatchExtractor.SortAndExtractPatches(root, modList, progress);

            Assert.Equal(new[] { config1 }, root.AllConfigs);

            progress.Received().Error(config2, "Error - pass specifier detected on an insert node (not a patch): abc/def/NODE:FOR[mod1]");
            progress.Received().Error(config3, "Error - pass specifier detected on an insert node (not a patch): abc/def/NODE:FOR[mod2]");
            progress.Received().Error(config4, "Error - pass specifier detected on an insert node (not a patch): abc/def/NODE:FINAL");

            Assert.Empty(list.firstPatches);
            Assert.Empty(list.legacyPatches);
            Assert.Empty(list.finalPatches);
            Assert.Empty(list.modPasses["mod1"].beforePatches);
            Assert.Empty(list.modPasses["mod1"].forPatches);
            Assert.Empty(list.modPasses["mod1"].afterPatches);

            progress.DidNotReceive().PatchAdded();
        }

        [Fact]
        public void TestSortAndExtractPatches__MoreThanOnePass()
        {
            UrlDir.UrlConfig config1 = CreateConfig("@NODE:FIRST");
            UrlDir.UrlConfig config2 = CreateConfig("@NODE:FIRST:FIRST");
            UrlDir.UrlConfig config3 = CreateConfig("@NODE:FIRST:FOR[mod1]");

            string[] modList = { "mod1" };
            PatchList list = PatchExtractor.SortAndExtractPatches(root, modList, progress);

            Assert.Empty(root.AllConfigs);

            progress.Received().Error(config2, "Error - more than one pass specifier on a node: abc/def/@NODE:FIRST:FIRST");
            progress.Received().Error(config3, "Error - more than one pass specifier on a node: abc/def/@NODE:FIRST:FOR[mod1]");

            Assert.Equal(1, list.firstPatches.Count);
            AssertPatchCorrect(list.firstPatches[0], config1, Command.Edit, "NODE");
            Assert.Empty(list.legacyPatches);
            Assert.Empty(list.finalPatches);
            Assert.Empty(list.modPasses["mod1"].beforePatches);
            Assert.Empty(list.modPasses["mod1"].forPatches);
            Assert.Empty(list.modPasses["mod1"].afterPatches);

            progress.Received(1).PatchAdded();
        }

        [Fact]
        public void TestSortAndExtractPatches__Exception()
        {
            Exception e = new Exception("an exception was thrown");
            progress.WhenForAnyArgs(p => p.Error(null, null)).Throw(e);

            UrlDir.UrlConfig config1 = CreateConfig("@NODE");
            UrlDir.UrlConfig config2 = CreateConfig("@NODE:FIRST:FIRST");
            UrlDir.UrlConfig config3 = CreateConfig("@NADE:FIRST");

            string[] modList = { "mod1" };
            PatchList list = PatchExtractor.SortAndExtractPatches(root, modList, progress);

            progress.Received().Exception(config2, "Exception while parsing pass for config: abc/def/@NODE:FIRST:FIRST", e);

            Assert.Equal(1, list.legacyPatches.Count);
            AssertPatchCorrect(list.legacyPatches[0], config1, Command.Edit, "NODE");
            Assert.Equal(1, list.firstPatches.Count);
            AssertPatchCorrect(list.firstPatches[0], config3, Command.Edit, "NADE");

            progress.Received(2).PatchAdded();
        }

        [Fact]
        public void TestSortAndExtractPatches__NotBracketBalanced()
        {
            UrlDir.UrlConfig config1 = CreateConfig("@NODE:FOR[mod1]");
            UrlDir.UrlConfig config2 = CreateConfig("@NODE:FOR[");
            UrlDir.UrlConfig config3 = CreateConfig("NODE:HAS[#foo[]");

            string[] modList = { "mod1" };
            PatchList list = PatchExtractor.SortAndExtractPatches(root, modList, progress);

            Assert.Empty(root.AllConfigs);

            progress.Received().Error(config2, "Error - node name does not have balanced brackets (or a space - if so replace with ?):\nabc/def/@NODE:FOR[");
            progress.Received().Error(config3, "Error - node name does not have balanced brackets (or a space - if so replace with ?):\nabc/def/NODE:HAS[#foo[]");
            
            Assert.Empty(list.firstPatches);
            Assert.Empty(list.legacyPatches);
            Assert.Empty(list.finalPatches);
            Assert.Empty(list.modPasses["mod1"].beforePatches);
            Assert.Equal(1, list.modPasses["mod1"].forPatches.Count);
            AssertPatchCorrect(list.modPasses["mod1"].forPatches[0], config1, Command.Edit, "NODE");
            Assert.Empty(list.modPasses["mod1"].afterPatches);

            progress.Received(1).PatchAdded();
        }

        [Fact]
        public void TestSortAndExtractPatches__BadlyFormed()
        {
            UrlDir.UrlConfig config1 = CreateConfig("@NODE:FOR[mod1]");
            UrlDir.UrlConfig config2 = CreateConfig("@NODE:FOR[]");
            UrlDir.UrlConfig config3 = CreateConfig("@NADE:FIRST:BEFORE");
            UrlDir.UrlConfig config4 = CreateConfig("@NADE:AFTER");

            string[] modList = { "mod1" };
            PatchList list = PatchExtractor.SortAndExtractPatches(root, modList, progress);

            Assert.Empty(root.AllConfigs);

            progress.Received().Error(config2, "Error - malformed :FOR patch specifier detected: abc/def/@NODE:FOR[]");
            progress.Received().Error(config3, "Error - more than one pass specifier on a node: abc/def/@NADE:FIRST:BEFORE");
            progress.Received().Error(config3, "Error - malformed :BEFORE patch specifier detected: abc/def/@NADE:FIRST:BEFORE");
            progress.Received().Error(config4, "Error - malformed :AFTER patch specifier detected: abc/def/@NADE:AFTER");

            Assert.Empty(list.firstPatches);
            Assert.Empty(list.legacyPatches);
            Assert.Empty(list.finalPatches);
            Assert.Empty(list.modPasses["mod1"].beforePatches);
            Assert.Equal(1, list.modPasses["mod1"].forPatches.Count);
            AssertPatchCorrect(list.modPasses["mod1"].forPatches[0], config1, Command.Edit, "NODE");
            Assert.Empty(list.modPasses["mod1"].afterPatches);

            progress.Received(1).PatchAdded();
        }

        [Fact]
        public void TestSortAndExtractPatches__Command()
        {
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

            string[] modList = { "mod1" };
            PatchList list = PatchExtractor.SortAndExtractPatches(root, modList, progress);

            Assert.Empty(root.AllConfigs);

            progress.Received().Error(config06, "Error - replace command (%) is not valid on a root node: abc/def/%NODE:FOR[mod1]");
            progress.Received().Error(config07, "Error - create command (&) is not valid on a root node: abc/def/&NODE:FOR[mod1]");
            progress.Received().Error(config08, "Error - rename command (|) is not valid on a root node: abc/def/|NODE:FOR[mod1]");
            progress.Received().Error(config09, "Error - paste command (#) is not valid on a root node: abc/def/#NODE:FOR[mod1]");
            progress.Received().Error(config10, "Error - special command (*) is not valid on a root node: abc/def/*NODE:FOR[mod1]");

            Assert.Empty(list.firstPatches);
            Assert.Empty(list.legacyPatches);
            Assert.Empty(list.finalPatches);
            Assert.Empty(list.modPasses["mod1"].beforePatches);
            Assert.Equal(5, list.modPasses["mod1"].forPatches.Count);
            AssertPatchCorrect(list.modPasses["mod1"].forPatches[0], config01, Command.Edit, "NODE");
            AssertPatchCorrect(list.modPasses["mod1"].forPatches[1], config02, Command.Copy, "NODE");
            AssertPatchCorrect(list.modPasses["mod1"].forPatches[2], config03, Command.Copy, "NODE");
            AssertPatchCorrect(list.modPasses["mod1"].forPatches[3], config04, Command.Delete, "NODE");
            AssertPatchCorrect(list.modPasses["mod1"].forPatches[4], config05, Command.Delete, "NODE");
            Assert.Empty(list.modPasses["mod1"].afterPatches);

            progress.Received(5).PatchAdded();
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
        }
    }
}
