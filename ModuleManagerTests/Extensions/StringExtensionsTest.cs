using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using ModuleManager.Extensions;

namespace ModuleManagerTests.Extensions
{
    public class StringExtensionsTest
    {
        [Fact]
        public void TestIsBracketBalanced()
        {
            Assert.True("abc[def[ghi[jkl]mno[pqr]]stu]vwx".IsBracketBalanced());
        }

        [Fact]
        public void TestIsBracketBalanced__NoBrackets()
        {
            Assert.True("she sells seashells by the seashore".IsBracketBalanced());
        }

        [Fact]
        public void TestIsBracketBalanced__Unbalanced()
        {
            Assert.False("abc[def[ghi[jkl]mno[pqr]]stuvwx".IsBracketBalanced());
            Assert.False("abcdef[ghi[jkl]mno[pqr]]stu]vwx".IsBracketBalanced());
        }

        [Fact]
        public void TestIsBracketBalanced__BalancedButNegative()
        {
            Assert.False("abc]def[ghi".IsBracketBalanced());
        }

        [Fact]
        public void TestRemoveWS()
        {
            Assert.Equal("abcdef", " abc \tdef\r\n\t ".RemoveWS());
        }


        [InlineData("abc", "b", true, 1)]
        [InlineData("abc", "x", false, -1)]
        [Theory]
        public void TestContains(string str, string test, bool expectedResult, int expectedIndex)
        {
            bool result = str.Contains(test, out int index);
            Assert.Equal(expectedResult, result);
            Assert.Equal(expectedIndex, index);
        }

        [Fact]
        public void TestContains__NullStr()
        {
            string s = null;
            Assert.Throws<ArgumentNullException>(delegate
            {
                s.Contains("x", out int _x);
            });
        }

        [Fact]
        public void TestContains__NullValue()
        {
            Assert.Throws<ArgumentNullException>(delegate
            {
                "abc".Contains(null, out int _x);
            });
        }
    }
}
