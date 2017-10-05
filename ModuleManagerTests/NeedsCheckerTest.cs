using System;
using System.Linq;
using Xunit;
using NSubstitute;
using TestUtils;
using ModuleManager;
using ModuleManager.Logging;
using ModuleManager.Progress;

namespace ModuleManagerTests
{
    public class NeedsCheckerTest
    {
        private UrlDir root;
        private UrlDir.UrlFile file;

        private IPatchProgress progress;
        private IBasicLogger logger;

        public NeedsCheckerTest()
        {
            root = UrlBuilder.CreateRoot();
            file = UrlBuilder.CreateFile("abc/def.cfg", root);

            progress = Substitute.For<IPatchProgress>();
            logger = Substitute.For<IBasicLogger>();
        }

        [Fact]
        public void TestCheckNeeds__Root()
        {
            string[] modList = { "mod1", "mod2" };

            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE"), file);
            UrlDir.UrlConfig config2 = UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE:NEEDS[mod1]"), file);
            UrlDir.UrlConfig config3 = UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE:needs[mod1]"), file);
            UrlDir.UrlConfig config4 = UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE:NEEDS[mod2]:AFTER[mod3]"), file);

            UrlDir.UrlConfig config5 = UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE:NEEDS[mod3]"), file);
            UrlDir.UrlConfig config6 = UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE:needs[mod3]"), file);
            UrlDir.UrlConfig config7 = UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE:NEEDS[mod3]:FOR[mod2]"), file);

            NeedsChecker.CheckNeeds(root, modList, progress, logger);

            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);
            progress.DidNotReceiveWithAnyArgs().Error(null, null);

            UrlDir.UrlConfig[] configs = root.AllConfigs.ToArray();
            Assert.Equal(4, configs.Length);
            
            Assert.Same(config1, configs[0]);
            AssertUrlCorrect("SOME_NODE", config2, configs[1]);
            AssertUrlCorrect("SOME_NODE", config3, configs[2]);
            AssertUrlCorrect("SOME_NODE:AFTER[mod3]", config4, configs[3]);

            progress.Received().NeedsUnsatisfiedRoot(config5);
            progress.Received().NeedsUnsatisfiedRoot(config6);
            progress.Received().NeedsUnsatisfiedRoot(config7);
        }

        [Fact]
        public void TestCheckNeeds__Root__AndOr()
        {
            string[] modList = { "mod1", "mod2" };

            UrlDir.UrlConfig noNeedsNode = UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE"), file);

            UrlDir.UrlConfig[] needsSatisfiedConfigs = new[] {
                UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE:NEEDS[mod1&mod2]"), file),
                UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE:NEEDS[mod1,mod2]"), file),
                UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE:NEEDS[mod1|mod2]"), file),
                UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE:NEEDS[mod1|mod3]"), file),
                UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE:NEEDS[mod1&mod2|mod3]"), file),
                UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE:NEEDS[mod1,mod2|mod3]"), file),
                UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE:NEEDS[mod1|mod3&mod1]"), file),
                UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE:NEEDS[mod1|mod,mod1]"), file),
            };

            UrlDir.UrlConfig[] needsUnsatisfiedConfigs = new[] {
                UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE:NEEDS[mod1&mod3]"), file),
                UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE:NEEDS[mod1,mod3]"), file),
                UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE:NEEDS[mod1&mod2&mod3]"), file),
                UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE:NEEDS[mod1,mod2,mod3]"), file),
                UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE:NEEDS[mod3|mod4]"), file),
                UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE:NEEDS[mod1|mod2&mod3]"), file),
                UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE:NEEDS[mod1|mod2,mod3]"), file),
                UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE:NEEDS[mod3&mod1|mod2]"), file),
                UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE:NEEDS[mod3,mod1|mod2]"), file),
            };

            NeedsChecker.CheckNeeds(root, modList, progress, logger);

            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);
            progress.DidNotReceiveWithAnyArgs().Error(null, null);

