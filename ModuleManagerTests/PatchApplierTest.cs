using System;
using System.Linq;
using Xunit;
using NSubstitute;
using UnityEngine;
using TestUtils;
using ModuleManager;
using ModuleManager.Collections;
using ModuleManager.Extensions;
using ModuleManager.Logging;
using ModuleManager.Progress;

namespace ModuleManagerTests
{
    public class PatchApplierTest
    {
        private readonly IBasicLogger logger;
        private readonly IPatchProgress progress;
        private readonly string[] modList = new[] { "mod1", "mod2" };
        private UrlDir databaseRoot;
        private UrlDir.UrlFile file;
        private readonly IPass pass1;
        private readonly IPass pass2;
        private readonly IPass pass3;
        private readonly IPatchList patchList;
        private readonly PatchApplier patchApplier;

        public PatchApplierTest()
        {
            logger = Substitute.For<IBasicLogger>();
            progress = Substitute.For<IPatchProgress>();
            databaseRoot = UrlBuilder.CreateRoot();
            file = UrlBuilder.CreateFile("abc/def.cfg", databaseRoot);
            pass1 = Substitute.For<IPass>();
            pass2 = Substitute.For<IPass>();
            pass3 = Substitute.For<IPass>();
            pass1.Name.Returns(":PASS1");
            pass2.Name.Returns(":PASS2");
            pass3.Name.Returns(":PASS3");
            patchList = Substitute.For<IPatchList>();
            patchList.GetEnumerator().Returns(new ArrayEnumerator<IPass>(pass1, pass2, pass3));
            patchApplier = new PatchApplier(patchList, databaseRoot, progress, logger);
        }

        [Fact]
        public void TestApplyPatches__Edit()
        {
            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig(new TestConfigNode("PART")
            {
                { "name", "abc" },
                { "foo", "bar" },
            }, file);

            UrlDir.UrlConfig config2 = UrlBuilder.CreateConfig(new TestConfigNode("PART")
            {
                { "name", "def" },
            }, file);

            UrlDir.UrlConfig config3 = UrlBuilder.CreateConfig(new TestConfigNode("PORT")
            {
                { "name", "ghi" },
                { "jkl", "mno" },
            }, file);

            Patch patch1 = CreatePatch(Command.Edit, new TestConfigNode("@PART")
            {
                { "@foo", "baz" },
                { "pqr", "stw" },
            });

            pass1.GetEnumerator().Returns(new ArrayEnumerator<Patch>(patch1));

            patchApplier.ApplyPatches();

            EnsureNoErrors();

            Received.InOrder(delegate
            {
                logger.Received().Log(LogType.Log, ":PASS1 pass");
                progress.Received().ApplyingUpdate(config1, patch1.urlConfig);
                progress.Received().ApplyingUpdate(config2, patch1.urlConfig);
                progress.Received(1).PatchApplied();
                logger.Received().Log(LogType.Log, ":PASS2 pass");
                logger.Received().Log(LogType.Log, ":PASS3 pass");
            });

            UrlDir.UrlConfig[] allConfigs = databaseRoot.AllConfigs.ToArray();
            Assert.Equal(3, allConfigs.Length);

            AssertNodesEqual(new TestConfigNode("PART")
            {
                { "name", "abc" },
                { "foo", "baz" },
                { "pqr", "stw" },
            }, allConfigs[0].config);

            AssertNodesEqual(new TestConfigNode("PART")
            {
                { "name", "def" },
                { "pqr", "stw" },
            }, allConfigs[1].config);

            AssertNodesEqual(new TestConfigNode("PORT")
            {
                { "name", "ghi" },
                { "jkl", "mno" },
            }, allConfigs[2].config);
        }

