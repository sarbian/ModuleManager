using System;
using Xunit;
using TestUtils;
using ModuleManager;

namespace ModuleManagerTests
{
    public class NodeMatcherTest
    {
        #region Constructor

        [Fact]
        public void TestConstructor__Null()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new NodeMatcher(null);
            });

            Assert.Equal("nodeName", ex.ParamName);
        }

        [Fact]
        public void TestConstructor__Blank()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(delegate
            {
                new NodeMatcher("");
            });

            Assert.Equal("nodeName", ex.ParamName);
            Assert.Contains("can't be empty", ex.Message);
        }

        [Fact]
        public void TestConstructor__NotBracketBalanced()
        {
            FormatException ex = Assert.Throws<FormatException>(delegate
            {
                new NodeMatcher("NODE[stuff");
            });

            Assert.Equal("node name is not bracket balanced: NODE[stuff", ex.Message);
        }

        [Fact]
        public void TestConstructor__StartsWithHas()
        {
            FormatException ex = Assert.Throws<FormatException>(delegate
            {
                new NodeMatcher(":HAS[#blah]");
            });

            Assert.Equal("node name cannot begin with :HAS : :HAS[#blah]", ex.Message);
        }

        [Fact]
        public void TestConstructor__StartsWithBracket()
        {
            FormatException ex = Assert.Throws<FormatException>(delegate
            {
                new NodeMatcher("[#blah]");
            });

            Assert.Equal("node name cannot begin with a bracket: [#blah]", ex.Message);
        }

        #endregion

        #region IsMatch

        [Fact]
        public void TestIsMatch()
        {
            NodeMatcher matcher = new NodeMatcher("NODE");

            Assert.True(matcher.IsMatch(new ConfigNode("NODE")));
            Assert.False(matcher.IsMatch(new ConfigNode("PART")));
        }

        [Fact]
        public void TestIsMatch__Name()
        {
            NodeMatcher matcher = new NodeMatcher("NODE[blah]");

            Assert.True(matcher.IsMatch(new TestConfigNode("NODE")
            {
                { "name", "blah" },
            }));

            Assert.False(matcher.IsMatch(new ConfigNode("NODE")));

            Assert.False(matcher.IsMatch(new TestConfigNode("NODE")
            {
                { "name", "bleh" },
            }));
            
            Assert.False(matcher.IsMatch(new ConfigNode("PART")));

            Assert.False(matcher.IsMatch(new TestConfigNode("PART")
            {
                { "name", "blah" },
            }));

            Assert.False(matcher.IsMatch(new TestConfigNode("PART")
            {
                { "name", "bleh" },
            }));
        }

        [Fact]
        public void TestIsMatch__Name__Wildcard()
        {
            NodeMatcher matcher = new NodeMatcher("NODE[bl*h]");

            Assert.True(matcher.IsMatch(new TestConfigNode("NODE")
            {
                { "name", "blah" },
            }));

            Assert.True(matcher.IsMatch(new TestConfigNode("NODE")
            {
                { "name", "blablah" },
            }));

            Assert.True(matcher.IsMatch(new TestConfigNode("NODE")
            {
                { "name", "bleh" },
            }));

            Assert.False(matcher.IsMatch(new ConfigNode("NODE")));

            Assert.False(matcher.IsMatch(new TestConfigNode("NODE")
            {
                { "name", "blue" },
            }));

            Assert.False(matcher.IsMatch(new ConfigNode("PART")));

            Assert.False(matcher.IsMatch(new TestConfigNode("PART")
            {
                { "name", "blah" },
            }));

            Assert.False(matcher.IsMatch(new TestConfigNode("PART")
            {
                { "name", "blue" },
            }));
        }

        [Fact]
        public void TestIsMatch__Name__Multiple()
        {
            NodeMatcher matcher = new NodeMatcher("NODE[blah|bleh|blih*]");

            Assert.True(matcher.IsMatch(new TestConfigNode("NODE")
            {
                { "name", "blah" },
            }));

            Assert.True(matcher.IsMatch(new TestConfigNode("NODE")
            {
                { "name", "bleh" },
            }));

            Assert.True(matcher.IsMatch(new TestConfigNode("NODE")
            {
                { "name", "blih" },
            }));

            Assert.True(matcher.IsMatch(new TestConfigNode("NODE")
            {
                { "name", "blihblih" },
            }));

            Assert.False(matcher.IsMatch(new ConfigNode("NODE")));

            Assert.False(matcher.IsMatch(new TestConfigNode("NODE")
            {
                { "name", "bloh" },
            }));

            Assert.False(matcher.IsMatch(new ConfigNode("PART")));

            Assert.False(matcher.IsMatch(new TestConfigNode("PART")
            {
                { "name", "blah" },
            }));

            Assert.False(matcher.IsMatch(new TestConfigNode("PART")
            {
                { "name", "bleh" },
            }));

            Assert.False(matcher.IsMatch(new TestConfigNode("PART")
            {
                { "name", "blih" },
            }));

            Assert.False(matcher.IsMatch(new TestConfigNode("PART")
            {
                { "name", "bloh" },
            }));
        }

        [Fact]
        public void TestIsMatch__Constraints()
        {
            NodeMatcher matcher = new NodeMatcher("NODE[blah]:HAS[@FOO[bar*],#something[else]]");

            Assert.True(matcher.IsMatch(new TestConfigNode("NODE")
            {
                { "name", "blah" },
                { "something", "else" },
                new TestConfigNode("FOO")
                {
                    { "name", "barbar" },
                },
            }));

            Assert.False(matcher.IsMatch(new TestConfigNode("NODE")
            {
                { "name", "blah" },
            }));

            Assert.False(matcher.IsMatch(new TestConfigNode("NODE")
            {
                { "name", "blah" },
                { "something", "else" },
            }));

            Assert.False(matcher.IsMatch(new TestConfigNode("NODE")
            {
                { "name", "blah" },
                new TestConfigNode("FOO")
                {
                    { "name", "barbar" },
                },
            }));

            Assert.False(matcher.IsMatch(new TestConfigNode("NODE")
            {
                { "name", "bleh" },
                { "something", "else" },
                new TestConfigNode("FOO")
                {
                    { "name", "barbar" },
                },
            }));

            Assert.False(matcher.IsMatch(new TestConfigNode("NADE")
            {
                { "name", "blah" },
                { "something", "else" },
                new TestConfigNode("FOO")
                {
                    { "name", "barbar" },
                },
            }));
        }

        [Fact]
        public void TestIsMatch__Constraints_Open()
        {
            NodeMatcher matcher = new NodeMatcher("NODE[blah]:HAS[@FOO,#something]");

            Assert.True(matcher.IsMatch(new TestConfigNode("NODE")
            {
                { "name", "blah" },
                { "something", "else" },
                new TestConfigNode("FOO")
                {
                    { "name", "barbar" },
                },
            }));

            Assert.True(matcher.IsMatch(new TestConfigNode("NODE")
            {
                { "name", "blah" },
                { "something", "else" },
                new TestConfigNode("FOO")
                {
                    { "name", "barbar" },
                },
            }));

            Assert.False(matcher.IsMatch(new TestConfigNode("NODE")
            {
                { "name", "blah" },
            }));

            Assert.False(matcher.IsMatch(new TestConfigNode("NODE")
            {
                { "name", "blah" },
                { "something", "else" },
            }));

            Assert.False(matcher.IsMatch(new TestConfigNode("NODE")
            {
                { "name", "blah" },
                new TestConfigNode("FOO")
                {
                    { "name", "barbar" },
                },
            }));

            Assert.False(matcher.IsMatch(new TestConfigNode("NODE")
            {
                { "name", "bleh" },
                { "something", "else" },
                new TestConfigNode("FOO")
                {
                    { "name", "barbar" },
                },
            }));

            Assert.False(matcher.IsMatch(new TestConfigNode("NADE")
            {
                { "name", "blah" },
                { "something", "else" },
                new TestConfigNode("FOO")
                {
                    { "name", "barbar" },
                },
            }));
        }

        #endregion
    }
}
