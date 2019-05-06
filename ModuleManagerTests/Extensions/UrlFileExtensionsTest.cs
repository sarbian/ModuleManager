using System;
using Xunit;
using TestUtils;
using ModuleManager.Extensions;

namespace ModuleManagerTests.Extensions
{
    public static class UrlFileExtensionsTest
    {
        [Fact]
        public static void TestGetUrlWithExtension()
        {
            UrlDir.UrlFile urlFile = UrlBuilder.CreateFile("abc/def/ghi.cfg");
            Assert.Equal("abc/def/ghi.cfg", urlFile.GetUrlWithExtension());
        }
    }
}
