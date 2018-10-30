using System;
using System.Collections.Generic;
using ModuleManager.Extensions;
using ModuleManager.Logging;
using ModuleManager.Patches.PassSpecifiers;
using ModuleManager.Progress;

namespace ModuleManager.Patches
{
    public class DeletePatch : IPatch
    {
        public UrlDir.UrlConfig UrlConfig { get; }
        public INodeMatcher NodeMatcher { get; }
        public IPassSpecifier PassSpecifier { get; }

        public DeletePatch(UrlDir.UrlConfig urlConfig, INodeMatcher nodeMatcher, IPassSpecifier passSpecifier)
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

            LinkedListNode<IProtoUrlConfig> currentNode = databaseConfigs.First;
            while (currentNode != null)
            {
                IProtoUrlConfig protoConfig = currentNode.Value;
                try
                {
                    LinkedListNode<IProtoUrlConfig> nextNode = currentNode.Next;
                    if (NodeMatcher.IsMatch(protoConfig.Node))
                    {
                        progress.ApplyingDelete(protoConfig, UrlConfig);
                        databaseConfigs.Remove(currentNode);
                    }
                    currentNode = nextNode;
                }
                catch (Exception ex)
                {
                    progress.Exception(UrlConfig, $"Exception while applying delete {UrlConfig.SafeUrl()} to {protoConfig.FullUrl}", ex);
                }
            }
        }
    }
}
