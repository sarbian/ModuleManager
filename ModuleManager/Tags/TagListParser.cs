using System;
using System.Collections.Generic;
using ModuleManager.Progress;

namespace ModuleManager.Tags
{
    public interface ITagListParser
    {
        ITagList Parse(string ToParse, UrlDir.UrlConfig urlConfig);
    }

    public class TagListParser : ITagListParser
    {
        private readonly IPatchProgress progress;

        public TagListParser(IPatchProgress progress)
        {
            this.progress = progress ?? throw new ArgumentNullException(nameof(progress));
        }

        public ITagList Parse(string toParse, UrlDir.UrlConfig urlConfig)
        {
            if (toParse == null) throw new ArgumentNullException(nameof(toParse));
            if (urlConfig == null) throw new ArgumentNullException(nameof(urlConfig));
            if (toParse.Length == 0) throw new FormatException("can't create tag list from empty string");
            if (toParse[0] == '[') throw new FormatException("can't create tag list beginning with [");
            if (toParse[0] == ':') throw new FormatException("can't create tag list beginning with :");

            if (toParse[toParse.Length - 1] == ':')
            {
                progress.Warning(urlConfig, "trailing : detected");
                toParse = toParse.TrimEnd(':');
            }

            List<Tag> tags = new List<Tag>();
            Tag primaryTag = ParsePrimaryTag(toParse, ref tags, urlConfig);
            return new TagList(primaryTag, tags);
        }

        private Tag ParsePrimaryTag(string toParse, ref List<Tag> tags, UrlDir.UrlConfig urlConfig)
        {
            for (int i = 1; i < toParse.Length; i++)
            {
                char c = toParse[i];

                if (c == '[')
                {
                    int j = ClosingBracketIndex(toParse, i + 1);
                    return ParsePrimaryTrailer(toParse, j + 1, ref tags, toParse.Substring(0, i), toParse.Substring(i + 1, j - i - 1), urlConfig);
                }
                else if (c == ':')
                {
                    ParseTag(toParse, i + 1, ref tags, urlConfig);
                    return new Tag(toParse.Substring(0, i), null, null);
                }
                else if (c == ']')
                {
                    throw new FormatException("encountered closing bracket in primary key");
                }
            }

            return new Tag(toParse, null, null);
        }

        private Tag ParsePrimaryTrailer(string toParse, int start, ref List<Tag> tags, string primaryKey, string primaryValue, UrlDir.UrlConfig urlConfig)
        {
            for (int i = start; i < toParse.Length; i++)
            {
                char c = toParse[i];

                if (c == ':')
                {
                    string trailer = i == start ? null : toParse.Substring(start, i - start);
                    ParseTag(toParse, i + 1, ref tags, urlConfig);
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

        private void ParseTag(string toParse, int start, ref List<Tag> tags, UrlDir.UrlConfig urlConfig)
        {
            for (int i = start; i < toParse.Length; i++)
            {
                char c = toParse[i];

                if (c == '[')
                {
                    if (i == start)
                        throw new FormatException("tag can't start with [");

                    int j = ClosingBracketIndex(toParse, i + 1);
                    ParseTrailer(toParse, j + 1, ref tags, toParse.Substring(start, i - start), toParse.Substring(i + 1, j - i - 1), urlConfig);
                    return;
                }
                else if (c == ':')
                {
                    if (i == start)
                        progress.Warning(urlConfig, "extra : detected");
                    else
                        tags.Add(new Tag(toParse.Substring(start, i - start), null, null));

                    ParseTag(toParse, i + 1, ref tags, urlConfig);
                    return;
                }
                else if (c == ']')
                {
                    throw new FormatException("encountered closing bracket in key");
                }
            }

            tags.Add(new Tag(toParse.Substring(start), null, null));
        }

        private void ParseTrailer(string toParse, int start, ref List<Tag> tags, string key, string value, UrlDir.UrlConfig urlConfig)
        {
            for (int i = start; i < toParse.Length; i++)
            {
                char c = toParse[i];

                if (c == ':')
                {
                    string trailer = i == start ? null : toParse.Substring(start, i - start);
                    tags.Add(new Tag(key, value, trailer));
                    ParseTag(toParse, i + 1, ref tags, urlConfig);
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
