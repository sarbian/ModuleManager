using System;
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
        private readonly UrlDir gameData;

        private readonly IPatchProgress progress = Substitute.For<IPatchProgress>();
        private readonly IBasicLogger logger = Substitute.For<IBasicLogger>();
        private readonly NeedsChecker needsChecker;

        public NeedsCheckerTest()
        {
            gameData = UrlBuilder.CreateGameData();
            needsChecker = new NeedsChecker(new[] { "mod1", "mod2", "mod/2" }, gameData, progress, logger);
        }

        [Fact]
        public void TestConstructor__ModsNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new NeedsChecker(null, gameData, progress, logger);
            });

            Assert.Equal("mods", ex.ParamName);
        }

        [Fact]
        public void TestConstructor__GameDataNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new NeedsChecker(new string[0], null, progress, logger);
            });

            Assert.Equal("gameData", ex.ParamName);
        }

        [Fact]
        public void TestConstructor__ProgressNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new NeedsChecker(new string[0], gameData, null, logger);
            });

            Assert.Equal("progress", ex.ParamName);
        }

        [Fact]
        public void TestConstructor__LoggerNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new NeedsChecker(new string[0], gameData, progress, null);
            });

            Assert.Equal("logger", ex.ParamName);
        }

        [Fact]
        public void TestCheckNeedsExpression()
        {
            Assert.True(needsChecker.CheckNeedsExpression("mod1"));
            Assert.True(needsChecker.CheckNeedsExpression("mod2"));
            Assert.False(needsChecker.CheckNeedsExpression("mod3"));
        }

        [Fact]
        public void TestCheckNeedsExpression__AndOr()
        {
            Assert.True(needsChecker.CheckNeedsExpression("mod1&mod2"));
            Assert.True(needsChecker.CheckNeedsExpression("mod1,mod2"));
            Assert.True(needsChecker.CheckNeedsExpression("mod1|mod2"));
            Assert.True(needsChecker.CheckNeedsExpression("mod1|mod3"));
            Assert.True(needsChecker.CheckNeedsExpression("mod1&mod2|mod3"));
            Assert.True(needsChecker.CheckNeedsExpression("mod1,mod2|mod3"));
            Assert.True(needsChecker.CheckNeedsExpression("mod1|mod3&mod2"));
            Assert.True(needsChecker.CheckNeedsExpression("mod1|mod3,mod2"));

            Assert.False(needsChecker.CheckNeedsExpression("mod1&mod3"));
            Assert.False(needsChecker.CheckNeedsExpression("mod1,mod3"));
            Assert.False(needsChecker.CheckNeedsExpression("mod1&mod2&mod3"));
            Assert.False(needsChecker.CheckNeedsExpression("mod1,mod2,mod3"));
            Assert.False(needsChecker.CheckNeedsExpression("mod3|mod4"));
            Assert.False(needsChecker.CheckNeedsExpression("mod1|mod2&mod3"));
            Assert.False(needsChecker.CheckNeedsExpression("mod1|mod2,mod3"));
            Assert.False(needsChecker.CheckNeedsExpression("mod3&mod1|mod2"));
            Assert.False(needsChecker.CheckNeedsExpression("mod3,mod1|mod2"));
        }

        [Fact]
        public void TestCheckNeedsExpression__Not()
        {
            Assert.True(needsChecker.CheckNeedsExpression("!mod3"));
            Assert.True(needsChecker.CheckNeedsExpression("mod1,!mod3"));
            Assert.True(needsChecker.CheckNeedsExpression("!mod1|!mod3"));
            Assert.True(needsChecker.CheckNeedsExpression("mod1|!mod2"));
            
            Assert.False(needsChecker.CheckNeedsExpression("!mod1"));
            Assert.False(needsChecker.CheckNeedsExpression("!mod1,mod2"));
            Assert.False(needsChecker.CheckNeedsExpression("!mod1&!mod3"));
        }

        [Fact]
        public void TestCheckNeedsExpression__Capitalization()
        {
            Assert.True(needsChecker.CheckNeedsExpression("mod1"));
            Assert.True(needsChecker.CheckNeedsExpression("Mod1"));
            Assert.True(needsChecker.CheckNeedsExpression("MOD1"));

            Assert.False(needsChecker.CheckNeedsExpression("mod3"));
            Assert.False(needsChecker.CheckNeedsExpression("Mod3"));
            Assert.False(needsChecker.CheckNeedsExpression("MOD3"));
        }

        [Fact]
        public void TestCheckNeedsExpression__Directory()
        {
            UrlBuilder.CreateDir("abc", gameData);
            UrlBuilder.CreateDir("ghi/jkl", gameData);

            Assert.True(needsChecker.CheckNeedsExpression("/abc"));
            Assert.True(needsChecker.CheckNeedsExpression("abc/"));
            Assert.True(needsChecker.CheckNeedsExpression("/abc/"));
            Assert.True(needsChecker.CheckNeedsExpression("ghi/jkl"));
            Assert.True(needsChecker.CheckNeedsExpression("/ghi/jkl"));
            Assert.True(needsChecker.CheckNeedsExpression("ghi/jkl/"));
            Assert.True(needsChecker.CheckNeedsExpression("mod1&ghi/jkl"));
            Assert.True(needsChecker.CheckNeedsExpression("mod3|ghi/jkl"));
            Assert.True(needsChecker.CheckNeedsExpression("abc/&ghi/jkl"));
            Assert.True(needsChecker.CheckNeedsExpression("mod/2"));

            Assert.False(needsChecker.CheckNeedsExpression("abc"));
            Assert.False(needsChecker.CheckNeedsExpression("mod3&ghi/jkl"));
            Assert.False(needsChecker.CheckNeedsExpression("Ghi/jkl"));
            Assert.False(needsChecker.CheckNeedsExpression("mno/pqr"));
        }

        [Fact]
        public void TestCheckNeedsExpression__Null()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                needsChecker.CheckNeedsExpression(null);
            });

            Assert.Equal("needsExpression", ex.ParamName);
        }

        [Fact]
        public void TestCheckNeedsExpression__Empty()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(delegate
            {
                needsChecker.CheckNeedsExpression("");
            });

            Assert.Equal("needsExpression", ex.ParamName);
            Assert.Contains("can't be empty", ex.Message);
        }

        [Fact]
        public void TestCheckNeeds()
        {
            UrlBuilder.CreateDir("ghi/jkl", gameData);

            Assert.True(needsChecker.CheckNeeds("mod1"));
            Assert.True(needsChecker.CheckNeeds("MOD1"));
            Assert.True(needsChecker.CheckNeeds("mod2"));

            Assert.False(needsChecker.CheckNeeds("mod1&mod2"));
            Assert.False(needsChecker.CheckNeeds("ghi/jkl"));
        }

        [Fact]
        public void TestCheckNeeds__Null()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                needsChecker.CheckNeeds(null);
            });

            Assert.Equal("mod", ex.ParamName);
        }

        [Fact]
        public void TestCheckNeeds__Empty()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(delegate
            {
                needsChecker.CheckNeeds("");
            });

            Assert.Equal("mod", ex.ParamName);
            Assert.Contains("can't be empty", ex.Message);
        }

        [Fact]
        public void TestCheckNeedsRecursive()
        {
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

            UrlDir.UrlConfig urlConfig = UrlBuilder.CreateConfig("abc/def", node);

            needsChecker.CheckNeedsRecursive(node, urlConfig);

            progress.DidNotReceiveWithAnyArgs().Warning(null, null);
            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);

            Received.InOrder(delegate
            {
                progress.NeedsUnsatisfiedValue(urlConfig, "SOME_NODE/cc:NEEDS[mod3]");
                progress.NeedsUnsatisfiedValue(urlConfig, "SOME_NODE/INNER_NODE_2/hh:NEEDS[mod3]");
                progress.NeedsUnsatisfiedNode(urlConfig, "SOME_NODE/INNER_NODE_2/INNER_INNER_NODE_12:NEEDS[mod3]");
                progress.NeedsUnsatisfiedValue(urlConfig, "SOME_NODE/INNER_NODE_3/nn:NEEDS[mod3]");
                progress.NeedsUnsatisfiedNode(urlConfig, "SOME_NODE/INNER_NODE_3/INNER_INNER_NODE_22:NEEDS[mod3]");
                progress.NeedsUnsatisfiedNode(urlConfig, "SOME_NODE/INNER_NODE_4:NEEDS[mod3]");
            });

            Assert.Equal(2, node.values.Count);
            Assert.Equal(3, node.nodes.Count);
            
            Assert.Equal("aa", node.values[0].name);
            Assert.Equal("00", node.values[0].value);
            
            Assert.Equal("bb", node.values[1].name);
            Assert.Equal("01", node.values[1].value);
            
            Assert.Same(node.nodes[0], node.nodes[0]);
            Assert.Equal("INNER_NODE_1", node.nodes[0].name);

            Assert.Equal(2, node.nodes[0].values.Count);
            Assert.Equal(1, node.nodes[0].nodes.Count);
            
            Assert.Equal("dd", node.nodes[0].values[0].name);
            Assert.Equal("03", node.nodes[0].values[0].value);
            
            Assert.Equal("ee", node.nodes[0].values[1].name);
            Assert.Equal("04", node.nodes[0].values[1].value);
            
            Assert.Equal("INNER_INNER_NODE_1", node.nodes[0].nodes[0].name);

            Assert.Equal(1, node.nodes[0].nodes[0].values.Count);
            Assert.Equal(0, node.nodes[0].nodes[0].nodes.Count);
            
            Assert.Equal("ff", node.nodes[0].nodes[0].values[0].name);
            Assert.Equal("05", node.nodes[0].nodes[0].values[0].value);

            // Assert.NotSame(node.nodes[1], newNode.nodes[1]);
            Assert.Equal("INNER_NODE_2", node.nodes[1].name);

            Assert.Equal(2, node.nodes[1].values.Count);
            Assert.Equal(2, node.nodes[1].nodes.Count);

            Assert.Equal("gg", node.nodes[1].values[0].name);
            Assert.Equal("06", node.nodes[1].values[0].value);

            Assert.Equal("ii", node.nodes[1].values[1].name);
            Assert.Equal("08", node.nodes[1].values[1].value);

            Assert.Equal("INNER_INNER_NODE_11", node.nodes[1].nodes[0].name);

            Assert.Equal("jj", node.nodes[1].nodes[0].values[0].name);
            Assert.Equal("09", node.nodes[1].nodes[0].values[0].value);

            Assert.Equal("INNER_INNER_NODE_12", node.nodes[1].nodes[1].name);

            Assert.Equal("kk", node.nodes[1].nodes[1].values[0].name);
            Assert.Equal("10", node.nodes[1].nodes[1].values[0].value);

            // Assert.NotSame(node.nodes[1], newNode.nodes[1]);
            Assert.Equal("INNER_NODE_3", node.nodes[2].name);

            Assert.Equal(2, node.nodes[2].values.Count);
            Assert.Equal(2, node.nodes[2].nodes.Count);

            Assert.Equal("mm", node.nodes[2].values[0].name);
            Assert.Equal("12", node.nodes[2].values[0].value);

            Assert.Equal("oo", node.nodes[2].values[1].name);
            Assert.Equal("14", node.nodes[2].values[1].value);

            Assert.Equal("INNER_INNER_NODE_21", node.nodes[2].nodes[0].name);

            Assert.Equal("pp", node.nodes[2].nodes[0].values[0].name);
            Assert.Equal("15", node.nodes[2].nodes[0].values[0].value);

            Assert.Equal("INNER_INNER_NODE_22", node.nodes[2].nodes[1].name);

            Assert.Equal("qq", node.nodes[2].nodes[1].values[0].name);
            Assert.Equal("16", node.nodes[2].nodes[1].values[0].value);

        }
        
        [Fact]
        public void TestCheckNeedsRecursive__NodeNull()
        {
            UrlDir.UrlConfig urlConfig = UrlBuilder.CreateConfig("abc/def", new ConfigNode("NODE"));
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                needsChecker.CheckNeedsRecursive(null, urlConfig);
            });

            Assert.Equal("node", ex.ParamName);
        }

        [Fact]
        public void TestCheckNeedsRecursive__UrlConfigNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                needsChecker.CheckNeedsRecursive(new ConfigNode(), null);
            });

            Assert.Equal("urlConfig", ex.ParamName);
        }
    }
}
