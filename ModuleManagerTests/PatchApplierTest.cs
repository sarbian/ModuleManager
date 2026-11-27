using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using NSubstitute;
using UnityEngine;
using TestUtils;
using ModuleManager;
using ModuleManager.Collections;
using ModuleManager.Logging;
using ModuleManager.Patches;
using ModuleManager.Patches.PassSpecifiers;
using ModuleManager.Progress;

namespace ModuleManagerTests
{
    public class PatchApplierTest
    {
        [Fact]
        public void TestConstructor__ProgressNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new PatchApplier(null, Substitute.For<IBasicLogger>());
            });

            Assert.Equal("progress", ex.ParamName);
        }

        [Fact]
        public void TestConstructor__LoggerNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new PatchApplier(Substitute.For<IPatchProgress>(), null);
            });

            Assert.Equal("logger", ex.ParamName);
        }

        [Fact]
        public void TestApplyPatches__PatchesNull()
        {
            PatchApplier applier = new PatchApplier(Substitute.For<IPatchProgress>(), Substitute.For<IBasicLogger>());
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                applier.ApplyPatches(null);
            });

            Assert.Equal("patches", ex.ParamName);
        }

        [Fact]
        public void TestApplyPatches()
        {
            IBasicLogger logger = Substitute.For<IBasicLogger>();
            IPatchProgress progress = Substitute.For<IPatchProgress>();
            PatchApplier patchApplier = new PatchApplier(progress, logger);
            IPass pass1 = Substitute.For<IPass>();
            IPass pass2 = Substitute.For<IPass>();
            IPass pass3 = Substitute.For<IPass>();
            pass1.Name.Returns(":PASS1");
            pass2.Name.Returns(":PASS2");
            pass3.Name.Returns(":PASS3");

            UrlDir.UrlConfig[] patchUrlConfigs = new UrlDir.UrlConfig[9];
            IPatch[] patches = new IPatch[9];
            for (int i = 0; i < patches.Length; i++)
            {
                patches[i] = Substitute.For<IPatch>();
            }

            patches[0].CountsAsPatch.Returns(false);
            patches[1].CountsAsPatch.Returns(false);
            patches[2].CountsAsPatch.Returns(false);
            patches[3].CountsAsPatch.Returns(true);
            patches[4].CountsAsPatch.Returns(true);
            patches[5].CountsAsPatch.Returns(true);
            patches[6].CountsAsPatch.Returns(true);
            patches[7].CountsAsPatch.Returns(true);
            patches[8].CountsAsPatch.Returns(true);

            pass1.GetEnumerator().Returns(new ArrayEnumerator<IPatch>(patches[0], patches[1], patches[2]));
            pass2.GetEnumerator().Returns(new ArrayEnumerator<IPatch>(patches[3], patches[4], patches[5]));
            pass3.GetEnumerator().Returns(new ArrayEnumerator<IPatch>(patches[6], patches[7], patches[8]));

            IPass[] patchList = new IPass[] { pass1, pass2, pass3 };

            LinkedList<IProtoUrlConfig> databaseConfigs = Assert.IsType<LinkedList<IProtoUrlConfig>>(patchApplier.ApplyPatches(new[] { pass1, pass2, pass3 }));

            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);

            logger.AssertNoWarning();
            logger.AssertNoError();
            logger.AssertNoException();

            Received.InOrder(delegate
            {
                progress.PassStarted(pass1);
                patches[0].Apply(databaseConfigs, progress, logger);
                patches[1].Apply(databaseConfigs, progress, logger);
                patches[2].Apply(databaseConfigs, progress, logger);
                progress.PassStarted(pass2);
                patches[3].Apply(databaseConfigs, progress, logger);
                progress.PatchApplied();
                patches[4].Apply(databaseConfigs, progress, logger);
                progress.PatchApplied();
                patches[5].Apply(databaseConfigs, progress, logger);
                progress.PatchApplied();
                progress.PassStarted(pass3);
                patches[6].Apply(databaseConfigs, progress, logger);
                progress.PatchApplied();
                patches[7].Apply(databaseConfigs, progress, logger);
                progress.PatchApplied();
                patches[8].Apply(databaseConfigs, progress, logger);
                progress.PatchApplied();
            });
        }

        [Fact]
        public void TestApplyPatches__TypeIndex()
        {
            // Edit patches visit nodes through a per-type index; copies, deletes, and inserts
            // between them must be seen by the edits that follow.
            IBasicLogger logger = Substitute.For<IBasicLogger>();
            IPatchProgress progress = Substitute.For<IPatchProgress>();
            PatchApplier patchApplier = new PatchApplier(progress, logger);
            IPassSpecifier passSpecifier = Substitute.For<IPassSpecifier>();

            IPatch Insert(string type, string name) => new InsertPatch(
                UrlBuilder.CreateConfig("abc/" + name, new TestConfigNode(type) { { "name", name } }), type, passSpecifier);
            UrlDir.UrlConfig Config(string url, ConfigNode node) => UrlBuilder.CreateConfig(url, node);

            IPass pass1 = Substitute.For<IPass>();
            pass1.GetEnumerator().Returns(new ArrayEnumerator<IPatch>(
                Insert("PART", "a"),
                Insert("PROP", "p"),
                Insert("PART", "b")
            ));

            IPass pass2 = Substitute.For<IPass>();
            pass2.GetEnumerator().Returns(new ArrayEnumerator<IPatch>(
                new EditPatch(Config("ghi/e1", new TestConfigNode("@PART[*]") { { "x", "1" } }), new NodeMatcher("PART", "*", null), passSpecifier),
                new CopyPatch(Config("ghi/c1", new TestConfigNode("+PART[a]") { { "@name", "c" } }), new NodeMatcher("PART", "a", null), passSpecifier),
                new EditPatch(Config("ghi/e2", new TestConfigNode("@PART[*]") { { "y", "2" } }), new NodeMatcher("PART", "*", null), passSpecifier),
                new DeletePatch(Config("ghi/d1", new ConfigNode("!PART[b]")), new NodeMatcher("PART", "b", null), passSpecifier),
                new EditPatch(Config("ghi/e3", new TestConfigNode("@PART[*]") { { "z", "3" } }), new NodeMatcher("PART", "*", null), passSpecifier),
                Insert("PART", "d"),
                new EditPatch(Config("ghi/e4", new TestConfigNode("@PART[d]") { { "w", "4" } }), new NodeMatcher("PART", "d", null), passSpecifier)
            ));

            IProtoUrlConfig[] configs = patchApplier.ApplyPatches(new[] { pass1, pass2 }).ToArray();

            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);

            Assert.Equal(new[] { "PART", "PART", "PROP", "PART" }, configs.Select(c => c.Node.name));
            AssertNodesEqual(new TestConfigNode("PART") { { "name", "a" }, { "x", "1" }, { "y", "2" }, { "z", "3" } }, configs[0].Node);
            AssertNodesEqual(new TestConfigNode("PART") { { "name", "c" }, { "x", "1" }, { "y", "2" }, { "z", "3" } }, configs[1].Node);
            AssertNodesEqual(new TestConfigNode("PROP") { { "name", "p" } }, configs[2].Node);
            AssertNodesEqual(new TestConfigNode("PART") { { "name", "d" }, { "w", "4" } }, configs[3].Node);
        }

        private void AssertNodesEqual(ConfigNode expected, ConfigNode actual)
        {
            Assert.Equal(expected.ToString(), actual.ToString());
        }
    }
}
