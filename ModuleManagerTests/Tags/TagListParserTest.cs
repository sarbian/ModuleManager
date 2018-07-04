using System;
using Xunit;
using ModuleManager.Tags;

namespace ModuleManagerTests.Tags
{
    public class TagListParserTest
    {
        private readonly TagListParser tagListParser = new TagListParser();

        [Fact]
        public void TestParse__OnlyPrimaryKey()
        {
            ITagList tagList = tagListParser.Parse("01");
            Assert.Equal(new Tag("01", null, null), tagList.PrimaryTag);
            Assert.Empty(tagList);
        }

        [Fact]
        public void TestParse__OnlyPrimaryKeyAndValue()
        {
            ITagList tagList = tagListParser.Parse("01[02]");
            Assert.Equal(new Tag("01", "02", null), tagList.PrimaryTag);
            Assert.Empty(tagList);
        }

        [Fact]
        public void TestParse__OnlyPrimaryKeyValueAndTrailer()
        {
            ITagList tagList = tagListParser.Parse("01[02]03");
            Assert.Equal(new Tag("01", "02", "03"), tagList.PrimaryTag);
            Assert.Empty(tagList);
        }

        [Fact]
        public void TestParse__OnlyPrimaryKeyValueAndTrailer__ValueHasSomeStuff()
        {
            ITagList tagList = tagListParser.Parse("01[02:[03:04[05]]]06");
            Assert.Equal(new Tag("01", "02:[03:04[05]]", "06"), tagList.PrimaryTag);
            Assert.Empty(tagList);
        }

        [Fact]
        public void TestParse__TagWithOnlyKey()
        {
            ITagList tagList = tagListParser.Parse("01[02]03:04:05");
            Assert.Equal(new Tag("01", "02", "03"), tagList.PrimaryTag);
            Assert.Equal(new[] {
                new Tag("04", null, null),
                new Tag("05", null, null),
            }, tagList);
        }

        [Fact]
        public void TestParse__TagWithOnlyKeyAndValue()
        {
            ITagList tagList = tagListParser.Parse("01[02]03:04[05]:06[07]");
            Assert.Equal(new Tag("01", "02", "03"), tagList.PrimaryTag);
            Assert.Equal(new[] {
                new Tag("04", "05", null),
                new Tag("06", "07", null),
            }, tagList);
        }

        [Fact]
        public void TestParse__FullTags()
        {
            ITagList tagList = tagListParser.Parse("01[02]03:04[05]06:07[08]09");
            Assert.Equal(new Tag("01", "02", "03"), tagList.PrimaryTag);
            Assert.Equal(new[] {
                new Tag("04", "05", "06"),
                new Tag("07", "08", "09"),
            }, tagList);
        }

        [Fact]
        public void TestParse__MixedTags()
        {
            ITagList tagList = tagListParser.Parse("01[02]03:04:05[06]:07[08]09");
            Assert.Equal(new Tag("01", "02", "03"), tagList.PrimaryTag);
            Assert.Equal(new[] {
                new Tag("04", null, null),
                new Tag("05", "06", null),
                new Tag("07", "08", "09"),
            }, tagList);
        }

        [Fact]
        public void TestParse__Null()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                tagListParser.Parse(null);
            });

            Assert.Equal("toParse", ex.ParamName);
        }

        [Fact]
        public void TestParse__Empty()
        {
            FormatException ex = Assert.Throws<FormatException>(delegate
            {
                tagListParser.Parse("");
            });

            Assert.Equal("can't create tag list from empty string", ex.Message);
        }

        [Fact]
        public void TestParse__StartsWithOpenBracket()
        {
            FormatException ex = Assert.Throws<FormatException>(delegate
            {
                tagListParser.Parse("[stuff]");
            });

            Assert.Equal("can't create tag list beginning with [", ex.Message);
        }

        [Fact]
        public void TestParse__StartsWithColon()
        {
            FormatException ex = Assert.Throws<FormatException>(delegate
            {
                tagListParser.Parse(":stuff");
            });

            Assert.Equal("can't create tag list beginning with :", ex.Message);
        }

        [Fact]
        public void TestParse__EndsWithColon()
        {
            FormatException ex = Assert.Throws<FormatException>(delegate
            {
                tagListParser.Parse("stuff:blah:");
            });

            Assert.Equal("tag list can't end with :", ex.Message);
        }

        [Fact]
        public void TestParse__ClosingBracketInPrimaryKey()
        {
            FormatException ex = Assert.Throws<FormatException>(delegate
            {
                tagListParser.Parse("abc]def");
            });

            Assert.Equal("encountered closing bracket in primary key", ex.Message);
        }

        [Fact]
        public void TestParse__PrimaryValueHasNoClosingBracket()
        {
            FormatException ex = Assert.Throws<FormatException>(delegate
            {
                tagListParser.Parse("abc[def[ghi]jkl");
            });

            Assert.Equal("reached end of the tag list without encountering a close bracket", ex.Message);
        }

        [Fact]
        public void TestParse__OpeningBracketInPrimaryTrailer()
        {
            FormatException ex = Assert.Throws<FormatException>(delegate
            {
                tagListParser.Parse("abc[def]ghi[jkl]");
            });

            Assert.Equal("encountered opening bracket in primary trailer", ex.Message);
        }

        [Fact]
        public void TestParse__ClosingBracketInPrimaryTrailer()
        {
            FormatException ex = Assert.Throws<FormatException>(delegate
            {
                tagListParser.Parse("abc[def]ghi]jkl");
            });

            Assert.Equal("encountered closing bracket in primary trailer", ex.Message);
        }

        [Fact]
        public void TestParse__TagStartsWithOpenBracket()
        {
            FormatException ex = Assert.Throws<FormatException>(delegate
            {
                tagListParser.Parse("abc:def:[ghi]");
            });

            Assert.Equal("tag can't start with [", ex.Message);
        }

        [Fact]
        public void TestParse__TagStartsWithColon()
        {
            FormatException ex = Assert.Throws<FormatException>(delegate
            {
                tagListParser.Parse("abc:def::ghi");
            });

            Assert.Equal("tag can't start with :", ex.Message);
        }

        [Fact]
        public void TestParse__ClosingBracketInKey()
        {
            FormatException ex = Assert.Throws<FormatException>(delegate
            {
                tagListParser.Parse("abc:def:ghi]jkl");
            });

            Assert.Equal("encountered closing bracket in key", ex.Message);
        }

        [Fact]
        public void TestParse__ValueHasNoClosingBracket()
        {
            FormatException ex = Assert.Throws<FormatException>(delegate
            {
                tagListParser.Parse("abc:def:ghi[jkl[mno]pqr");
            });

            Assert.Equal("reached end of the tag list without encountering a close bracket", ex.Message);
        }

        [Fact]
        public void TestParse__OpeningBracketInTrailer()
        {
            FormatException ex = Assert.Throws<FormatException>(delegate
            {
                tagListParser.Parse("abc:def:ghi[jkl]mno[pqr]");
            });

            Assert.Equal("encountered opening bracket in trailer", ex.Message);
        }

        [Fact]
        public void TestParse__ClosingBracketInTrailer()
        {
            FormatException ex = Assert.Throws<FormatException>(delegate
            {
                tagListParser.Parse("abc:def:ghi[jkl]mno]pqr");
            });

            Assert.Equal("encountered closing bracket in trailer", ex.Message);
        }
    }
}
