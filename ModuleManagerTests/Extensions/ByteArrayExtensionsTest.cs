using System;
using Xunit;
using ModuleManager.Extensions;

namespace ModuleManagerTests.Extensions
{
    public class ByteArrayExtensionsTest
    {
        [Fact]
        public void TestToHex()
        {
            byte[] data = { 0x00, 0xff, 0x01, 0xfe, 0x02, 0xfd, 0x9a };

            Assert.Equal("00ff01fe02fd9a", data.ToHex());
        }

        [Fact]
        public void TestToHex__NullData()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                ByteArrayExtensions.ToHex(null);
            });

            Assert.Equal("data", ex.ParamName);
        }
    }
}
