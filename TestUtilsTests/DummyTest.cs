using System;
using Xunit;

namespace TestUtilsTests
{
    public class DummyTest
    {
        [Fact]
        public void PassingTest()
        {
            Assert.Equal(true, true);
        }
    }
}
