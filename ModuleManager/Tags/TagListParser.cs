using System;
using System.Collections.Generic;

namespace ModuleManager.Tags
{
    public interface ITagListParser
    {
        ITagList Parse(string ToParse);
    }

    public class TagListParser : ITagListParser
    {
        public ITagList Parse(string toParse)
        {
            if (toParse == null) throw new ArgumentNullException(nameof(toParse));
            if (toParse.Length == 0) throw new FormatException("can't create tag list from empty string");
            if (toParse[0] == '[') throw new FormatException("can't create tag list beginning with [");
            if (toParse[0] == ':') throw new FormatException("can't create tag list beginning with :");
            if (toParse[toParse.Length - 1] == ':') throw new FormatException("tag list can't end with :");

            List<Tag> tags = new List<Tag>();
            Tag primaryTag = ParsePrimaryTag(toParse, ref tags);
            return new TagList(primaryTag, tags);
        }

        private static Tag ParsePrimaryTag(string toParse, ref List<Tag> tags)
        {
            for (int i = 1; i < toParse.Length; i++)
            {
                char c = toParse[i];

                if (c == '[')
                {
                    int j = ClosingBracketIndex(toParse, i + 1);
                    return ParsePrimaryTrailer(toParse, j + 1, ref tags, toParse.Substring(0, i), toParse.Substring(i + 1, j - i - 1));
                }
                else if (c == ':')
                {
                    ParseTag(toParse, i + 1, ref tags);
                    return new Tag(toParse.Substring(0, i), null, null);
                }
                else if (c == ']')
                {
                    throw new FormatException("encountered closing bracket in primary key");
                }
            }

            return new Tag(toParse, null, null);
        }

        private static Tag ParsePrimaryTrailer(string toParse, int start, ref List<Tag> tags, string primaryKey, string primaryValue)
        {
            for (int i = start; i < toParse.Length; i++)
            {
                char c = toParse[i];

                if (c == ':')
                {
                    string trailer = i == start ? null : toParse.Substring(start, i - start);
                    ParseTag(toParse, i + 1, ref tags);
                    return new Tag(primaryKey, primaryValue, trailer);
                }
                else if (c == '[')
                {
                    throw new FormatException("encountered opening bracket in primary trailer");
                }
                else if (c == ']')
                {
                    throw new FormatException("encountered closing bracket in primary trailer");
                }
            }
            
            string primaryTrailer = toParse.Length - start == 0 ? null : toParse.Substring(start);
            return new Tag(primaryKey, primaryValue, primaryTrailer);
        }

        private static void ParseTag(string toParse, int start, ref List<Tag> tags)
        {
            for (int i = start; i < toParse.Length; i++)
            {
                char c = toParse[i];

                if (c == '[')
                {
                    if (i == start)
                        throw new FormatException("tag can't start with [");

                    int j = ClosingBracketIndex(toParse, i + 1);
                    ParseTrailer(toParse, j + 1, ref tags, toParse.Substring(start, i - start), toParse.Substring(i + 1, j - i - 1));
                    return;
                }
                else if (c == ':')
                {
                    if (i == start)
                        throw new FormatException("tag can't start with :");

                    tags.Add(new Tag(toParse.Substring(start, i - start), null, null));
                    ParseTag(toParse, i + 1, ref tags);
                    return;
                }
                else if (c == ']')
                {
                    throw new FormatException("encountered closing bracket in key");
                }
            }

            tags.Add(new Tag(toParse.Substring(start), null, null));
        }

        private static void ParseTrailer(string toParse, int start, ref List<Tag> tags, string key, string value)
        {
            for (int i = start; i < toParse.Length; i++)
            {
                char c = toParse[i];

                if (c == ':')
                {
                    string trailer = i == start ? null : toParse.Substring(start, i - start);
                    tags.Add(new Tag(key, value, trailer));
                    ParseTag(toParse, i + 1, ref tags);
                    return;
                }
                else if (c == '[')
                {
                    throw new FormatException("encountered opening bracket in trailer");
                }
                else if (c == ']')
                {
                    throw new FormatException("encountered closing bracket in trailer");
                }
            }

            string finalTrailer = toParse.Length - start == 0 ? null : toParse.Substring(start);
            tags.Add(new Tag(key, value, finalTrailer));
        }

        private static int ClosingBracketIndex(string toParse, int start)
        {
            int bracketLevel = 0;

            for (int i = start; i < toParse.Length; i++)
            {
                char c = toParse[i];

                if (c == '[')
                {
                    bracketLevel++;
                }
                else if (c == ']')
                {
                    bracketLevel--;
                }

                if (bracketLevel == -1) return i;
            }

            throw new FormatException("reached end of the tag list without encountering a close bracket");
        }
    }
}