        [Fact]
        public void TestApplyPatches__Copy()
        {
            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig(new TestConfigNode("PART")
            {
                { "name", "000" },
                { "aaa", "001" },
            }, file);

            UrlDir.UrlConfig config2 = UrlBuilder.CreateConfig(new TestConfigNode("PART")
            {
                { "name", "002" },
            }, file);

            UrlDir.UrlConfig config3 = UrlBuilder.CreateConfig(new TestConfigNode("PORT")
            {
                { "name", "003" },
                { "bbb", "004" },
            }, file);

            Patch patch1 = CreatePatch(Command.Copy, new TestConfigNode("+PART")
            {
                { "@name ^", ":^00:01:" },
                { "@aaa", "011" },
                { "ccc", "005" },
            });

            pass1.GetEnumerator().Returns(new ArrayEnumerator<Patch>(patch1));

            patchApplier.ApplyPatches();

            EnsureNoErrors();

            Received.InOrder(delegate
            {
                logger.Received().Log(LogType.Log, ":PASS1 pass");
                progress.Received().ApplyingCopy(config1, patch1.urlConfig);
                progress.Received().ApplyingCopy(config2, patch1.urlConfig);
                progress.Received(1).PatchApplied();
                logger.Received().Log(LogType.Log, ":PASS2 pass");
                logger.Received().Log(LogType.Log, ":PASS3 pass");
            });

            UrlDir.UrlConfig[] allConfigs = databaseRoot.AllConfigs.ToArray();
            Assert.Equal(5, allConfigs.Length);

            AssertNodesEqual(new TestConfigNode("PART")
            {
                { "name", "000" },
                { "aaa", "001" },
            }, allConfigs[0].config);

            AssertNodesEqual(new TestConfigNode("PART")
            {
                { "name", "002" },
            }, allConfigs[1].config);

            AssertNodesEqual(new TestConfigNode("PORT")
            {
                { "name", "003" },
                { "bbb", "004" },
            }, allConfigs[2].config);

            AssertNodesEqual(new TestConfigNode("PART")
            {
                { "name", "010" },
                { "aaa", "011" },
                { "ccc", "005" },
            }, allConfigs[3].config);

            AssertNodesEqual(new TestConfigNode("PART")
            {
                { "name", "012" },
                { "ccc", "005" },
            }, allConfigs[4].config);
        }

        [Fact]
        public void TestApplyPatches__Delete()
        {
            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig(new TestConfigNode("PART")
            {
                { "name", "abc" },
                { "foo", "bar" },
            }, file);

            UrlDir.UrlConfig config2 = UrlBuilder.CreateConfig(new TestConfigNode("PART")
            {
                { "name", "def" },
            }, file);

            UrlDir.UrlConfig config3 = UrlBuilder.CreateConfig(new TestConfigNode("PORT")
            {
                { "name", "ghi" },
                { "jkl", "mno" },
            }, file);

            Patch patch1 = CreatePatch(Command.Delete, new TestConfigNode("-PART"));

            pass1.GetEnumerator().Returns(new ArrayEnumerator<Patch>(patch1));

            patchApplier.ApplyPatches();

            EnsureNoErrors();

            Received.InOrder(delegate
            {
                logger.Received().Log(LogType.Log, ":PASS1 pass");
                progress.Received().ApplyingDelete(config1, patch1.urlConfig);
                progress.Received().ApplyingDelete(config2, patch1.urlConfig);
                progress.Received(1).PatchApplied();
                logger.Received().Log(LogType.Log, ":PASS2 pass");
                logger.Received().Log(LogType.Log, ":PASS3 pass");
            });

            UrlDir.UrlConfig[] allConfigs = databaseRoot.AllConfigs.ToArray();
            Assert.Equal(1, allConfigs.Length);

            AssertNodesEqual(new TestConfigNode("PORT")
            {
                { "name", "ghi" },
                { "jkl", "mno" },
            }, allConfigs[0].config);
        }

