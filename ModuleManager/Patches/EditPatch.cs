using System;
using System.Collections.Generic;
using NodeStack = ModuleManager.Collections.ImmutableStack<ConfigNode>;
using ModuleManager.Extensions;
using ModuleManager.Logging;
using ModuleManager.Patches.PassSpecifiers;
using ModuleManager.Progress;

namespace ModuleManager.Patches
{
    public class EditPatch : IPatch
    {
        private readonly bool loop;

        public UrlDir.UrlConfig UrlConfig { get; }
        public INodeMatcher NodeMatcher { get; }
        public IPassSpecifier PassSpecifier { get; }

        public EditPatch(UrlDir.UrlConfig urlConfig, INodeMatcher nodeMatcher, IPassSpecifier passSpecifier)
        {
            UrlConfig = urlConfig ?? throw new ArgumentNullException(nameof(urlConfig));
            NodeMatcher = nodeMatcher ?? throw new ArgumentNullException(nameof(nodeMatcher));
            PassSpecifier = passSpecifier ?? throw new ArgumentNullException(nameof(passSpecifier));

            loop = urlConfig.config.HasNode("MM_PATCH_LOOP");
        }

        public void Apply(LinkedList<IProtoUrlConfig> databaseConfigs, IPatchProgress progress, IBasicLogger logger)
        {
            if (databaseConfigs == null) throw new ArgumentNullException(nameof(databaseConfigs));
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            PatchContext context = new PatchContext(UrlConfig, databaseConfigs, logger, progress);
            for (LinkedListNode<IProtoUrlConfig> listNode = databaseConfigs.First; listNode != null; listNode = listNode.Next)
            {
                IProtoUrlConfig protoConfig = listNode.Value;
                try
                {
                    if (!NodeMatcher.IsMatch(protoConfig.Node)) continue;
                    if (loop) logger.Info($"Looping on {UrlConfig.SafeUrl()} to {protoConfig.FullUrl}");

                    do
                    {
                        progress.ApplyingUpdate(protoConfig, UrlConfig);
                        listNode.Value = protoConfig = new ProtoUrlConfig(protoConfig.UrlFile, MMPatchLoader.ModifyNode(new NodeStack(protoConfig.Node), UrlConfig.config, context));
                    } while (loop && NodeMatcher.IsMatch(protoConfig.Node));

                    if (loop) protoConfig.Node.RemoveNodes("MM_PATCH_LOOP");
                }
                catch (Exception ex)
                {
                    progress.Exception(UrlConfig, $"Exception while applying update {UrlConfig.SafeUrl()} to {protoConfig.FullUrl}", ex);
                }
            }
        }
    }
}
