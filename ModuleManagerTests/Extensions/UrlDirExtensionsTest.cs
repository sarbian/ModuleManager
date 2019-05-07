using System;
using Xunit;
using TestUtils;
using ModuleManager.Extensions;

namespace ModuleManagerTests.Extensions
{
    public class UrlDirExtensionsTest
    {
        [Fact]
        public void TestFindFile__IndirectChild()
        {
            UrlDir urlDir = UrlBuilder.CreateDir("abc");
            UrlDir.UrlFile urlFile = UrlBuilder.CreateFile("def/ghi.cfg", urlDir);

            Assert.Equal(urlFile, urlDir.FindFile("def/ghi"));
        }

        [Fact]
        public void TestFindFile__DirectChild()
        {
            UrlDir urlDir = UrlBuilder.CreateDir("abc");
            UrlDir.UrlFile urlFile = UrlBuilder.CreateFile("def.cfg", urlDir);

            Assert.Equal(urlFile, urlDir.FindFile("def"));
        }

        [Fact]
        public void TestFindFile__Extension()
        {
            UrlDir urlDir = UrlBuilder.CreateDir("abc");
            UrlBuilder.CreateFile("def/ghi.yyy", urlDir);
            UrlDir.UrlFile urlFile = UrlBuilder.CreateFile("def/ghi.cfg", urlDir);
            UrlBuilder.CreateFile("def/ghi.zzz", urlDir);

            Assert.Equal(urlFile, urlDir.FindFile("def/ghi.cfg"));
        }

        [Fact]
        public void TestFindFile__FileType()
        {
            UrlDir urlDir = UrlBuilder.CreateDir("abc");
            UrlBuilder.CreateFile("def/ghi.yyy", urlDir);
            UrlDir.UrlFile urlFile = UrlBuilder.CreateFile("def/ghi.cfg", urlDir);
            UrlBuilder.CreateFile("def/ghi.zzz", urlDir);

            Assert.Equal(urlFile, urlDir.FindFile("def/ghi", UrlDir.FileType.Config));
        }

        [Fact]
        public void TestFindFile__NotFound()
        {
            UrlDir urlDir = UrlBuilder.CreateDir("abc");
            UrlBuilder.CreateDir("def", urlDir);

            Assert.Null(urlDir.FindFile("def/ghi"));
        }

        [Fact]
        public void TestFindFile__Extension__NotFound()
        {
            UrlDir urlDir = UrlBuilder.CreateDir("abc");
            UrlBuilder.CreateFile("def/ghi.yyy", urlDir);
            UrlBuilder.CreateFile("def/ghi.zzz", urlDir);

            Assert.Null(urlDir.FindFile("def/ghi.cfg"));
        }

        [Fact]
        public void TestFindFile__FileType__NotFound()
        {
            UrlDir urlDir = UrlBuilder.CreateDir("abc");
            UrlBuilder.CreateFile("def/ghi.yyy", urlDir);
            UrlBuilder.CreateFile("def/ghi.zzz", urlDir);

            Assert.Null(urlDir.FindFile("def/ghi", UrlDir.FileType.Config));
        }

        [Fact]
        public void TestFindFile__IntermediateDirectoryNotFound()
        {
            UrlDir urlDir = UrlBuilder.CreateDir("abc");
            Assert.Null(urlDir.FindFile("def/ghi"));
        }

        [Fact]
        public void TestFindFile__UrlDirNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                UrlDirExtensions.FindFile(null, "abc");
            });

            Assert.Equal("urlDir", ex.ParamName);
        }

        [Fact]
        public void TestFindFile__UrlNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                UrlDirExtensions.FindFile(UrlBuilder.CreateDir("abc"), null);
            });

            Assert.Equal("url", ex.ParamName);
        }
    }
}