        [Fact]
        public void TestApplyPatches__Name()
        {
            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig(new TestConfigNode("PART")
            {
                { "name", "000" },
                { "aaa", "001" },
            }, file);

            UrlDir.UrlConfig config2 = UrlBuilder.CreateConfig(new TestConfigNode("PART")
            {
                { "name", "002" },
                { "bbb", "003" },
            }, file);

            UrlDir.UrlConfig config3 = UrlBuilder.CreateConfig(new TestConfigNode("PART")
            {
                { "name", "004" },
                { "ccc", "005" },
            }, file);

            UrlDir.UrlConfig config4 = UrlBuilder.CreateConfig(new TestConfigNode("PORT")
            {
                { "name", "006" },
                { "ddd", "007" },
            }, file);

            Patch patch1 = CreatePatch(Command.Edit, new TestConfigNode("@PART[000]")
            {
                { "@aaa", "011" },
                { "eee", "012" },
            });

            Patch patch2 = CreatePatch(Command.Copy, new TestConfigNode("+PART[002]")
            {
                { "@name", "022" },
                { "@bbb", "013" },
                { "fff", "014" },
            });

            Patch patch3 = CreatePatch(Command.Delete, new TestConfigNode("!PART[004]"));

            pass1.GetEnumerator().Returns(new ArrayEnumerator<Patch>(patch1, patch2, patch3));

            patchApplier.ApplyPatches();

            EnsureNoErrors();

            Received.InOrder(delegate
            {
                logger.Received().Log(LogType.Log, ":PASS1 pass");
                progress.Received().ApplyingUpdate(config1, patch1.urlConfig);
                progress.Received().PatchApplied();
                progress.Received().ApplyingCopy(config2, patch2.urlConfig);
                progress.Received().PatchApplied();
                progress.Received().ApplyingDelete(config3, patch3.urlConfig);
                progress.Received().PatchApplied();
                logger.Received().Log(LogType.Log, ":PASS2 pass");
                logger.Received().Log(LogType.Log, ":PASS3 pass");
            });

            UrlDir.UrlConfig[] allConfigs = databaseRoot.AllConfigs.ToArray();
            Assert.Equal(4, allConfigs.Length);

            AssertNodesEqual(new TestConfigNode("PART")
            {
                { "name", "000" },
                { "aaa", "011" },
                { "eee", "012" },
            }, allConfigs[0].config);

            AssertNodesEqual(new TestConfigNode("PART")
            {
                { "name", "002" },
                { "bbb", "003" },
            }, allConfigs[1].config);

            AssertNodesEqual(new TestConfigNode("PORT")
            {
                { "name", "006" },
                { "ddd", "007" },
            }, allConfigs[2].config);

            AssertNodesEqual(new TestConfigNode("PART")
            {
                { "name", "022" },
                { "bbb", "013" },
                { "fff", "014" },
            }, allConfigs[3].config);
        }

        [Fact]
        public void TestApplyPatches__Name__Wildcard()
        {
            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig(new TestConfigNode("PART")
            {
                { "name", "000" },
                { "aaa", "001" },
            }, file);

            UrlDir.UrlConfig config2 = UrlBuilder.CreateConfig(new TestConfigNode("PART")
            {
                { "name", "002" },
                { "bbb", "003" },
            }, file);

            UrlDir.UrlConfig config3 = UrlBuilder.CreateConfig(new TestConfigNode("PART")
            {
                { "name", "004" },
                { "ccc", "005" },
            }, file);

            UrlDir.UrlConfig config4 = UrlBuilder.CreateConfig(new TestConfigNode("PORT")
            {
                { "name", "006" },
                { "ddd", "007" },
            }, file);

            Patch patch1 = CreatePatch(Command.Edit, new TestConfigNode("@PART[0*0]")
            {
                { "@aaa", "011" },
                { "eee", "012" },
            });

            Patch patch2 = CreatePatch(Command.Copy, new TestConfigNode("+PART[0*2]")
            {
                { "@name", "022" },
                { "@bbb", "013" },
                { "fff", "014" },
            });

            Patch patch3 = CreatePatch(Command.Delete, new TestConfigNode("!PART[0*4]"));

            pass1.GetEnumerator().Returns(new ArrayEnumerator<Patch>(patch1, patch2, patch3));

            patchApplier.ApplyPatches();

            EnsureNoErrors();

            Received.InOrder(delegate
            {
                logger.Received().Log(LogType.Log, ":PASS1 pass");
                progress.Received().ApplyingUpdate(config1, patch1.urlConfig);
                progress.Received().PatchApplied();
                progress.Received().ApplyingCopy(config2, patch2.urlConfig);
                progress.Received().PatchApplied();
                progress.Received().ApplyingDelete(config3, patch3.urlConfig);
                progress.Received().PatchApplied();
                logger.Received().Log(LogType.Log, ":PASS2 pass");
                logger.Received().Log(LogType.Log, ":PASS3 pass");
            });

            UrlDir.UrlConfig[] allConfigs = databaseRoot.AllConfigs.ToArray();
            Assert.Equal(4, allConfigs.Length);

            AssertNodesEqual(new TestConfigNode("PART")
            {
                { "name", "000" },
                { "aaa", "011" },
                { "eee", "012" },
            }, allConfigs[0].config);

            AssertNodesEqual(new TestConfigNode("PART")
            {
                { "name", "002" },
                { "bbb", "003" },
            }, allConfigs[1].config);

            AssertNodesEqual(new TestConfigNode("PORT")
            {
                { "name", "006" },
                { "ddd", "007" },
            }, allConfigs[2].config);

            AssertNodesEqual(new TestConfigNode("PART")
            {
                { "name", "022" },
                { "bbb", "013" },
                { "fff", "014" },
            }, allConfigs[3].config);
        }

