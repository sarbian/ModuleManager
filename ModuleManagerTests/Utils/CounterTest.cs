using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using ModuleManager.Utils;

namespace ModuleManagerTests.Utils
{
    public class CounterTest
    {
        [Fact]
        public void Test__Constructor()
        {
            Counter counter = new Counter();

            Assert.Equal(0, counter.Value);
        }

        [Fact]
        public void TestIncrement()
        {
            Counter counter = new Counter();

            Assert.Equal(0, counter.Value);

            counter.Increment();

            Assert.Equal(1, counter.Value);

            counter.Increment();

            Assert.Equal(2, counter.Value);
        }

        [Fact]
        public void Test__CastAsInt()
        {
            Counter counter = new Counter();
            int i;

            i = counter;
            Assert.Equal(0, i);

            counter.Increment();

            i = counter;
            Assert.Equal(1, i);

            counter.Increment();

            i = counter;
            Assert.Equal(2, i);
        }
    }
}
