using System;
using System.Collections.Generic;
using NodeStack = ModuleManager.Collections.ImmutableStack<ConfigNode>;
using ModuleManager.Extensions;
using ModuleManager.Logging;
using ModuleManager.Patches.PassSpecifiers;
using ModuleManager.Progress;

namespace ModuleManager.Patches
{
    public class CopyPatch : IPatch
    {
        public UrlDir.UrlConfig UrlConfig { get; }
        public INodeMatcher NodeMatcher { get; }
        public IPassSpecifier PassSpecifier { get; }
        public bool CountsAsPatch => true;

        public CopyPatch(UrlDir.UrlConfig urlConfig, INodeMatcher nodeMatcher, IPassSpecifier passSpecifier)
        {
            UrlConfig = urlConfig ?? throw new ArgumentNullException(nameof(urlConfig));
            NodeMatcher = nodeMatcher ?? throw new ArgumentNullException(nameof(nodeMatcher));
            PassSpecifier = passSpecifier ?? throw new ArgumentNullException(nameof(passSpecifier));
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

                    ConfigNode clone = MMPatchLoader.ModifyNode(new NodeStack(protoConfig.Node), UrlConfig.config, context);
                    if (protoConfig.Node.GetValue("name") is string name && name == clone.GetValue("name"))
                    {
                        progress.Error(UrlConfig, $"Error - when applying copy {UrlConfig.SafeUrl()} to {protoConfig.FullUrl} - the copy needs to have a different name than the parent (use @name = xxx)");
                    }
                    else
                    {
                        progress.ApplyingCopy(protoConfig, UrlConfig);
                        listNode = databaseConfigs.AddAfter(listNode, new ProtoUrlConfig(protoConfig.UrlFile, clone));
                    }
                }
                catch (Exception ex)
                {
                    progress.Exception(UrlConfig, $"Exception while applying copy {UrlConfig.SafeUrl()} to {protoConfig.FullUrl}", ex);
                }
            }
        }
    }
}
