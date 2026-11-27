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

        // Whether edits may share the subnodes they leave alone with the node they replace (see
        // MMPatchLoader.ModifyNodeSharingSubnodes): the patch has no special command and no paste
        private readonly bool canShareSubnodes;

        // For tests: false makes every edit copy whole nodes
        public static bool ShareSubnodes = true;

        public UrlDir.UrlConfig UrlConfig { get; }
        public INodeMatcher NodeMatcher { get; }
        public IPassSpecifier PassSpecifier { get; }
        public bool CountsAsPatch => true;

        public EditPatch(UrlDir.UrlConfig urlConfig, INodeMatcher nodeMatcher, IPassSpecifier passSpecifier)
        {
            UrlConfig = urlConfig ?? throw new ArgumentNullException(nameof(urlConfig));
            NodeMatcher = nodeMatcher ?? throw new ArgumentNullException(nameof(nodeMatcher));
            PassSpecifier = passSpecifier ?? throw new ArgumentNullException(nameof(passSpecifier));

            loop = urlConfig.config.HasNode("MM_PATCH_LOOP");
            canShareSubnodes = !HasSpecialOrPaste(urlConfig.config);
        }

        private static bool HasSpecialOrPaste(ConfigNode node)
        {
            for (int i = 0; i < node.values.Count; i++)
                if (CommandParser.Parse(node.values[i].name, out _) == Command.Special)
                    return true;
            for (int i = 0; i < node.nodes.Count; i++)
            {
                ConfigNode subnode = node.nodes[i];
                if (CommandParser.Parse(subnode.name, out _) == Command.Paste || HasSpecialOrPaste(subnode))
                    return true;
            }
            return false;
        }

        public void Apply(LinkedList<IProtoUrlConfig> databaseConfigs, IPatchProgress progress, IBasicLogger logger)
        {
            if (databaseConfigs == null) throw new ArgumentNullException(nameof(databaseConfigs));
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            PatchContext context = new PatchContext(UrlConfig, databaseConfigs, logger, progress);
            for (LinkedListNode<IProtoUrlConfig> listNode = databaseConfigs.First; listNode != null; listNode = listNode.Next)
            {
                ApplyTo(listNode, context, progress, logger);
            }
        }

        // Same as Apply, visiting only the nodes the index lists under the matcher's type, which
        // are the only ones it can match. An edit replaces each node's value in place and keeps
        // its type, so the index stays valid; if a type does change, the index is invalidated,
        // and if a name changes, the type's name lookup is.
        //
        // When the matcher's names have no wildcard, only nodes with one of those names (or one
        // followed by a newline, which WildcardMatch also accepts) can match, and the index finds
        // them by name. If a special command writes a name value in place while the patch runs,
        // a node it has not reached yet may now match: from there on it visits every node again.
        public void Apply(LinkedList<IProtoUrlConfig> databaseConfigs, PatchTypeIndex index, IPatchProgress progress, IBasicLogger logger)
        {
            if (index == null) throw new ArgumentNullException(nameof(index));
            if (!(NodeMatcher is NodeMatcher nodeMatcher))
            {
                Apply(databaseConfigs, progress, logger);
                index.Invalidate();
                return;
            }
            if (databaseConfigs == null) throw new ArgumentNullException(nameof(databaseConfigs));
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            string type = nodeMatcher.Type;
            bool typeChanged = false;
            // Lookups of root nodes (#$@PART[foo]/mass$) go through the index, which stays valid:
            // ApplyTo updates it as soon as a node's name or type changes.
            PatchContext context = new PatchContext(UrlConfig, databaseConfigs, logger, progress, index);
            List<LinkedListNode<IProtoUrlConfig>> nodes = index.NodesOfType(type);

            int start = 0;
            string[] literalNames = nodeMatcher.LiteralNames;
            if (literalNames != null)
            {
                int writes = PatchTypeIndex.NameWrites;
                List<int> positions = index.PositionsNamed(type, literalNames);
                start = nodes.Count;
                for (int i = 0; i < positions.Count; i++)
                {
                    int position = positions[i];
                    ApplyTo(nodes[position], type, index, context, progress, logger, ref typeChanged);
                    if (PatchTypeIndex.NameWrites != writes)
                    {
                        start = position + 1;
                        break;
                    }
                }
            }

            for (int i = start; i < nodes.Count; i++)
                ApplyTo(nodes[i], type, index, context, progress, logger, ref typeChanged);

            if (typeChanged) index.Invalidate();
        }

        private void ApplyTo(LinkedListNode<IProtoUrlConfig> listNode, string type, PatchTypeIndex index, PatchContext context, IPatchProgress progress, IBasicLogger logger, ref bool typeChanged)
        {
            IProtoUrlConfig before = listNode.Value;
            string nameBefore = PatchTypeIndex.NameOf(before);
            ApplyTo(listNode, context, progress, logger, index, type);
            IProtoUrlConfig after = listNode.Value;
            if (after == before) return;
            if (after.Node.name != type)
            {
                // The rest of this patch still visits the nodes it listed before
                typeChanged = true;
                index.Invalidate();
            }
            else if (PatchTypeIndex.NameOf(after) != nameBefore)
            {
                index.NamesChanged(type);
            }
        }

        // With an index, a loop that renames the node updates the index after each pass, since
        // the next pass's root lookups (#$@PART[foo]/mass$) read it
        private void ApplyTo(LinkedListNode<IProtoUrlConfig> listNode, PatchContext context, IPatchProgress progress, IBasicLogger logger, PatchTypeIndex index = null, string type = null)
        {
            IProtoUrlConfig protoConfig = listNode.Value;
            string name = index != null && loop ? PatchTypeIndex.NameOf(protoConfig) : null;
            try
            {
                if (!NodeMatcher.IsMatch(protoConfig)) return;
                if (loop) logger.Info($"Looping on {UrlConfig.SafeUrl()} to {protoConfig.FullUrl}");

                do
                {
                    progress.ApplyingUpdate(protoConfig, UrlConfig);
                    // The node replaced is not used again, so the new one can share its subnodes
                    ConfigNode newNode = canShareSubnodes && ShareSubnodes && !MMPatchLoader.NeedWholeCopies
                        ? MMPatchLoader.ModifyNodeSharingSubnodes(new NodeStack(protoConfig.Node), UrlConfig.config, context)
                        : MMPatchLoader.ModifyNode(new NodeStack(protoConfig.Node), UrlConfig.config, context);
                    listNode.Value = protoConfig = new ProtoUrlConfig(protoConfig.UrlFile, newNode);
                    if (index != null && loop)
                    {
                        string newName = PatchTypeIndex.NameOf(protoConfig);
                        if (newName != name)
                        {
                            index.NamesChanged(type);
                            name = newName;
                        }
                    }
                } while (loop && NodeMatcher.IsMatch(protoConfig));

                if (loop) protoConfig.Node.RemoveNodes("MM_PATCH_LOOP");
            }
            catch (Exception ex)
            {
                progress.Exception(UrlConfig, $"Exception while applying update {UrlConfig.SafeUrl()} to {protoConfig.FullUrl}", ex);
            }
        }
    }
}
