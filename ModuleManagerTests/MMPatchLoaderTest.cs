using System;
using System.Linq;
using Xunit;
using NSubstitute;
using UnityEngine;
using TestUtils;
using ModuleManager;
using ModuleManager.Logging;
using ModuleManager.Progress;
using NodeStack = ModuleManager.Collections.ImmutableStack<ConfigNode>;

namespace ModuleManagerTests
{
    // This is not intended to fully test ModifyNode, however it is useful to include tests for bugfixes here before it is split up
    public class MMPatchLoaderTest
    {
        private readonly IBasicLogger logger = Substitute.For<IBasicLogger>();
        private readonly IPatchProgress progress = Substitute.For<IPatchProgress>();

        [Fact]
        public void TestModifyNode__IndexAllWithAssign()
        {
            ConfigNode c1 = new TestConfigNode("NODE")
            {
                { "foo", "bar1" },
                { "foo", "bar2" },
            };

            UrlDir.UrlConfig c2u = UrlBuilder.CreateConfig("abc/def", new TestConfigNode("@NODE")
            {
                { "@foo,*", "bar3" },
            });
            
            PatchContext context = new PatchContext(c2u, Enumerable.Empty<IProtoUrlConfig>(), logger, progress);

            ConfigNode c3 = MMPatchLoader.ModifyNode(new NodeStack(c1), c2u.config, context);

            EnsureNoErrors();

            AssertConfigNodesEqual(new TestConfigNode("NODE")
            {
                { "foo", "bar3" },
                { "foo", "bar3" },
            }, c3);
        }

        [Fact]
        public void TestModifyNode__MultiplyValue()
        {
            ConfigNode c1 = new TestConfigNode("NODE")
            {
                { "foo", "3" },
                { "foo", "5" },
            };

            UrlDir.UrlConfig c2u = UrlBuilder.CreateConfig("abc/def", new TestConfigNode("@NODE")
            {
                { "@foo *", "2" },
            });

            PatchContext context = new PatchContext(c2u, Enumerable.Empty<IProtoUrlConfig>(), logger, progress);

            ConfigNode c3 = MMPatchLoader.ModifyNode(new NodeStack(c1), c2u.config, context);

            EnsureNoErrors();

            AssertConfigNodesEqual(new TestConfigNode("NODE")
            {
                { "foo", "6" },
                { "foo", "5" },
            }, c3);
        }

        [Fact]
        public void TestModifyNode__EditNode__SpecialCharacters()
        {
            ConfigNode c1 = new TestConfigNode("NODE")
            {
                new TestConfigNode("INNER_NODE")
                {
                    { "weird_values", "some\r\n\tstuff" },
                },
            };

            UrlDir.UrlConfig c2u = UrlBuilder.CreateConfig("abc/def", new TestConfigNode("@NODE")
            {
                new TestConfigNode("@INNER_NODE")
                {
                    { "another_weird_value", "some\r\nmore\tstuff" },
                },
            });

            PatchContext context = new PatchContext(c2u, Enumerable.Empty<IProtoUrlConfig>(), logger, progress);

            ConfigNode c3 = MMPatchLoader.ModifyNode(new NodeStack(c1), c2u.config, context);

            EnsureNoErrors();

            AssertConfigNodesEqual(new TestConfigNode("NODE")
            {
                new TestConfigNode("INNER_NODE")
                {
                    { "weird_values", "some\r\n\tstuff" },
                    { "another_weird_value", "some\r\nmore\tstuff" },
                },
            }, c3);
        }

