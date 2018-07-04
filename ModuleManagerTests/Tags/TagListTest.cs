using System;
using Xunit;
using ModuleManager.Tags;

namespace ModuleManagerTests.Tags
{
    public class TagListTest
    {
        [Fact]
        public void TestPrimaryTag()
        {
            Tag primaryTag = new Tag("stuff", null, null);
            TagList tagList = new TagList(primaryTag, new Tag[0]);

            Assert.Equal(primaryTag, tagList.PrimaryTag);
        }

        [Fact]
        public void TestEnumeration()
        {
            Tag primaryTag = new Tag("stuff", null, null);
            Tag tag1 = new Tag("tag1", null, null);
            Tag tag2 = new Tag("tag2", null, null);

            Tag[] tags = new Tag[] { tag1, tag2 };
            TagList tagList = new TagList(primaryTag, tags);

            tags[0] = new Tag("tag3", null, null);

            Assert.Equal(new[] { tag1, tag2 }, tagList);
        }

        [Fact]
        public void TestConstructor__TagsNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new TagList(new Tag("blah", null, null), null);
            });

            Assert.Equal("tags", ex.ParamName);
        }
    }
}
