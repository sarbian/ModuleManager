using System;
using System.Collections.Generic;
using Xunit;
using ModuleManager.Collections;

namespace ModuleManagerTests.Collections
{
    public class ArrayEnumeratorTest
    {
        [Fact]
        public void TestArrayEnumerator()
        {
            string[] arr = { "abc", "def", "ghi" };

            IEnumerator<string> enumerator = new ArrayEnumerator<string>(arr);

            Assert.True(enumerator.MoveNext());
            Assert.Equal("abc", enumerator.Current);
            Assert.True(enumerator.MoveNext());
            Assert.Equal("def", enumerator.Current);
            Assert.True(enumerator.MoveNext());
            Assert.Equal("ghi", enumerator.Current);
            Assert.False(enumerator.MoveNext());
        }

        [Fact]
        public void TestArrayEnumerator__Reset()
        {
            string[] arr = { "abc", "def" };

            IEnumerator<string> enumerator = new ArrayEnumerator<string>(arr);

            Assert.True(enumerator.MoveNext());
            Assert.Equal("abc", enumerator.Current);
            Assert.True(enumerator.MoveNext());
            Assert.Equal("def", enumerator.Current);
            Assert.False(enumerator.MoveNext());

            enumerator.Reset();

            Assert.True(enumerator.MoveNext());
            Assert.Equal("abc", enumerator.Current);
            Assert.True(enumerator.MoveNext());
            Assert.Equal("def", enumerator.Current);
            Assert.False(enumerator.MoveNext());
        }

        [Fact]
        public void TestArrayEnumerator__Empty()
        {
            string[] arr = new string[0];
            IEnumerator<string> enumerator = new ArrayEnumerator<string>(arr);
            Assert.False(enumerator.MoveNext());
        }
    }
}
