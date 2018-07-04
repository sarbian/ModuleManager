using System;
using ModuleManager.Extensions;
using ModuleManager.Logging;
using ModuleManager.Patches;
using ModuleManager.Progress;
using ModuleManager.Tags;

namespace ModuleManager
{
    public class PatchExtractor
    {
        private readonly IPatchProgress progress;
        private readonly IBasicLogger logger;
        private readonly INeedsChecker needsChecker;
        private readonly ITagListParser tagListParser;
        private readonly IProtoPatchBuilder protoPatchBuilder;
        private readonly IPatchCompiler patchCompiler;

        public PatchExtractor(
            IPatchProgress progress,
            IBasicLogger logger,
            INeedsChecker needsChecker,
            ITagListParser tagListParser,
            IProtoPatchBuilder protoPatchBuilder,
            IPatchCompiler patchCompiler
        )
        {
            this.progress = progress ?? throw new ArgumentNullException(nameof(progress));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.needsChecker = needsChecker ?? throw new ArgumentNullException(nameof(needsChecker));
            this.tagListParser = tagListParser ?? throw new ArgumentNullException(nameof(tagListParser));
            this.protoPatchBuilder = protoPatchBuilder ?? throw new ArgumentNullException(nameof(protoPatchBuilder));
            this.patchCompiler = patchCompiler ?? throw new ArgumentNullException(nameof(patchCompiler));
        }

        public IPatch ExtractPatch(UrlDir.UrlConfig urlConfig)
        {
            if (urlConfig == null) throw new ArgumentNullException(nameof(urlConfig));

            try
            {
                int index = urlConfig.parent.configs.IndexOf(urlConfig);
                urlConfig.parent.configs.RemoveAt(index);

                if (!urlConfig.type.IsBracketBalanced())
                {
                    progress.Error(urlConfig, "Error - node name does not have balanced brackets (or a space - if so replace with ?):\n" + urlConfig.SafeUrl());
                    return null;
                }

                Command command = CommandParser.Parse(urlConfig.type, out string name);
                
                if (command == Command.Replace)
                {
                    progress.Error(urlConfig, $"Error - replace command (%) is not valid on a root node: {urlConfig.SafeUrl()}");
                    return null;
                }
                else if (command == Command.Create)
                {
                    progress.Error(urlConfig, $"Error - create command (&) is not valid on a root node: {urlConfig.SafeUrl()}");
                    return null;
                }
                else if (command == Command.Rename)
                {
                    progress.Error(urlConfig, $"Error - rename command (|) is not valid on a root node: {urlConfig.SafeUrl()}");
                    return null;
                }
                else if (command == Command.Paste)
                {
                    progress.Error(urlConfig, $"Error - paste command (#) is not valid on a root node: {urlConfig.SafeUrl()}");
                    return null;
                }
                else if (command == Command.Special)
                {
                    progress.Error(urlConfig, $"Error - special command (*) is not valid on a root node: {urlConfig.SafeUrl()}");
                    return null;
                }

                ITagList tagList;
                try
                {
                    tagList = tagListParser.Parse(name);
                }
                catch (FormatException ex)
                {
                    progress.Error(urlConfig, $"Cannot parse node name as tag list: {ex.Message}\non: {urlConfig.SafeUrl()}");
                    return null;
                }

                ProtoPatch protoPatch = protoPatchBuilder.Build(urlConfig, command, tagList);

                if (protoPatch == null)
                {
                    return null;
                }

                if (protoPatch.needs != null && !needsChecker.CheckNeedsExpression(protoPatch.needs))
                {
                    progress.NeedsUnsatisfiedRoot(urlConfig);
                    return null;
                }
                else if (!protoPatch.passSpecifier.CheckNeeds(needsChecker, progress))
                {
                    return null;
                }
                
                if (command == Command.Insert)
                {
                    ConfigNode newNode = urlConfig.config.DeepCopy();
                    newNode.name = protoPatch.nodeType;
                    newNode.id = urlConfig.config.id;
                    needsChecker.CheckNeedsRecursive(newNode, urlConfig);
                    urlConfig.parent.configs.Insert(index, new UrlDir.UrlConfig(urlConfig.parent, newNode));
                    return null;
                }
                else
                {
                    needsChecker.CheckNeedsRecursive(urlConfig.config, urlConfig);
                    return patchCompiler.CompilePatch(protoPatch);
                }
            }
            catch(Exception e)
            {
                progress.Exception(urlConfig, $"Exception while attempting to create patch from config: {urlConfig.SafeUrl()}", e);
                return null;
            }
        }
    }
}
