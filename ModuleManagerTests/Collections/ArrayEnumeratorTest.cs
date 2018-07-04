using System;
using System.Collections.Generic;
using Xunit;
using ModuleManager.Collections;

namespace ModuleManagerTests.Collections
{
    public class ArrayEnumeratorTest
    {
        [Fact]
        public void Test__Constructor__ArrayNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new ArrayEnumerator<string>(null);
            });

            Assert.Equal("array", ex.ParamName);
        }

        [Fact]
        public void Test__Constructor__StartIndex__Negative()
        {
            string[] arr = { "abc", "def", "ghi" };
            ArgumentException ex = Assert.Throws<ArgumentException>(delegate
            {
                new ArrayEnumerator<string>(arr, -1);
            });

            Assert.Equal("startIndex", ex.ParamName);
            Assert.Contains("must be non-negative (got -1)", ex.Message);
        }

        [Fact]
        public void Test__Constructor__StartIndex__TooLarge()
        {
            string[] arr = { "abc", "def", "ghi" };
            ArgumentException ex = Assert.Throws<ArgumentException>(delegate
            {
                new ArrayEnumerator<string>(arr, 4);
            });

            Assert.Equal("startIndex", ex.ParamName);
            Assert.Contains("must be less than or equal to array length (array length 3, startIndex 4)", ex.Message);
        }

        [Fact]
        public void Test__Constructor__Length__Negative()
        {
            string[] arr = { "abc", "def", "ghi" };
            ArgumentException ex = Assert.Throws<ArgumentException>(delegate
            {
                new ArrayEnumerator<string>(arr, 1, -1);
            });

            Assert.Equal("length", ex.ParamName);
            Assert.Contains("must be non-negative (got -1)", ex.Message);
        }

        [Fact]
        public void Test__Constructor__Length__TooLong()
        {
            string[] arr = { "abc", "def", "ghi" };
            ArgumentException ex = Assert.Throws<ArgumentException>(delegate
            {
                new ArrayEnumerator<string>(arr, 1, 3);
            });

            Assert.Equal("length", ex.ParamName);
            Assert.Contains("must fit within the string (array length 3, startIndex 1, length 3)", ex.Message);
        }

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

        [Fact]
        public void TestArrayEnumerator__StartIndex()
        {
            string[] arr = { "abc", "def", "ghi" };

            IEnumerator<string> enumerator = new ArrayEnumerator<string>(arr, 1);

            Assert.True(enumerator.MoveNext());
            Assert.Equal("def", enumerator.Current);
            Assert.True(enumerator.MoveNext());
            Assert.Equal("ghi", enumerator.Current);
            Assert.False(enumerator.MoveNext());
        }

        [Fact]
        public void TestArrayEnumerator__StartIndex__Length()
        {
            string[] arr = { "abc", "def", "ghi", "jkl" };

            IEnumerator<string> enumerator = new ArrayEnumerator<string>(arr, 1, 2);

            Assert.True(enumerator.MoveNext());
            Assert.Equal("def", enumerator.Current);
            Assert.True(enumerator.MoveNext());
            Assert.Equal("ghi", enumerator.Current);
            Assert.False(enumerator.MoveNext());
        }
    }
}