        [Fact]
        public void TestApplyPatches__Name__Or()
        {
            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig(new TestConfigNode("PART")
            {
                { "name", "000" },
                { "aaa", "001" },
            }, file);

            UrlDir.UrlConfig config2 = UrlBuilder.CreateConfig(new TestConfigNode("PART")
            {
                { "name", "002" },
                { "bbb", "003" },
            }, file);

            UrlDir.UrlConfig config3 = UrlBuilder.CreateConfig(new TestConfigNode("PART")
            {
                { "name", "004" },
                { "ccc", "005" },
            }, file);

            Patch patch1 = CreatePatch(Command.Edit, new TestConfigNode("@PART[000|0*2]")
            {
                { "@aaa", "011" },
                { "ddd", "006" },
            });

            pass1.GetEnumerator().Returns(new ArrayEnumerator<Patch>(patch1));

            patchApplier.ApplyPatches();

            EnsureNoErrors();

            Received.InOrder(delegate
            {
                logger.Received().Log(LogType.Log, ":PASS1 pass");
                progress.Received().ApplyingUpdate(config1, patch1.urlConfig);
                progress.Received().ApplyingUpdate(config2, patch1.urlConfig);
                progress.Received().PatchApplied();
                logger.Received().Log(LogType.Log, ":PASS2 pass");
                logger.Received().Log(LogType.Log, ":PASS3 pass");
            });

            UrlDir.UrlConfig[] allConfigs = databaseRoot.AllConfigs.ToArray();
            Assert.Equal(3, allConfigs.Length);

            AssertNodesEqual(new TestConfigNode("PART")
            {
                { "name", "000" },
                { "aaa", "011" },
                { "ddd", "006" },
            }, allConfigs[0].config);

            AssertNodesEqual(new TestConfigNode("PART")
            {
                { "name", "002" },
                { "bbb", "003" },
                { "ddd", "006" },
            }, allConfigs[1].config);

            AssertNodesEqual(new TestConfigNode("PART")
            {
                { "name", "004" },
                { "ccc", "005" },
            }, allConfigs[2].config);
        }

        [Fact]
        public void TestApplyPatches__Order()
        {
            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig(new TestConfigNode("PART")
            {
                { "name", "000" },
                { "aaa", "001" },
            }, file);
            
            Patch patch1 = CreatePatch(Command.Edit, new TestConfigNode("@PART[000|0*2]")
            {
                { "bbb", "002" },
            });

            Patch patch2 = CreatePatch(Command.Edit, new TestConfigNode("@PART[000|0*2]")
            {
                { "ccc", "003" },
            });

            Patch patch3 = CreatePatch(Command.Edit, new TestConfigNode("@PART[000|0*2]")
            {
                { "ddd", "004" },
            });

            Patch patch4 = CreatePatch(Command.Edit, new TestConfigNode("@PART[000|0*2]")
            {
                { "eee", "005" },
            });

            Patch patch5 = CreatePatch(Command.Edit, new TestConfigNode("@PART[000|0*2]")
            {
                { "fff", "006" },
            });

            Patch patch6 = CreatePatch(Command.Edit, new TestConfigNode("@PART[000|0*2]")
            {
                { "ggg", "007" },
            });

            Patch patch7 = CreatePatch(Command.Edit, new TestConfigNode("@PART[000|0*2]")
            {
                { "hhh", "008" },
            });

            Patch patch8 = CreatePatch(Command.Edit, new TestConfigNode("@PART[000|0*2]")
            {
                { "iii", "009" },
            });

            Patch patch9 = CreatePatch(Command.Edit, new TestConfigNode("@PART[000|0*2]")
            {
                { "jjj", "010" },
            });

            pass1.GetEnumerator().Returns(new ArrayEnumerator<Patch>(patch1, patch2, patch3));
            pass2.GetEnumerator().Returns(new ArrayEnumerator<Patch>(patch4, patch5, patch6));
            pass3.GetEnumerator().Returns(new ArrayEnumerator<Patch>(patch7, patch8, patch9));

            patchApplier.ApplyPatches();

            EnsureNoErrors();

            Received.InOrder(delegate
            {
                logger.Received().Log(LogType.Log, ":PASS1 pass");
                progress.Received().ApplyingUpdate(config1, patch1.urlConfig);
                progress.Received().PatchApplied();
                progress.Received().ApplyingUpdate(config1, patch2.urlConfig);
                progress.Received().PatchApplied();
                progress.Received().ApplyingUpdate(config1, patch3.urlConfig);
                progress.Received().PatchApplied();
                logger.Received().Log(LogType.Log, ":PASS2 pass");
                progress.Received().ApplyingUpdate(config1, patch4.urlConfig);
                progress.Received().PatchApplied();
                progress.Received().ApplyingUpdate(config1, patch5.urlConfig);
                progress.Received().PatchApplied();
                progress.Received().ApplyingUpdate(config1, patch6.urlConfig);
                progress.Received().PatchApplied();
                logger.Received().Log(LogType.Log, ":PASS3 pass");
                progress.Received().ApplyingUpdate(config1, patch7.urlConfig);
                progress.Received().PatchApplied();
                progress.Received().ApplyingUpdate(config1, patch8.urlConfig);
                progress.Received().PatchApplied();
                progress.Received().ApplyingUpdate(config1, patch9.urlConfig);
                progress.Received().PatchApplied();
            });

            UrlDir.UrlConfig[] allConfigs = databaseRoot.AllConfigs.ToArray();
            Assert.Equal(1, allConfigs.Length);

            AssertNodesEqual(new TestConfigNode("PART")
            {
                { "name", "000" },
                { "aaa", "001" },
                { "bbb", "002" },
                { "ccc", "003" },
                { "ddd", "004" },
                { "eee", "005" },
                { "fff", "006" },
                { "ggg", "007" },
                { "hhh", "008" },
                { "iii", "009" },
                { "jjj", "010" },
            }, allConfigs[0].config);
        }