        [Fact]
        public void TestModifyNode__ReplaceNode__SpecialCharacters()
        {
            ConfigNode c1 = new TestConfigNode("NODE")
            {
                new TestConfigNode("INNER_NODE")
                {
                    { "weird_values", "some\r\n\tstuff" },
                },
            };

            UrlDir.UrlConfig c2u = UrlBuilder.CreateConfig("abc/def", new TestConfigNode("@NODE")
            {
                new TestConfigNode("%INNER_NODE")
                {
                    { "another_weird_value", "some\r\nmore\tstuff" },
                },
                new TestConfigNode("%OTHER_INNER_NODE")
                {
                    { "another_weirder_value", "even\r\nmore\tstuff" },
                },
            });

            PatchContext context = new PatchContext(c2u, Enumerable.Empty<IProtoUrlConfig>(), logger, progress);

            ConfigNode c3 = MMPatchLoader.ModifyNode(new NodeStack(c1), c2u.config, context);

            EnsureNoErrors();

            AssertConfigNodesEqual(new TestConfigNode("NODE")
            {
                new TestConfigNode("INNER_NODE")
                {
                    { "weird_values", "some\r\n\tstuff" },
                    { "another_weird_value", "some\r\nmore\tstuff" },
                },
                new TestConfigNode("OTHER_INNER_NODE")
                {
                    { "another_weirder_value", "even\r\nmore\tstuff" },
                },
            }, c3);
        }

        [Theory]
        [InlineData("@MOD,*", null, "")]
        [InlineData("@MOD[b*],*", "b*", "")]
        [InlineData("@MOD[nope],*", "nope", "")]
        [InlineData("@MOD:HAS[#x[1]]", null, "#x[1]")]
        [InlineData("@MOD[b*]:HAS[#x[1]]", "b*", "#x[1]")]
        [InlineData("@MOD,{0}:HAS[#x[1]]", null, "#x[1]")]
        [InlineData("@MOD,{0}:HAS[#x]", null, "#x")]
        [InlineData("@MOD[b*],{0}:HAS[#x]", "b*", "#x")]
        [InlineData("@MOD[b*],{0}:HAS[~x[]]", "b*", "~x[]")]
        public void TestModifyNode__EditSubnodesFromIndex(string subName, string nodeName, string constraints)
        {
            ConfigNode Make() => new TestConfigNode("NODE")
            {
                new TestConfigNode("MOD") { { "name", "a1" }, { "x", "1" } },
                new TestConfigNode("OTHER") { { "name", "b0" }, { "x", "1" } },
                new TestConfigNode("MOD") { { "name", "b1" } },
                new TestConfigNode("MOD") { { "name", "b2" }, { "x", "1" } },
                new TestConfigNode("MOD") { { "name", "a2" }, { "x", "2" } },
                new TestConfigNode("MOD") { { "name", "b3" }, { "x", "1" } },
            };

            for (int index = -7; index <= 7; index++)
            {
                // The nodes the old loop over FindConfigNodeIn picked
                ConfigNode c1 = Make();
                bool[] expected = new bool[c1.nodes.Count];
                int i = subName.Contains("{0}") ? index : 0;
                ConfigNode n, last = null;
                while (true)
                {
                    n = MMPatchLoader.FindConfigNodeIn(c1, "MOD", nodeName, i++);
                    if (n == last || n == null)
                        break;
                    if (MMPatchLoader.CheckConstraints(n, constraints))
                    {
                        for (int j = 0; j < c1.nodes.Count; j++)
                            if (c1.nodes[j] == n) expected[j] = true;
                    }
                    last = n;
                }

                UrlDir.UrlConfig c2u = UrlBuilder.CreateConfig("abc/def", new TestConfigNode("@NODE")
                {
                    new TestConfigNode(string.Format(subName, index)) { { "marked", "true" } },
                });
                PatchContext context = new PatchContext(c2u, Enumerable.Empty<IProtoUrlConfig>(), logger, progress);

                ConfigNode c3 = MMPatchLoader.ModifyNode(new NodeStack(c1), c2u.config, context);

                for (int j = 0; j < expected.Length; j++)
                    Assert.True(expected[j] == c3.nodes[j].HasValue("marked"), $"{subName} index {index}: node {j}");
            }
            EnsureNoErrors();
        }

        private void AssertConfigNodesEqual(ConfigNode expected, ConfigNode observed)
        {
            Assert.Equal(expected.ToString(), observed.ToString());
        }

        private void EnsureNoErrors()
        {
            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);

            logger.AssertNoWarning();
            logger.AssertNoError();
            logger.AssertNoException();
        }
    }
}
