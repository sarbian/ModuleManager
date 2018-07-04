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
        public void TestConstructor__TypeNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new NodeMatcher(null, null, null);
            });

            Assert.Equal("type", ex.ParamName);
        }

        [Fact]
        public void TestConstructor__TypeBlank()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(delegate
            {
                new NodeMatcher("", null, null);
            });

            Assert.Equal("type", ex.ParamName);
            Assert.Contains("can't be empty", ex.Message);
        }

        [Fact]
        public void TestConstructor__NameBlank()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(delegate
            {
                new NodeMatcher("NODE", "", null);
            });

            Assert.Equal("name", ex.ParamName);
            Assert.Contains("can't be empty (null allowed)", ex.Message);
        }

        [Fact]
        public void TestConstructor__ConstraintsBlank()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(delegate
            {
                new NodeMatcher("NODE", null, "");
            });

            Assert.Equal("constraints", ex.ParamName);
            Assert.Contains("can't be empty (null allowed)", ex.Message);
        }

        [Fact]
        public void TestConstructor__ConstraintsNotBracketBalanced()
        {
            ArgumentException ex = Assert.Throws<ArgumentException>(delegate
            {
                new NodeMatcher("NODE", null, "stuff[blah");
            });

            Assert.Equal("constraints", ex.ParamName);
            Assert.Contains("is not bracket balanced: stuff[blah", ex.Message);
        }

        #endregion

        #region IsMatch

        [Fact]
        public void TestIsMatch()
        {
            NodeMatcher matcher = new NodeMatcher("NODE", null, null);

            Assert.True(matcher.IsMatch(new ConfigNode("NODE")));
            Assert.False(matcher.IsMatch(new ConfigNode("PART")));
        }

        [Fact]
        public void TestIsMatch__Name()
        {
            NodeMatcher matcher = new NodeMatcher("NODE", "blah", null);

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
            NodeMatcher matcher = new NodeMatcher("NODE", "bl*h", null);

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
            NodeMatcher matcher = new NodeMatcher("NODE", "blah|bleh|blih*", null);

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
            NodeMatcher matcher = new NodeMatcher("NODE", "blah", "@FOO[bar*],#something[else]");

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
            NodeMatcher matcher = new NodeMatcher("NODE", "blah", "@FOO,#something");

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