        [Fact]
        public void TestApplyPatches__Constraints()
        {
            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig(new TestConfigNode("PART")
            {
                { "name", "000" },
                { "aaa", "001" },
            }, file);

            UrlDir.UrlConfig config2 = UrlBuilder.CreateConfig(new TestConfigNode("PART")
            {
                { "name", "002" },
                { "bbb", "003" },
            }, file);

            UrlDir.UrlConfig config3 = UrlBuilder.CreateConfig(new TestConfigNode("PART")
            {
                { "name", "004" },
                { "ccc", "005" },
            }, file);

            UrlDir.UrlConfig config4 = UrlBuilder.CreateConfig(new TestConfigNode("PORT")
            {
                { "name", "006" },
                { "ddd", "007" },
            }, file);

            Patch patch1 = CreatePatch(Command.Edit, new TestConfigNode("@PART:HAS[#aaa[001]]")
            {
                { "@aaa", "011" },
                { "eee", "012" },
            });

            Patch patch2 = CreatePatch(Command.Copy, new TestConfigNode("+PART:HAS[#bbb[003]]")
            {
                { "@name", "012" },
                { "@bbb", "013" },
                { "fff", "014" },
            });

            Patch patch3 = CreatePatch(Command.Delete, new TestConfigNode("!PART:HAS[#ccc[005]]"));

            pass1.GetEnumerator().Returns(new ArrayEnumerator<Patch>(patch1, patch2, patch3));

            patchApplier.ApplyPatches();

            EnsureNoErrors();

            Received.InOrder(delegate
            {
                logger.Received().Log(LogType.Log, ":PASS1 pass");
                progress.Received().ApplyingUpdate(config1, patch1.urlConfig);
                progress.Received().PatchApplied();
                progress.Received().ApplyingCopy(config2, patch2.urlConfig);
                progress.Received().PatchApplied();
                progress.Received().ApplyingDelete(config3, patch3.urlConfig);
                progress.Received().PatchApplied();
                logger.Received().Log(LogType.Log, ":PASS2 pass");
                logger.Received().Log(LogType.Log, ":PASS3 pass");
            });

            UrlDir.UrlConfig[] allConfigs = databaseRoot.AllConfigs.ToArray();
            Assert.Equal(4, allConfigs.Length);

            AssertNodesEqual(new TestConfigNode("PART")
            {
                { "name", "000" },
                { "aaa", "011" },
                { "eee", "012" },
            }, allConfigs[0].config);

            AssertNodesEqual(new TestConfigNode("PART")
            {
                { "name", "002" },
                { "bbb", "003" },
            }, allConfigs[1].config);

            AssertNodesEqual(new TestConfigNode("PORT")
            {
                { "name", "006" },
                { "ddd", "007" },
            }, allConfigs[2].config);

            AssertNodesEqual(new TestConfigNode("PART")
            {
                { "name", "012" },
                { "bbb", "013" },
                { "fff", "014" },
            }, allConfigs[3].config);
        }

