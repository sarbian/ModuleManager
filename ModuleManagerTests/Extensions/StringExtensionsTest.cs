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
    }
}
