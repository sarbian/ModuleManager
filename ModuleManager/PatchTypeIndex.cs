using System;
using System.Collections.Generic;

namespace ModuleManager
{
    // The nodes of a config list grouped by node type, each group in list order. Built on first
    // use; whoever adds or removes nodes from the list must call Invalidate, with the type of the
    // nodes if they are all of one type, so that only that group is rebuilt.
    public class PatchTypeIndex
    {
        private static readonly List<LinkedListNode<IProtoUrlConfig>> none = new List<LinkedListNode<IProtoUrlConfig>>();

        private readonly LinkedList<IProtoUrlConfig> configs;
        private Dictionary<string, List<LinkedListNode<IProtoUrlConfig>>> byType;
        private readonly HashSet<string> staleTypes = new HashSet<string>();

        public PatchTypeIndex(LinkedList<IProtoUrlConfig> configs)
        {
            this.configs = configs ?? throw new ArgumentNullException(nameof(configs));
        }

        public List<LinkedListNode<IProtoUrlConfig>> NodesOfType(string type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (byType == null) Build();
            else if (staleTypes.Remove(type)) Rebuild(type);
            return byType.TryGetValue(type, out List<LinkedListNode<IProtoUrlConfig>> nodes) ? nodes : none;
        }

        public void Invalidate()
        {
            byType = null;
            staleTypes.Clear();
        }

        public void Invalidate(string type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (byType != null) staleTypes.Add(type);
        }

        private void Rebuild(string type)
        {
            List<LinkedListNode<IProtoUrlConfig>> nodes = new List<LinkedListNode<IProtoUrlConfig>>();
            for (LinkedListNode<IProtoUrlConfig> listNode = configs.First; listNode != null; listNode = listNode.Next)
            {
                if (listNode.Value.Node.name == type) nodes.Add(listNode);
            }
            if (nodes.Count > 0) byType[type] = nodes;
            else byType.Remove(type);
        }

        private void Build()
        {
            byType = new Dictionary<string, List<LinkedListNode<IProtoUrlConfig>>>();
            for (LinkedListNode<IProtoUrlConfig> listNode = configs.First; listNode != null; listNode = listNode.Next)
            {
                string type = listNode.Value.Node.name;
                if (type == null) continue;
                if (!byType.TryGetValue(type, out List<LinkedListNode<IProtoUrlConfig>> nodes))
                {
                    nodes = new List<LinkedListNode<IProtoUrlConfig>>();
                    byType.Add(type, nodes);
                }
                nodes.Add(listNode);
            }
        }
    }
}
