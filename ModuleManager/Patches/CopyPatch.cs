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
                CopyFrom(ref listNode, databaseConfigs, context, progress);
            }
        }

        // Same as Apply, visiting only the nodes the index lists under the matcher's type, or
        // with one of its names when they have no wildcard. The clones go right after their
        // source, which Apply then skips too, so the nodes to visit are the ones listed before the
        // patch. Root lookups in the patch (#$@PART[foo]/mass$) scan the list, which has the
        // clones made so far. If a special command writes a name value in place, a node not
        // reached yet may now match: from there on it visits every node of the type.
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
            PatchContext context = new PatchContext(UrlConfig, databaseConfigs, logger, progress);
            List<LinkedListNode<IProtoUrlConfig>> nodes = new List<LinkedListNode<IProtoUrlConfig>>(index.NodesOfType(type));
            int count = databaseConfigs.Count;
            bool otherType = false;
            try
            {
                int start = 0;
                if (nodeMatcher.LiteralNames != null)
                {
                    int writes = PatchTypeIndex.NameWrites;
                    List<int> positions = new List<int>(index.PositionsNamed(type, nodeMatcher.LiteralNames));
                    start = nodes.Count;
                    for (int i = 0; i < positions.Count; i++)
                    {
                        int position = positions[i];
                        otherType |= Visit(nodes[position], type, databaseConfigs, context, progress);
                        if (PatchTypeIndex.NameWrites != writes)
                        {
                            start = position + 1;
                            break;
                        }
                    }
                }

                for (int i = start; i < nodes.Count; i++)
                    otherType |= Visit(nodes[i], type, databaseConfigs, context, progress);
            }
            finally
            {
                if (otherType) index.Invalidate();
                else if (databaseConfigs.Count != count) index.Invalidate(type);
            }
        }

        // Copies the node if it matches; tells whether the clone is of another type than the source
        private bool Visit(LinkedListNode<IProtoUrlConfig> listNode, string type, LinkedList<IProtoUrlConfig> databaseConfigs, PatchContext context, IPatchProgress progress)
        {
            LinkedListNode<IProtoUrlConfig> source = listNode;
            CopyFrom(ref listNode, databaseConfigs, context, progress);
            return listNode != source && listNode.Value.Node.name != type;
        }

        private void CopyFrom(ref LinkedListNode<IProtoUrlConfig> listNode, LinkedList<IProtoUrlConfig> databaseConfigs, PatchContext context, IPatchProgress progress)
        {
            IProtoUrlConfig protoConfig = listNode.Value;
            try
            {
                if (!NodeMatcher.IsMatch(protoConfig)) return;

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
