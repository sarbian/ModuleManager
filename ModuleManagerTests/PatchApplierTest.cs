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

        [Fact]
        public void TestApplyPatches__NameIndex()
        {
            // Edits whose names have no wildcard find nodes by name; renames by edits and by
            // special commands must be seen by the patches that follow, and by the same patch.
            IBasicLogger logger = Substitute.For<IBasicLogger>();
            IPatchProgress progress = Substitute.For<IPatchProgress>();
            PatchApplier patchApplier = new PatchApplier(progress, logger);
            IPassSpecifier passSpecifier = Substitute.For<IPassSpecifier>();

            IPatch Insert(string type, string name) => new InsertPatch(
                UrlBuilder.CreateConfig("abc/" + name, new TestConfigNode(type) { { "name", name } }), type, passSpecifier);
            IPatch Edit(string url, string name, ConfigNode node) => new EditPatch(UrlBuilder.CreateConfig(url, node), new NodeMatcher("PART", name, null), passSpecifier);

            IPass pass1 = Substitute.For<IPass>();
            pass1.GetEnumerator().Returns(new ArrayEnumerator<IPatch>(
                Insert("PART", "a"),
                Insert("PART", "b"),
                Insert("PART", "c"),
                Insert("PART", "r")
            ));

            IPass pass2 = Substitute.For<IPass>();
            pass2.GetEnumerator().Returns(new ArrayEnumerator<IPatch>(
                Edit("ghi/e1", "a", new TestConfigNode("@PART[a]") { { "x", "1" } }),
                // rename b to a: the next edit of a sees both
                Edit("ghi/e2", "b", new TestConfigNode("@PART[b]") { { "@name", "a" } }),
                Edit("ghi/e3", "a", new TestConfigNode("@PART[a]") { { "y", "2" } }),
                Edit("ghi/e4", "a,c", new TestConfigNode("@PART[a,c]") { { "z", "3" } }),
                // renames r to w in place while it runs: then w matches too, after c
                Edit("ghi/e5", "c|w", new TestConfigNode("@PART[c|w]") { { "*@PART[r]/name", "w" }, { "w", "4" } }),
                Edit("ghi/e6", "w", new TestConfigNode("@PART[w]") { { "v", "5" } }),
                Edit("ghi/e7", "r", new TestConfigNode("@PART[r]") { { "u", "6" } })
            ));

            IProtoUrlConfig[] configs = patchApplier.ApplyPatches(new[] { pass1, pass2 }).ToArray();

            // e5 on w cannot find PART[r] any more
            progress.ReceivedWithAnyArgs(1).Error(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);

            Assert.Equal(4, configs.Length);
            AssertNodesEqual(new TestConfigNode("PART") { { "name", "a" }, { "x", "1" }, { "y", "2" }, { "z", "3" } }, configs[0].Node);
            AssertNodesEqual(new TestConfigNode("PART") { { "name", "a" }, { "y", "2" }, { "z", "3" } }, configs[1].Node);
            AssertNodesEqual(new TestConfigNode("PART") { { "name", "c" }, { "z", "3" }, { "w", "4" } }, configs[2].Node);
            AssertNodesEqual(new TestConfigNode("PART") { { "name", "w" }, { "w", "4" }, { "v", "5" } }, configs[3].Node);
        }

        [Fact]
        public void TestApplyPatches__RootLookups()
        {
            // #$@TYPE[name]/key$ finds the first root node of that type and name, in list order,
            // as the database stands when the value is read, renames included
            IBasicLogger logger = Substitute.For<IBasicLogger>();
            IPatchProgress progress = Substitute.For<IPatchProgress>();
            PatchApplier patchApplier = new PatchApplier(progress, logger);
            IPassSpecifier passSpecifier = Substitute.For<IPassSpecifier>();

            IPatch Insert(string name, string x) => new InsertPatch(
                UrlBuilder.CreateConfig("abc/" + name, new TestConfigNode("PART") { { "name", name }, { "x", x } }), "PART", passSpecifier);
            IPatch Edit(string url, string name, ConfigNode node) => new EditPatch(UrlBuilder.CreateConfig(url, node), new NodeMatcher("PART", name, null), passSpecifier);

            IPass pass1 = Substitute.For<IPass>();
            pass1.GetEnumerator().Returns(new ArrayEnumerator<IPatch>(
                Insert("a", "1"),
                Insert("b", "2"),
                Insert("c", "3")
            ));

            IPass pass2 = Substitute.For<IPass>();
            pass2.GetEnumerator().Returns(new ArrayEnumerator<IPatch>(
                Edit("ghi/e1", "c", new TestConfigNode("@PART[c]") { { "fromB", "#$@PART[b]/x$" }, { "fromAny", "#$@PART[?]/x$" } }),
                // a is renamed b while the patch runs: c, after it, reads the renamed node
                Edit("ghi/e2", "a|c", new TestConfigNode("@PART[a|c]") { { "@name", "b" }, { "fromB2", "#$@PART[b]/x$" } }),
                Edit("ghi/e3", "b", new TestConfigNode("@PART[b]") { { "fromB3", "#$@PART[b*]/x$" } })
            ));

            IProtoUrlConfig[] configs = patchApplier.ApplyPatches(new[] { pass1, pass2 }).ToArray();

            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);

            AssertNodesEqual(new TestConfigNode("PART") { { "name", "b" }, { "x", "1" }, { "fromB2", "2" }, { "fromB3", "1" } }, configs[0].Node);
            AssertNodesEqual(new TestConfigNode("PART") { { "name", "b" }, { "x", "2" }, { "fromB3", "1" } }, configs[1].Node);
            AssertNodesEqual(new TestConfigNode("PART") { { "name", "b" }, { "x", "3" }, { "fromB", "2" }, { "fromAny", "1" }, { "fromB2", "1" }, { "fromB3", "1" } }, configs[2].Node);
        }

        [Fact]
        public void TestApplyPatches__LoopRenameRootLookups()
        {
            // An MM_PATCH_LOOP pass that renames the node is seen by the root lookups of the next
            // pass, and by the patches after the loop, even when a later pass renames it back
            IBasicLogger logger = Substitute.For<IBasicLogger>();
            IPatchProgress progress = Substitute.For<IPatchProgress>();
            PatchApplier patchApplier = new PatchApplier(progress, logger);
            IPassSpecifier passSpecifier = Substitute.For<IPassSpecifier>();

            IPatch Insert(ConfigNode node) => new InsertPatch(UrlBuilder.CreateConfig("abc/" + node.GetValue("name"), node), "PART", passSpecifier);
            IPatch Edit(string url, string name, string constraints, ConfigNode node) => new EditPatch(UrlBuilder.CreateConfig(url, node), new NodeMatcher("PART", name, constraints), passSpecifier);

            IPass pass1 = Substitute.For<IPass>();
            pass1.GetEnumerator().Returns(new ArrayEnumerator<IPatch>(
                Insert(new TestConfigNode("PART") { { "name", "a" }, { "n", "0" } }),
                Insert(new TestConfigNode("PART") { { "name", "b" }, { "n", "0" }, { "alt", "d" } })
            ));

            IPass pass2 = Substitute.For<IPass>();
            pass2.GetEnumerator().Returns(new ArrayEnumerator<IPatch>(
                // first pass: reads a (itself) and renames it c; second pass: there is no a any more
                Edit("ghi/l1", "a|c", "#n[<2]", new TestConfigNode("@PART[a|c]:HAS[#n[<2]]")
                {
                    { "@n +", "1" },
                    { "%seen", "#$@PART[a]/n$" },
                    { "@name", "c" },
                    new ConfigNode("MM_PATCH_LOOP"),
                }),
                // first pass renames b to d; the second looks up a root while the node is d,
                // then renames it back to b
                Edit("ghi/l2", "b|d", "#n[<2]", new TestConfigNode("@PART[b|d]:HAS[#n[<2]]")
                {
                    new TestConfigNode("@MARK") { { "%v", "#$@PART[c]/n$" } },
                    new ConfigNode("%MARK"),
                    { "@n +", "1" },
                    { "%tmp", "#$name$" },
                    { "@name", "#$alt$" },
                    { "@alt", "#$tmp$" },
                    new ConfigNode("MM_PATCH_LOOP"),
                }),
                Edit("ghi/e1", "b", null, new TestConfigNode("@PART[b]") { { "after", "1" } }),
                Edit("ghi/e2", "a", null, new TestConfigNode("@PART[a]") { { "after", "2" } })
            ));

            IProtoUrlConfig[] configs = patchApplier.ApplyPatches(new[] { pass1, pass2 }).ToArray();

            // l1's second pass cannot find PART[a]
            progress.ReceivedWithAnyArgs(1).Error(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);

            AssertNodesEqual(new TestConfigNode("PART") { { "name", "c" }, { "n", "2" }, { "seen", "0" } }, configs[0].Node);
            Assert.Equal("b", configs[1].Node.GetValue("name"));
            Assert.Equal("2", configs[1].Node.GetValue("n"));
            Assert.Equal("2", configs[1].Node.GetNode("MARK").GetValue("v"));
            Assert.Equal("1", configs[1].Node.GetValue("after"));
        }

        [Fact]
        public void TestApplyPatches__IndexedMatchesPlain()
        {
            // Copies, deletes and edits through the index give the same nodes, in the same order,
            // with the same errors, as each patch's plain Apply over the whole list
            IPassSpecifier passSpecifier = Substitute.For<IPassSpecifier>();

            IPatch[] MakePatches()
            {
                IPatch Insert(string type, string name, string x) => new InsertPatch(
                    UrlBuilder.CreateConfig("abc/" + name, new TestConfigNode(type) { { "name", name }, { "x", x } }), type, passSpecifier);
                IPatch Copy(string url, string name, string constraints, ConfigNode node) => new CopyPatch(UrlBuilder.CreateConfig(url, node), new NodeMatcher("PART", name, constraints), passSpecifier);
                IPatch Delete(string url, string name, string constraints) => new DeletePatch(UrlBuilder.CreateConfig(url, new ConfigNode("!PART")), new NodeMatcher("PART", name, constraints), passSpecifier);
                IPatch Edit(string url, string name, string constraints, ConfigNode node) => new EditPatch(UrlBuilder.CreateConfig(url, node), new NodeMatcher("PART", name, constraints), passSpecifier);

                return new[]
                {
                    Insert("PART", "a", "1"),
                    Insert("PROP", "a", "2"),
                    Insert("PART", "b", "3"),
                    Insert("PART", "a", "4"),
                    Insert("PART", "c", "5"),
                    Insert("PART", "d", "6"),
                    Copy("ghi/c1", "a", null, new TestConfigNode("+PART[a]") { { "@name", "a2" }, { "@x", "#$x$0" } }),
                    Copy("ghi/c2", "b,c", null, new TestConfigNode("+PART[b,c]") { { "@name", "#$name$_copy" }, { "fromA2", "#$@PART[a2]/x$" } }),
                    // fails: same name as the source
                    Copy("ghi/c3", "d", null, new TestConfigNode("+PART[d]") { { "y", "1" } }),
                    Edit("ghi/e1", "a2", null, new TestConfigNode("@PART[a2]") { { "edited", "1" } }),
                    // renames d to e in place while copying c: e, not yet reached, is then copied too
                    Copy("ghi/c4", "c|e", null, new TestConfigNode("+PART[c|e]") { { "*@PART[d]/name", "e" }, { "@name", "#$name$_c4" } }),
                    Delete("ghi/d1", "a2", null),
                    Delete("ghi/d2", "*", "#x[5*]"),
                    Delete("ghi/d3", "nothere", null),
                    Copy("ghi/c5", "*_copy", null, new TestConfigNode("+PART[*_copy]") { { "@name ^", ":_copy$:_again:" } }),
                    Edit("ghi/e2", "*", null, new TestConfigNode("@PART[*]") { { "last", "#$name$" } }),
                    Delete("ghi/d4", "b", null),
                    Edit("ghi/e3", "b|e", null, new TestConfigNode("@PART[b|e]") { { "after", "1" } }),
                };
            }

            string Describe(NSubstitute.Core.ICall call) => call.GetMethodInfo().Name + ":" + string.Join("|", call.GetArguments().Select(a => a is IProtoUrlConfig p ? p.Node.ToString() : a?.ToString()));

            IPatchProgress plainProgress = Substitute.For<IPatchProgress>();
            LinkedList<IProtoUrlConfig> plain = new LinkedList<IProtoUrlConfig>();
            foreach (IPatch patch in MakePatches())
                patch.Apply(plain, plainProgress, Substitute.For<IBasicLogger>());

            IPatchProgress progress = Substitute.For<IPatchProgress>();
            PatchApplier patchApplier = new PatchApplier(progress, Substitute.For<IBasicLogger>());
            IPass pass = Substitute.For<IPass>();
            pass.GetEnumerator().Returns(new ArrayEnumerator<IPatch>(MakePatches()));
            IProtoUrlConfig[] configs = patchApplier.ApplyPatches(new[] { pass }).ToArray();

            Assert.Equal(plain.Select(c => c.Node.ToString()).ToArray(), configs.Select(c => c.Node.ToString()).ToArray());
            Assert.Equal(
                plainProgress.ReceivedCalls().Select(Describe).ToArray(),
                progress.ReceivedCalls().Where(c => c.GetMethodInfo().Name != "PatchApplied" && c.GetMethodInfo().Name != "PassStarted").Select(Describe).ToArray());
            Assert.Equal(1, configs.Count(c => c.Node.name == "PROP"));
            Assert.Contains(configs, c => c.Node.GetValue("name") == "e_c4");
            Assert.Contains(configs, c => c.Node.GetValue("name") == "b_again");
        }

        // Records the text of every root node, and checks that the ones the patch before replaced
        // are as they were: edits must not change the versions they replace. (Later, a special
        // command can write into a subnode a replaced version shares with the live one, which
        // nothing reads.)
        private class Snapshots
        {
            // the root nodes in the list at the last snapshot, and their text
            public readonly List<KeyValuePair<ConfigNode, string>> Seen = new List<KeyValuePair<ConfigNode, string>>();
            public int Checked;
        }

        private class SnapshotPatch : IPatch
        {
            private readonly Snapshots snapshots;
            public SnapshotPatch(Snapshots snapshots) { this.snapshots = snapshots; }
            public UrlDir.UrlConfig UrlConfig { get; } = UrlBuilder.CreateConfig("snap/shot", new ConfigNode("SNAPSHOT"));
            public IPassSpecifier PassSpecifier => null;
            public bool CountsAsPatch => false;
            public void Apply(LinkedList<IProtoUrlConfig> configs, IPatchProgress progress, IBasicLogger logger)
            {
                HashSet<ConfigNode> live = new HashSet<ConfigNode>(configs.Select(c => c.Node));
                foreach (KeyValuePair<ConfigNode, string> pair in snapshots.Seen)
                {
                    if (live.Contains(pair.Key)) continue;
                    Assert.Equal(pair.Value, pair.Key.ToString());
                    snapshots.Checked++;
                }
                snapshots.Seen.Clear();
                foreach (IProtoUrlConfig config in configs)
                    snapshots.Seen.Add(new KeyValuePair<ConfigNode, string>(config.Node, config.Node.ToString()));
            }
        }

        [Fact]
        public void TestApplyPatches__SharedSubnodesMatchCopies()
        {
            // Edits that share the subnodes they leave alone with the node they replace give the
            // same nodes and errors as edits of whole copies, and never change a replaced node
            IPassSpecifier passSpecifier = Substitute.For<IPassSpecifier>();

            IPatch[] MakePatches(Snapshots snapshots)
            {
                IPatch Insert(ConfigNode node) => new InsertPatch(UrlBuilder.CreateConfig("abc/" + node.GetValue("name"), node), "PART", passSpecifier);
                IPatch Edit(string url, string name, string constraints, ConfigNode node) => new EditPatch(UrlBuilder.CreateConfig(url, node), new NodeMatcher("PART", name, constraints), passSpecifier);

                List<IPatch> patches = new List<IPatch>
                {
                    Insert(new TestConfigNode("PART")
                    {
                        { "name", "a" }, { "mass", "1" },
                        new TestConfigNode("MODULE") { { "name", "M1" }, { "x", "1" }, new TestConfigNode("SUB") { { "y", "1" } } },
                        new TestConfigNode("MODULE") { { "name", "M2" }, { "x", "2" } },
                        new TestConfigNode("MODULE") { { "name", "M3" }, { "x", "3" } },
                        new TestConfigNode("RES") { { "name", "R" }, { "amount", "5" } },
                    }),
                    Insert(new TestConfigNode("PART")
                    {
                        { "name", "b" },
                        new TestConfigNode("MODULE") { { "name", "M1" }, { "x", "10" } },
                        new TestConfigNode("MODULE") { { "name", "M1" }, { "x", "11" } },
                    }),
                    Insert(new TestConfigNode("PART") { { "name", "c" }, { "foo", "1" } }),
                    // a nested edit
                    Edit("e/1", "a", null, new TestConfigNode("@PART[a]") { new TestConfigNode("@MODULE[M1]") { { "@x", "5" }, new TestConfigNode("@SUB") { { "y", "2" } } } }),
                    // the same subnode edited twice in one patch
                    Edit("e/2", "a", null, new TestConfigNode("@PART[a]") { new TestConfigNode("@MODULE[M1]") { { "@x +", "1" } }, new TestConfigNode("@MODULE[M1]") { { "z", "1" } } }),
                    // every MODULE of every part that has an M1
                    Edit("e/3", "*", "@MODULE[M1]", new TestConfigNode("@PART[*]:HAS[@MODULE[M1]]") { new TestConfigNode("@MODULE,*") { { "touched", "#$name$" } } }),
                    // replace an existing subnode and a missing one
                    Edit("e/4", "a", null, new TestConfigNode("@PART[a]") { new TestConfigNode("%MODULE[M2]") { { "%x", "9" } }, new TestConfigNode("%MODULE[M9]") { { "x", "1" } } }),
                    // delete and copy
                    Edit("e/5", "a", null, new TestConfigNode("@PART[a]") { new TestConfigNode("!MODULE[M3]"), new TestConfigNode("+MODULE[M1]") { { "@name", "M1copy" } } }),
                    // a negative index lists the last subnode twice
                    Edit("e/6", "b", null, new TestConfigNode("@PART[b]") { new TestConfigNode("@MODULE,-1") { { "last +", "1" } } }),
                    // a subnode inserted, then edited
                    Edit("e/7", "c", null, new TestConfigNode("@PART[c]") { new TestConfigNode("MODULE") { { "name", "New" }, { "edited", "0" } }, new TestConfigNode("@MODULE[New]") { { "@edited +", "1" } } }),
                    // a loop
                    Edit("e/8", "c", "#foo[<3]", new TestConfigNode("@PART[c]:HAS[#foo[<3]]") { { "@foo +", "1" }, new TestConfigNode("@MODULE[New]") { { "@edited +", "1" } }, new ConfigNode("MM_PATCH_LOOP") }),
                    // a special command, writing into another part's subnode
                    Edit("e/9", "a", null, new TestConfigNode("@PART[a]") { { "*@PART[b]/MODULE[M1]/x", "99" }, new TestConfigNode("@MODULE[M2]") { { "s", "1" } } }),
                    Edit("e/10", "b", null, new TestConfigNode("@PART[b]") { new TestConfigNode("@MODULE[M1]") { { "after", "1" } } }),
                    // fails part way: the regex does not parse
                    Edit("e/11", "b", null, new TestConfigNode("@PART[b]") { new TestConfigNode("@MODULE[M1]") { { "mid", "1" } }, new TestConfigNode("@MODULE[M1]") { { "@x ^", ":(:z:" } } }),
                    // a paste
                    Edit("e/12", "c", null, new TestConfigNode("@PART[c]") { new ConfigNode("#@PART[a]/RES[R]") }),
                    // a rename a whole copy cleans (DEEP (x) -> DEEP), two levels down, then a
                    // match on the cleaned name; and the same one level down
                    Edit("e/15", "a", null, new TestConfigNode("@PART[a]") { new TestConfigNode("@MODULE[M1]") { new TestConfigNode("@SUB") { { "|_", "DEEP (x)" } } } }),
                    Edit("e/16", "a", null, new TestConfigNode("@PART[a]") { { "touch", "1" } }),
                    Edit("e/17", "a", "@MODULE[M1]:HAS[@DEEP]", new TestConfigNode("@PART[a]:HAS[@MODULE[M1]:HAS[@DEEP]]") { { "foundDeep", "1" } }),
                    Edit("e/18", "a", null, new TestConfigNode("@PART[a]") { new TestConfigNode("@MODULE[M2]") { { "|_", "OLD MODULE" } } }),
                    Edit("e/19", "a", null, new TestConfigNode("@PART[a]") { new TestConfigNode("@OLD") { { "gotIt", "1" } } }),
                    // a special command writing a brace into a subnode, then an edit of its part
                    Edit("e/13", "c", null, new TestConfigNode("@PART[c]") { { "*@PART[a]/RES[R]/amount", "{q}" } }),
                    Edit("e/14", "a", null, new TestConfigNode("@PART[a]") { new TestConfigNode("@MODULE[M1]") { { "after", "1" } } }),
                };
                List<IPatch> withSnapshots = new List<IPatch>();
                foreach (IPatch patch in patches)
                {
                    withSnapshots.Add(new SnapshotPatch(snapshots));
                    withSnapshots.Add(patch);
                }
                return withSnapshots.ToArray();
            }

            // As the patch log has them: a node by its URL, taken when it was put in the list
            string Describe(NSubstitute.Core.ICall call) => call.GetMethodInfo().Name + ":" + string.Join("|", call.GetArguments().Select(a => a is IProtoUrlConfig p ? p.FullUrl : a is Exception e ? e.GetType().Name : a?.ToString()));

            (string[] nodes, string[] calls, int checkedVersions) Run(bool share)
            {
                bool before = EditPatch.ShareSubnodes;
                EditPatch.ShareSubnodes = share;
                try
                {
                    Snapshots snapshots = new Snapshots();
                    IPatchProgress progress = Substitute.For<IPatchProgress>();
                    PatchApplier patchApplier = new PatchApplier(progress, Substitute.For<IBasicLogger>());
                    IPass pass = Substitute.For<IPass>();
                    pass.GetEnumerator().Returns(new ArrayEnumerator<IPatch>(MakePatches(snapshots).Concat(new[] { new SnapshotPatch(snapshots) }).ToArray()));
                    IProtoUrlConfig[] configs = patchApplier.ApplyPatches(new[] { pass }).ToArray();
                    return (configs.Select(c => c.Node.ToString()).ToArray(), progress.ReceivedCalls().Where(c => c.GetMethodInfo().Name != "PassStarted").Select(Describe).ToArray(), snapshots.Checked);
                }
                finally
                {
                    EditPatch.ShareSubnodes = before;
                }
            }

            var copies = Run(false);
            var shared = Run(true);

            Assert.Equal(copies.nodes, shared.nodes);
            Assert.Equal(copies.calls, shared.calls);
            Assert.Contains(copies.calls, c => c.StartsWith("Exception:"));
            Assert.Contains("M1copy", shared.nodes[0]);
            Assert.Contains("[q]", shared.nodes[0]);
            Assert.Contains("foundDeep", shared.nodes[0]);
            Assert.Contains("gotIt", shared.nodes[0]);
            Assert.True(shared.checkedVersions > 10);
        }

        private void AssertNodesEqual(ConfigNode expected, ConfigNode actual)
        {
            Assert.Equal(expected.ToString(), actual.ToString());
        }
    }
}
