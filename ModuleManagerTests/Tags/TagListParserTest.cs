using System;
using Xunit;
using NSubstitute;
using TestUtils;
using ModuleManager.Progress;
using ModuleManager.Tags;

namespace ModuleManagerTests.Tags
{
    public class TagListParserTest
    {
        private readonly IPatchProgress progress = Substitute.For<IPatchProgress>();
        private readonly TagListParser tagListParser;
        private readonly UrlDir.UrlConfig urlConfig = UrlBuilder.CreateConfig("abc/def.cfg", new ConfigNode("BLAH"));

        public TagListParserTest()
        {
            tagListParser = new TagListParser(progress);
        }

        [Fact]
        public void TestConstructor__ProgressNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new TagListParser(null);
            });

            Assert.Equal("progress", ex.ParamName);
        }

        [Fact]
        public void TestParse__OnlyPrimaryKey()
        {
            ITagList tagList = tagListParser.Parse("01", urlConfig);
            Assert.Equal(new Tag("01", null, null), tagList.PrimaryTag);
            Assert.Empty(tagList);
        }

        [Fact]
        public void TestParse__OnlyPrimaryKeyAndValue()
        {
            ITagList tagList = tagListParser.Parse("01[02]", urlConfig);
            Assert.Equal(new Tag("01", "02", null), tagList.PrimaryTag);
            Assert.Empty(tagList);
        }

        [Fact]
        public void TestParse__OnlyPrimaryKeyValueAndTrailer()
        {
            ITagList tagList = tagListParser.Parse("01[02]03", urlConfig);
            Assert.Equal(new Tag("01", "02", "03"), tagList.PrimaryTag);
            Assert.Empty(tagList);
        }

        [Fact]
        public void TestParse__OnlyPrimaryKeyValueAndTrailer__ValueHasSomeStuff()
        {
            ITagList tagList = tagListParser.Parse("01[02:[03:04[05]]]06", urlConfig);
            Assert.Equal(new Tag("01", "02:[03:04[05]]", "06"), tagList.PrimaryTag);
            Assert.Empty(tagList);
        }

        [Fact]
        public void TestParse__TagWithOnlyKey()
        {
            ITagList tagList = tagListParser.Parse("01[02]03:04:05", urlConfig);
            Assert.Equal(new Tag("01", "02", "03"), tagList.PrimaryTag);
            Assert.Equal(new[] {
                new Tag("04", null, null),
                new Tag("05", null, null),
            }, tagList);
        }

        [Fact]
        public void TestParse__TagWithOnlyKeyAndValue()
        {
            ITagList tagList = tagListParser.Parse("01[02]03:04[05]:06[07]", urlConfig);
            Assert.Equal(new Tag("01", "02", "03"), tagList.PrimaryTag);
            Assert.Equal(new[] {
                new Tag("04", "05", null),
                new Tag("06", "07", null),
            }, tagList);
        }

        [Fact]
        public void TestParse__FullTags()
        {
            ITagList tagList = tagListParser.Parse("01[02]03:04[05]06:07[08]09", urlConfig);
            Assert.Equal(new Tag("01", "02", "03"), tagList.PrimaryTag);
            Assert.Equal(new[] {
                new Tag("04", "05", "06"),
                new Tag("07", "08", "09"),
            }, tagList);
        }

        [Fact]
        public void TestParse__MixedTags()
        {
            ITagList tagList = tagListParser.Parse("01[02]03:04:05[06]:07[08]09", urlConfig);
            Assert.Equal(new Tag("01", "02", "03"), tagList.PrimaryTag);
            Assert.Equal(new[] {
                new Tag("04", null, null),
                new Tag("05", "06", null),
                new Tag("07", "08", "09"),
            }, tagList);
        }

        [Fact]
        public void TestParse__StringNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                tagListParser.Parse(null, urlConfig);
            });

            Assert.Equal("toParse", ex.ParamName);
        }

        [Fact]
        public void TestParse__UrlConfigNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                tagListParser.Parse("BLAH", null);
            });

            Assert.Equal("urlConfig", ex.ParamName);
        }

        [Fact]
        public void TestParse__Empty()
        {
            FormatException ex = Assert.Throws<FormatException>(delegate
            {
                tagListParser.Parse("", urlConfig);
            });

            Assert.Equal("can't create tag list from empty string", ex.Message);
        }

        [Fact]
        public void TestParse__StartsWithOpenBracket()
        {
            FormatException ex = Assert.Throws<FormatException>(delegate
            {
                tagListParser.Parse("[stuff]", urlConfig);
            });

            Assert.Equal("can't create tag list beginning with [", ex.Message);
        }

        [Fact]
        public void TestParse__StartsWithColon()
        {
            FormatException ex = Assert.Throws<FormatException>(delegate
            {
                tagListParser.Parse(":stuff", urlConfig);
            });

            Assert.Equal("can't create tag list beginning with :", ex.Message);
        }

        [Fact]
        public void TestParse__EndsWithColon()
        {
            ITagList tagList = tagListParser.Parse("stuff:blah::", urlConfig);

            progress.Received().Warning(urlConfig, "trailing : detected");

            Assert.Equal(new Tag("stuff", null, null), tagList.PrimaryTag);
            Assert.Equal(new[] {
                new Tag("blah", null, null),
            }, tagList);
        }

        [Fact]
        public void TestParse__ClosingBracketInPrimaryKey()
        {
            FormatException ex = Assert.Throws<FormatException>(delegate
            {
                tagListParser.Parse("abc]def", urlConfig);
            });

            Assert.Equal("encountered closing bracket in primary key", ex.Message);
        }

        [Fact]
        public void TestParse__PrimaryValueHasNoClosingBracket()
        {
            FormatException ex = Assert.Throws<FormatException>(delegate
            {
                tagListParser.Parse("abc[def[ghi]jkl", urlConfig);
            });

            Assert.Equal("reached end of the tag list without encountering a close bracket", ex.Message);
        }

        [Fact]
        public void TestParse__OpeningBracketInPrimaryTrailer()
        {
            FormatException ex = Assert.Throws<FormatException>(delegate
            {
                tagListParser.Parse("abc[def]ghi[jkl]", urlConfig);
            });

            Assert.Equal("encountered opening bracket in primary trailer", ex.Message);
        }

        [Fact]
        public void TestParse__ClosingBracketInPrimaryTrailer()
        {
            FormatException ex = Assert.Throws<FormatException>(delegate
            {
                tagListParser.Parse("abc[def]ghi]jkl", urlConfig);
            });

            Assert.Equal("encountered closing bracket in primary trailer", ex.Message);
        }

        [Fact]
        public void TestParse__TagStartsWithOpenBracket()
        {
            FormatException ex = Assert.Throws<FormatException>(delegate
            {
                tagListParser.Parse("abc:def:[ghi]", urlConfig);
            });

            Assert.Equal("tag can't start with [", ex.Message);
        }

        [Fact]
        public void TestParse__TagStartsWithColon()
        {
            ITagList tagList = tagListParser.Parse("abc:def::ghi", urlConfig);

            progress.Received().Warning(urlConfig, "extra : detected");

            Assert.Equal(new Tag("abc", null, null), tagList.PrimaryTag);
            Assert.Equal(new[] {
                new Tag("def", null, null),
                new Tag("ghi", null, null),
            }, tagList);
        }

        [Fact]
        public void TestParse__ClosingBracketInKey()
        {
            FormatException ex = Assert.Throws<FormatException>(delegate
            {
                tagListParser.Parse("abc:def:ghi]jkl", urlConfig);
            });

            Assert.Equal("encountered closing bracket in key", ex.Message);
        }

        [Fact]
        public void TestParse__ValueHasNoClosingBracket()
        {
            FormatException ex = Assert.Throws<FormatException>(delegate
            {
                tagListParser.Parse("abc:def:ghi[jkl[mno]pqr", urlConfig);
            });

            Assert.Equal("reached end of the tag list without encountering a close bracket", ex.Message);
        }

        [Fact]
        public void TestParse__OpeningBracketInTrailer()
        {
            FormatException ex = Assert.Throws<FormatException>(delegate
            {
                tagListParser.Parse("abc:def:ghi[jkl]mno[pqr]", urlConfig);
            });

            Assert.Equal("encountered opening bracket in trailer", ex.Message);
        }

        [Fact]
        public void TestParse__ClosingBracketInTrailer()
        {
            FormatException ex = Assert.Throws<FormatException>(delegate
            {
                tagListParser.Parse("abc:def:ghi[jkl]mno]pqr", urlConfig);
            });

            Assert.Equal("encountered closing bracket in trailer", ex.Message);
        }
    }
}
