using System;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;
using TestUtils;
using ModuleManager;

namespace ModuleManagerTests
{
    public class WildcardMatchTest
    {
        // What WildcardMatch did with a regex for every pattern
        private static bool RegexMatch(string s, string wildcard)
        {
            if (wildcard == null) return true;
            return new Regex("^" + Regex.Escape(wildcard).Replace(@"\*", ".*").Replace(@"\?", ".") + "$").IsMatch(s);
        }

        [Theory]
        [InlineData("abc", "abc")]
        [InlineData("abc", "a*")]
        [InlineData("abc", "*c")]
        [InlineData("abc", "*b*")]
        [InlineData("abc", "a?c")]
        [InlineData("abc", "a?")]
        [InlineData("abc", "*")]
        [InlineData("", "*")]
        [InlineData("", "?")]
        [InlineData("", "")]
        [InlineData("abc\n", "a*")]
        [InlineData("abc\n", "abc")]
        [InlineData("abc\n\n", "a*")]
        [InlineData("a\nc", "a?c")]
        [InlineData("a\nc", "a*c")]
        [InlineData("a\nc", "a*")]
        [InlineData("a\nc", "*c")]
        [InlineData("a.c", "a.c")]
        [InlineData("abc", "a.c")]
        [InlineData("a+b", "a+*")]
        [InlineData("ABC", "a*")]
        [InlineData("a\\b", "a\\*")]
        [InlineData("a\\*", "a\\*")]
        [InlineData("aaa", "a*a*a")]
        [InlineData("aa", "a*a*a")]
        [InlineData("mississippi", "m*iss*p?i")]
        [InlineData("a(b)", "a(*)")]
        [InlineData("a[b]", "a[?]")]
        [InlineData("a b", "a?b")]
        [InlineData("a\rb", "a?b")]
        public void TestWildcardMatch__SameAsRegex(string s, string wildcard)
        {
            Assert.Equal(RegexMatch(s, wildcard), MMPatchLoader.WildcardMatch(s, wildcard));
        }

        [Fact]
        public void TestWildcardMatch__Random()
        {
            Random random = new Random(1234);
            const string chars = "ab*?\n.\\";
            string Make(int maxLength)
            {
                StringBuilder sb = new StringBuilder();
                int length = random.Next(maxLength + 1);
                for (int i = 0; i < length; i++) sb.Append(chars[random.Next(chars.Length)]);
                return sb.ToString();
            }

            for (int i = 0; i < 20000; i++)
            {
                string wildcard = Make(6);
                string s = Make(8).Replace("*", "a").Replace("?", "b");
                Assert.True(RegexMatch(s, wildcard) == MMPatchLoader.WildcardMatch(s, wildcard), $"s = \"{s}\", wildcard = \"{wildcard}\"");
            }
        }

        [Fact]
        public void TestWildcardMatch__Null()
        {
            Assert.True(MMPatchLoader.WildcardMatch("abc", null));
            Assert.Throws<ArgumentNullException>(() => MMPatchLoader.WildcardMatch(null, "a*"));
            Assert.Throws<ArgumentNullException>(() => MMPatchLoader.WildcardMatch(null, "abc"));
        }

        [Theory]
        [InlineData("#foo", true)]
        [InlineData("#foo[1]", true)]
        [InlineData("#foo[2]", true)]
        [InlineData("#foo[3]", false)]
        [InlineData("#foo[>1.5]", true)]
        [InlineData("#foo[<1]", false)]
        [InlineData("#bar", false)]
        [InlineData("~bar[]", true)]
        [InlineData("~foo[]", false)]
        [InlineData("~foo[3]", true)]
        [InlineData("~foo[2]", false)]
        [InlineData("#foo[1],#foo[2]", true)]
        [InlineData("#foo[1],#bar", false)]
        [InlineData("#foo[1]&@INNER[x]", true)]
        [InlineData("@INNER[x]:HAS[#v[1]]", true)]
        [InlineData("@INNER[x]:HAS[#v[2]]", false)]
        [InlineData("@INNER:HAS[#v[2]]", true)]
        [InlineData("!INNER:HAS[#v[3]]", true)]
        [InlineData("@INNER[*]:HAS[@DEEP:HAS[#w[a*]]]", true)]
        [InlineData("@INNER[*]:HAS[@DEEP:HAS[#w[b*]]]", false)]
        [InlineData("@INNER[y]", true)]
        [InlineData("@INNER[Y]", false)]
        [InlineData("@INNE", false)]
        [InlineData("@INNER[x],@INNER[y]:HAS[#v[2]]", true)]
        // a type or name followed by a newline matches the literal, as ^literal$ does
        [InlineData("@LINE", true)]
        [InlineData("@LINE[z]", true)]
        [InlineData("@LINE[z]:HAS[#v[9]]", true)]
        [InlineData("@LINE[zz]", false)]
        [InlineData("!LINE[z]", false)]
        [InlineData("@LIN?[z*]", true)]
        public void TestCheckConstraints(string constraints, bool expected)
        {
            ConfigNode node = new TestConfigNode("PART")
            {
                { "foo", "1" },
                { "foo", "2" },
                new TestConfigNode("INNER") { { "name", "x" }, { "v", "1" } },
                new TestConfigNode("INNER") { { "name", "y" }, { "v", "2" }, new TestConfigNode("DEEP") { { "w", "abc" } } },
            };
            ConfigNode line = new ConfigNode();
            line.name = "LINE\n";
            line.AddValue("name", "z");
            line.values[0].value = "z\n";
            line.AddValue("v", "9");
            node.AddNode(line);
            Assert.Equal("LINE\n", node.nodes[2].name);

            // twice: the second time takes the parsed constraints from the cache
            Assert.Equal(expected, MMPatchLoader.CheckConstraints(node, constraints));
            Assert.Equal(expected, MMPatchLoader.CheckConstraints(node, constraints));
        }
    }
}
