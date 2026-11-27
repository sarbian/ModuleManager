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
        public bool CountsAsPatch => true;

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
                    if (NodeMatcher.IsMatch(protoConfig))
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

        // Same as Apply, visiting only the nodes the index lists under the matcher's type, or
        // with one of its names when they have no wildcard. Matching changes nothing, so the
        // candidates stay the same while the patch runs; the index then drops the deleted nodes.
        public void Apply(LinkedList<IProtoUrlConfig> databaseConfigs, PatchTypeIndex index, IPatchProgress progress, IBasicLogger logger)
        {
            if (index == null) throw new ArgumentNullException(nameof(index));
            if (!(NodeMatcher is NodeMatcher nodeMatcher))
            {
                try
                {
                    Apply(databaseConfigs, progress, logger);
                }
                finally
                {
                    index.Invalidate();
                }
                return;
            }
            if (databaseConfigs == null) throw new ArgumentNullException(nameof(databaseConfigs));
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            string type = nodeMatcher.Type;
            List<LinkedListNode<IProtoUrlConfig>> nodes = index.NodesOfType(type);
            List<int> positions = nodeMatcher.LiteralNames != null ? index.PositionsNamed(type, nodeMatcher.LiteralNames) : null;
            int count = positions?.Count ?? nodes.Count;
            bool removed = false;
            try
            {
                for (int i = 0; i < count; i++)
                {
                    LinkedListNode<IProtoUrlConfig> listNode = nodes[positions == null ? i : positions[i]];
                    IProtoUrlConfig protoConfig = listNode.Value;
                    try
                    {
                        if (NodeMatcher.IsMatch(protoConfig))
                        {
                            progress.ApplyingDelete(protoConfig, UrlConfig);
                            databaseConfigs.Remove(listNode);
                            removed = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        progress.Exception(UrlConfig, $"Exception while applying delete {UrlConfig.SafeUrl()} to {protoConfig.FullUrl}", ex);
                    }
                }
            }
            finally
            {
                if (removed) index.NodesRemoved(type);
            }
        }
    }
}