        [Fact]
        public void TestApplyPatches__Loop()
        {
            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig(new TestConfigNode("PART")
            {
                { "name", "000" },
                { "aaa", "1" },
            }, file);

            Patch patch1 = CreatePatch(Command.Edit, new TestConfigNode("@PART:HAS[~aaa[>10]]")
            {
                { "@aaa *", "2" },
                { "bbb", "002" },
                new ConfigNode("MM_PATCH_LOOP"),
            });

            pass1.GetEnumerator().Returns(new ArrayEnumerator<Patch>(patch1));

            patchApplier.ApplyPatches();

            EnsureNoErrors();

            Received.InOrder(delegate
            {
                logger.Received().Log(LogType.Log, ":PASS1 pass");
                logger.Received().Log(LogType.Log, "Looping on abc/def/@PART:HAS[~aaa[>10]] to abc/def/PART");
                progress.Received().ApplyingUpdate(config1, patch1.urlConfig);
                progress.Received().ApplyingUpdate(config1, patch1.urlConfig);
                progress.Received().ApplyingUpdate(config1, patch1.urlConfig);
                progress.Received().ApplyingUpdate(config1, patch1.urlConfig);
                progress.Received().PatchApplied();
                logger.Received().Log(LogType.Log, ":PASS2 pass");
                logger.Received().Log(LogType.Log, ":PASS3 pass");
            });

            UrlDir.UrlConfig[] allConfigs = databaseRoot.AllConfigs.ToArray();
            Assert.Equal(1, allConfigs.Length);

            AssertNodesEqual(new TestConfigNode("PART")
            {
                { "name", "000" },
                { "aaa", "16" },
                { "bbb", "002" },
                { "bbb", "002" },
                { "bbb", "002" },
                { "bbb", "002" },
            }, allConfigs[0].config);
        }

        [Fact]
        public void TestApplyPatches__Copy__NameNotChanged()
        {
            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig(new TestConfigNode("PART")
            {
                { "name", "000" },
                { "aaa", "001" },
            }, file);

            Patch patch1 = CreatePatch(Command.Copy, new TestConfigNode("+PART")
            {
                { "@aaa", "011" },
                { "bbb", "012" },
            });

            pass1.GetEnumerator().Returns(new ArrayEnumerator<Patch>(patch1));

            patchApplier.ApplyPatches();
            
            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);

            logger.DidNotReceiveWithAnyArgs().Exception(null, null);
            
            progress.DidNotReceiveWithAnyArgs().ApplyingUpdate(null, null);
            progress.DidNotReceiveWithAnyArgs().ApplyingCopy(null, null);
            progress.DidNotReceiveWithAnyArgs().ApplyingDelete(null, null);

            Received.InOrder(delegate
            {
                logger.Received().Log(LogType.Log, ":PASS1 pass");
                progress.Received().Error(patch1.urlConfig, "Error - when applying copy abc/def/+PART to abc/def/PART - the copy needs to have a different name than the parent (use @name = xxx)");
                progress.Received().PatchApplied();
                logger.Received().Log(LogType.Log, ":PASS2 pass");
                logger.Received().Log(LogType.Log, ":PASS3 pass");
            });

            UrlDir.UrlConfig[] allConfigs = databaseRoot.AllConfigs.ToArray();
            Assert.Equal(1, allConfigs.Length);

            AssertNodesEqual(new TestConfigNode("PART")
            {
                { "name", "000" },
                { "aaa", "001" },
            }, allConfigs[0].config);
        }

        private void EnsureNoErrors()
        {
            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);

            logger.DidNotReceive().Log(LogType.Warning, Arg.Any<string>());
            logger.DidNotReceive().Log(LogType.Error, Arg.Any<string>());
            logger.DidNotReceiveWithAnyArgs().Exception(null, null);
        }

        private void AssertNodesEqual(ConfigNode expected, ConfigNode actual)
        {
            Assert.Equal(expected.ToString(), actual.ToString());
        }

        private Patch CreatePatch(Command command, ConfigNode node)
        {
            ConfigNode newNode = node;
            if (command != Command.Insert)
            {
                newNode = new ConfigNode(node.name.Substring(1));
                newNode.ShallowCopyFrom(node);
            }
            return new Patch(new UrlDir.UrlConfig(file, node), command, newNode);
        }
    }
}
