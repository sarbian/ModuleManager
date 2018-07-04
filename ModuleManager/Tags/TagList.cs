using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ModuleManager.Collections;

namespace ModuleManager.Tags
{
    public interface ITagList : IEnumerable<Tag>
    {
        Tag PrimaryTag { get; }
    }

    public class TagList : ITagList
    {
        private readonly Tag[] tags;

        public TagList(Tag primaryTag, IEnumerable<Tag> tags)
        {
            PrimaryTag = primaryTag;
            this.tags = tags?.ToArray() ?? throw new ArgumentNullException(nameof(tags));
        }
        
        public Tag PrimaryTag { get; private set; }

        public ArrayEnumerator<Tag> GetEnumerator() => new ArrayEnumerator<Tag>(tags);
        IEnumerator<Tag> IEnumerable<Tag>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
