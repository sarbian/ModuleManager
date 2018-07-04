using System;
using ModuleManager.Extensions;
using ModuleManager.Patches.PassSpecifiers;
using ModuleManager.Progress;
using ModuleManager.Tags;

namespace ModuleManager.Patches
{
    public interface IProtoPatchBuilder
    {
        ProtoPatch Build(UrlDir.UrlConfig urlConfig, Command command, ITagList tagList);
    }

    public class ProtoPatchBuilder : IProtoPatchBuilder
    {
        private readonly IPatchProgress progress;

        public ProtoPatchBuilder(IPatchProgress progress)
        {
            this.progress = progress ?? throw new ArgumentNullException(nameof(progress));
        }

        public ProtoPatch Build(UrlDir.UrlConfig urlConfig, Command command, ITagList tagList)
        {
            if (urlConfig == null) throw new ArgumentNullException(nameof(urlConfig));
            if (tagList == null) throw new ArgumentNullException(nameof(tagList));
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            
            bool error = false;

            string nodeType = tagList.PrimaryTag.key;
            string nodeName = tagList.PrimaryTag.value;

            if (command == Command.Insert && nodeName != null)
            {
                progress.Error(urlConfig, "name specifier detected on insert node (not a patch): " + urlConfig.SafeUrl());
                error = true;
            }

            if (nodeName == string.Empty)
            {
                progress.Warning(urlConfig, "empty brackets detected on patch name: " + urlConfig.SafeUrl());
                nodeName = null;
            }
            
            if (tagList.PrimaryTag.trailer != null)
                progress.Warning(urlConfig, "unrecognized trailer: '" + tagList.PrimaryTag.trailer + "' on: " + urlConfig.SafeUrl());

            string needs = null;
            string has = null;
            IPassSpecifier passSpecifier = null;

            foreach (Tag tag in tagList)
            {
                if (tag.trailer != null) 
                    progress.Warning(urlConfig, "unrecognized trailer: '" + tag.trailer + "' on: " + urlConfig.SafeUrl());

                if (tag.key.Equals("NEEDS", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (needs != null)
                    {
                        progress.Warning(urlConfig, "more than one :NEEDS tag detected, ignoring all but the first: " + urlConfig.SafeUrl());
                        continue;
                    }
                    if (string.IsNullOrEmpty(tag.value))
                    {
                        progress.Error(urlConfig, "empty :NEEDS tag detected: " + urlConfig.SafeUrl());
                        error = true;
                        continue;
                    }

                    needs = tag.value;
                }
                else if (tag.key.Equals("HAS", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (command == Command.Insert)
                    {
                        progress.Error(urlConfig, ":HAS detected on insert node (not a patch): " + urlConfig.SafeUrl());
                        error = true;
                        continue;
                    }
                    if (has != null)
                    {
                        progress.Warning(urlConfig, "more than one :HAS tag detected, ignoring all but the first: " + urlConfig.SafeUrl());
                        continue;
                    }
                    if (string.IsNullOrEmpty(tag.value))
                    {
                        progress.Error(urlConfig, "empty :HAS tag detected: " + urlConfig.SafeUrl());
                        error = true;
                        continue;
                    }

                    has = tag.value;
                }
                else if (tag.key.Equals("FIRST", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (tag.value != null)
                    {
                        progress.Warning(urlConfig, "value detected on :FIRST tag: " + urlConfig.SafeUrl());
                    }

                    if (command == Command.Insert)
                    {
                        progress.Error(urlConfig, "pass specifier detected on insert node (not a patch): " + urlConfig.SafeUrl());
                        error = true;
                        continue;
                    }
                    if (passSpecifier != null)
                    {
                        progress.Warning(urlConfig, "more than one pass specifier detected, ignoring all but the first: " + urlConfig.SafeUrl());
                        continue;
                    }

                    passSpecifier = new FirstPassSpecifier();
                }
                else if (tag.key.Equals("BEFORE", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (string.IsNullOrEmpty(tag.value))
                    {
                        progress.Error(urlConfig, "empty :BEFORE tag detected: " + urlConfig.SafeUrl());
                        error = true;
                        continue;
                    }
                    
                    if (command == Command.Insert)
                    {
                        progress.Error(urlConfig, "pass specifier detected on insert node (not a patch): " + urlConfig.SafeUrl());
                        error = true;
                        continue;
                    }
                    if (passSpecifier != null)
                    {
                        progress.Warning(urlConfig, "more than one pass specifier detected, ignoring all but the first: " + urlConfig.SafeUrl());
                        continue;
                    }

                    passSpecifier = new BeforePassSpecifier(tag.value, urlConfig);
                }
                else if (tag.key.Equals("FOR", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (string.IsNullOrEmpty(tag.value))
                    {
                        progress.Error(urlConfig, "empty :FOR tag detected: " + urlConfig.SafeUrl());
                        error = true;
                        continue;
                    }

                    if (command == Command.Insert)
                    {
                        progress.Error(urlConfig, "pass specifier detected on insert node (not a patch): " + urlConfig.SafeUrl());
                        error = true;
                        continue;
                    }
                    if (passSpecifier != null)
                    {
                        progress.Warning(urlConfig, "more than one pass specifier detected, ignoring all but the first: " + urlConfig.SafeUrl());
                        continue;
                    }

                    passSpecifier = new ForPassSpecifier(tag.value, urlConfig);
                }
                else if (tag.key.Equals("AFTER", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (string.IsNullOrEmpty(tag.value))
                    {
                        progress.Error(urlConfig, "empty :AFTER tag detected: " + urlConfig.SafeUrl());
                        error = true;
                        continue;
                    }

                    if (command == Command.Insert)
                    {
                        progress.Error(urlConfig, "pass specifier detected on insert node (not a patch): " + urlConfig.SafeUrl());
                        error = true;
                        continue;
                    }
                    if (passSpecifier != null)
                    {
                        progress.Warning(urlConfig, "more than one pass specifier detected, ignoring all but the first: " + urlConfig.SafeUrl());
                        continue;
                    }

                    passSpecifier = new AfterPassSpecifier(tag.value, urlConfig);
                }
                else if (tag.key.Equals("FINAL", StringComparison.CurrentCultureIgnoreCase))
                {
                    if (tag.value != null)
                    {
                        progress.Warning(urlConfig, "value detected on :FINAL tag: " + urlConfig.SafeUrl());
                    }

                    if (command == Command.Insert)
                    {
                        progress.Error(urlConfig, "pass specifier detected on insert node (not a patch): " + urlConfig.SafeUrl());
                        error = true;
                        continue;
                    }
                    if (passSpecifier != null)
                    {
                        progress.Warning(urlConfig, "more than one pass specifier detected, ignoring all but the first: " + urlConfig.SafeUrl());
                        continue;
                    }

                    passSpecifier = new FinalPassSpecifier();
                }
                else
                {
                    progress.Warning(urlConfig, "unrecognized tag: '" + tag.key + "' on: " + urlConfig.SafeUrl());
                }
            }

            if (error) return null;

            if (passSpecifier == null)
            {
                if (command == Command.Insert) passSpecifier = new InsertPassSpecifier();
                else passSpecifier = new LegacyPassSpecifier();
            }

            return new ProtoPatch(urlConfig, command, nodeType, nodeName, needs, has, passSpecifier);
        }
    }
}