            UrlDir.UrlConfig[] configs = root.AllConfigs.ToArray();
            Assert.Equal(needsSatisfiedConfigs.Length + 1, configs.Length);

            Assert.Same(noNeedsNode, configs[0]);

            for (int i = 0; i < needsSatisfiedConfigs.Length; i++)
            {
                AssertUrlCorrect("SOME_NODE", needsSatisfiedConfigs[i], configs[i + 1]);
            }

            foreach (UrlDir.UrlConfig config in needsUnsatisfiedConfigs)
            {
                progress.Received().NeedsUnsatisfiedRoot(config);
            }
        }

        [Fact]
        public void TestCheckNeeds__Root__CaseInsensitive()
        {
            string[] modList = { "mod1", "mod2" };

            UrlDir.UrlConfig noNeedsNode = UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE"), file);

            UrlDir.UrlConfig[] needsSatisfiedConfigs = new[] {
                UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE:NEEDS[mod1]"), file),
                UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE:NEEDS[Mod1]"), file),
                UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE:NEEDS[MOD1]"), file),
            };

            UrlDir.UrlConfig[] needsUnsatisfiedConfigs = new[] {
                UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE:NEEDS[mod3]"), file),
                UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE:NEEDS[Mod3]"), file),
                UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE:NEEDS[MOD3]"), file),
            };

            NeedsChecker.CheckNeeds(root, modList, progress, logger);

            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);
            progress.DidNotReceiveWithAnyArgs().Error(null, null);

            UrlDir.UrlConfig[] configs = root.AllConfigs.ToArray();
            Assert.Equal(needsSatisfiedConfigs.Length + 1, configs.Length);

            Assert.Same(noNeedsNode, configs[0]);

            for (int i = 0; i < needsSatisfiedConfigs.Length; i++)
            {
                AssertUrlCorrect("SOME_NODE", needsSatisfiedConfigs[i], configs[i + 1]);
            }

            foreach (UrlDir.UrlConfig config in needsUnsatisfiedConfigs)
            {
                progress.Received().NeedsUnsatisfiedRoot(config);
            }
        }

        [Fact]
        public void TestCheckNeeds__Nested()
        {
            string[] modList = { "mod1", "mod2" };

            ConfigNode node = new TestConfigNode("SOME_NODE")
            {
                { "aa", "00" },
                { "bb:NEEDS[mod1]", "01" },
                { "cc:NEEDS[mod3]", "02" },
                new TestConfigNode("INNER_NODE_1")
                {
                    { "dd", "03" },
                    { "ee", "04" },
                    new TestConfigNode("INNER_INNER_NODE_1")
                    {
                        { "ff", "05" },
                    },
                },
                new TestConfigNode("INNER_NODE_2")
                {
                    { "gg:NEEDS[mod1]", "06" },
                    { "hh:NEEDS[mod3]", "07" },
                    { "ii", "08" },
                    new TestConfigNode("INNER_INNER_NODE_11")
                    {
                        { "jj", "09" },
                    },
                    new TestConfigNode("INNER_INNER_NODE_12:NEEDS[mod2]")
                    {
                        { "kk", "10" },
                    },
                    new TestConfigNode("INNER_INNER_NODE_12:NEEDS[mod3]")
                    {
                        { "ll", "11" },
                    },
                },
                new TestConfigNode("INNER_NODE_3:NEEDS[mod1]")
                {
                    { "mm:NEEDS[mod1]", "12" },
                    { "nn:NEEDS[mod3]", "13" },
                    { "oo", "14" },
                    new TestConfigNode("INNER_INNER_NODE_21")
                    {
                        { "pp", "15" },
                    },
                    new TestConfigNode("INNER_INNER_NODE_22:NEEDS[mod2]")
                    {
                        { "qq", "16" },
                    },
                    new TestConfigNode("INNER_INNER_NODE_22:NEEDS[mod3]")
                    {
                        { "rr", "17" },
                    },
                },
                new TestConfigNode("INNER_NODE_4:NEEDS[mod3]")
                {
                    { "ss:NEEDS[mod1]", "18" },
                },
            };

            UrlDir.UrlConfig origUrl = UrlBuilder.CreateConfig(node, file);

            NeedsChecker.CheckNeeds(root, modList, progress, logger);

            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);
            progress.DidNotReceiveWithAnyArgs().Error(null, null);

            UrlDir.UrlConfig[] configs = root.AllConfigs.ToArray();
            Assert.Equal(1, configs.Length);

            UrlDir.UrlConfig url = configs[0];
            Assert.Equal("SOME_NODE", url.type);
            ConfigNode newNode = url.config;
            Assert.Equal("SOME_NODE", newNode.name);

            Assert.Equal(2, newNode.values.Count);
            Assert.Equal(3, newNode.nodes.Count);
            
            Assert.Equal("aa", newNode.values[0].name);
            Assert.Equal("00", newNode.values[0].value);
            
            Assert.Equal("bb", newNode.values[1].name);
            Assert.Equal("01", newNode.values[1].value);
            
            Assert.Same(node.nodes[0], newNode.nodes[0]);
            Assert.Equal("INNER_NODE_1", newNode.nodes[0].name);

            Assert.Equal(2, newNode.nodes[0].values.Count);
            Assert.Equal(1, newNode.nodes[0].nodes.Count);
            
            Assert.Equal("dd", newNode.nodes[0].values[0].name);
            Assert.Equal("03", newNode.nodes[0].values[0].value);
            
            Assert.Equal("ee", newNode.nodes[0].values[1].name);
            Assert.Equal("04", newNode.nodes[0].values[1].value);
            
            Assert.Equal("INNER_INNER_NODE_1", newNode.nodes[0].nodes[0].name);

            Assert.Equal(1, newNode.nodes[0].nodes[0].values.Count);
            Assert.Equal(0, newNode.nodes[0].nodes[0].nodes.Count);
            
            Assert.Equal("ff", newNode.nodes[0].nodes[0].values[0].name);
            Assert.Equal("05", newNode.nodes[0].nodes[0].values[0].value);

            // Assert.NotSame(node.nodes[1], newNode.nodes[1]);
            Assert.Equal("INNER_NODE_2", newNode.nodes[1].name);

            Assert.Equal(2, newNode.nodes[1].values.Count);
            Assert.Equal(2, newNode.nodes[1].nodes.Count);

            Assert.Equal("gg", newNode.nodes[1].values[0].name);
            Assert.Equal("06", newNode.nodes[1].values[0].value);

            Assert.Equal("ii", newNode.nodes[1].values[1].name);
            Assert.Equal("08", newNode.nodes[1].values[1].value);

            Assert.Equal("INNER_INNER_NODE_11", newNode.nodes[1].nodes[0].name);

            Assert.Equal("jj", newNode.nodes[1].nodes[0].values[0].name);
            Assert.Equal("09", newNode.nodes[1].nodes[0].values[0].value);

            Assert.Equal("INNER_INNER_NODE_12", newNode.nodes[1].nodes[1].name);

            Assert.Equal("kk", newNode.nodes[1].nodes[1].values[0].name);
            Assert.Equal("10", newNode.nodes[1].nodes[1].values[0].value);

            // Assert.NotSame(node.nodes[1], newNode.nodes[1]);
            Assert.Equal("INNER_NODE_3", newNode.nodes[2].name);

            Assert.Equal(2, newNode.nodes[2].values.Count);
            Assert.Equal(2, newNode.nodes[2].nodes.Count);

            Assert.Equal("mm", newNode.nodes[2].values[0].name);
            Assert.Equal("12", newNode.nodes[2].values[0].value);

            Assert.Equal("oo", newNode.nodes[2].values[1].name);
            Assert.Equal("14", newNode.nodes[2].values[1].value);

            Assert.Equal("INNER_INNER_NODE_21", newNode.nodes[2].nodes[0].name);

            Assert.Equal("pp", newNode.nodes[2].nodes[0].values[0].name);
            Assert.Equal("15", newNode.nodes[2].nodes[0].values[0].value);

            Assert.Equal("INNER_INNER_NODE_22", newNode.nodes[2].nodes[1].name);

            Assert.Equal("qq", newNode.nodes[2].nodes[1].values[0].name);
            Assert.Equal("16", newNode.nodes[2].nodes[1].values[0].value);

        }

        [Fact]
        public void TestCheckNeeds__Exception()
        {
            string[] modList = { "mod1", "mod2" };

            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE"), file);
            UrlDir.UrlConfig config2 = UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE:NEEDS[mod3]"), file);
            UrlDir.UrlConfig config3 = UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE"), file);

            Exception e = new Exception();
            progress.When(p => p.NeedsUnsatisfiedRoot(config2)).Throw(e);

            NeedsChecker.CheckNeeds(root, modList, progress, logger);

            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Error(null, null);

            string expected = @"
Exception while checking needs on root node :
abc/def/SOME_NODE:NEEDS[mod3]
  SOME_NODE:NEEDS[mod3]
  {
  }
".Replace("\r", null).TrimStart();

            progress.Received().Exception(config2, expected, e);

            Assert.Equal(new[] { config1, config3 }, root.AllConfigs);
        }

        [Fact]
        public void TestCheckNeeds__ExceptionWhileLoggingException()
        {
            string[] modList = { "mod1", "mod2" };

            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE"), file);
            UrlDir.UrlConfig config2 = UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE:NEEDS[mod3]"), file);
            UrlDir.UrlConfig config3 = UrlBuilder.CreateConfig(new ConfigNode("SOME_NODE"), file);

            Exception e1 = new Exception();
            Exception e2 = new Exception();
            progress.When(p => p.NeedsUnsatisfiedRoot(config2)).Throw(e1);
            progress.WhenForAnyArgs(p => p.Exception(null, null, null)).Throw(e2);

            NeedsChecker.CheckNeeds(root, modList, progress, logger);

            progress.ReceivedWithAnyArgs().Exception(null, null, null);
            progress.DidNotReceiveWithAnyArgs().Error(null, null);

            progress.Received().Exception("Exception while attempting to log an exception", e2);

            Assert.Equal(new[] { config1, config3 }, root.AllConfigs);
        }

        private UrlDir.UrlConfig CreateConfig(string name)
        {
            ConfigNode node = new TestConfigNode(name)
            {
                { "name", "test" },
                { "foo", "bar" },
                new ConfigNode("INNER_NODE"),
            };

            node.id = "who_uses_this";

            return UrlBuilder.CreateConfig(node, file);
        }

        private void AssertUrlCorrect(string expectedNodeName, UrlDir.UrlConfig originalUrl, UrlDir.UrlConfig observedUrl)
        {
            // Assert.NotSame(originalUrl, observedUrl);
            Assert.Equal(expectedNodeName, observedUrl.type);

            ConfigNode originalNode = originalUrl.config;
            ConfigNode observedNode = observedUrl.config;

            Assert.Equal(expectedNodeName, observedNode.name);

            if (originalNode.HasValue("name")) Assert.Equal(originalNode.GetValue("name"), observedUrl.name);

            Assert.Same(originalUrl.parent, observedUrl.parent);

            Assert.Equal(originalNode.id, observedNode.id);
            Assert.Equal(originalNode.values.Count, observedNode.values.Count);
            Assert.Equal(originalNode.nodes.Count, observedNode.nodes.Count);

            for (int i = 0; i < originalNode.values.Count; i++)
            {
                Assert.Same(originalNode.values[i], observedNode.values[i]);
            }

            for (int i = 0; i < originalNode.nodes.Count; i++)
            {
                Assert.Same(originalNode.nodes[i], observedNode.nodes[i]);
            }
        }
    }
}
