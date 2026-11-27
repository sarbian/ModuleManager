using System;
using System.Collections.Generic;
using System.Threading;

namespace ModuleManager
{
    // The nodes of a config list grouped by node type, each group in list order. Built on first
    // use; whoever adds or removes nodes from the list must call Invalidate, with the type of the
    // nodes if they are all of one type, so that only that group is rebuilt.
    //
    // Each group can also be looked up by the nodes' name value, giving the positions in the
    // group of the nodes with that name. Whoever changes the name of a node in the list must call
    // NamesChanged with its type. A special command can write a name value in place, in any node:
    // it calls NameWritten, after which every name lookup is rebuilt.
    public class PatchTypeIndex
    {
        private static readonly List<LinkedListNode<IProtoUrlConfig>> none = new List<LinkedListNode<IProtoUrlConfig>>();
        private static readonly List<int> noPositions = new List<int>();

        private static int nameWrites;

        // Counts the name values written in place, by any patching run
        public static int NameWrites => Volatile.Read(ref nameWrites);
        public static void NameWritten() => Interlocked.Increment(ref nameWrites);

        private readonly LinkedList<IProtoUrlConfig> configs;
        private Dictionary<string, List<LinkedListNode<IProtoUrlConfig>>> byType;
        private readonly HashSet<string> staleTypes = new HashSet<string>();

        private readonly Dictionary<string, Dictionary<string, List<int>>> byName = new Dictionary<string, Dictionary<string, List<int>>>();
        private int byNameWrites;

        public PatchTypeIndex(LinkedList<IProtoUrlConfig> configs)
        {
            this.configs = configs ?? throw new ArgumentNullException(nameof(configs));
        }

        // The name value NodeMatcher matches a config's name patterns against
        public static string NameOf(IProtoUrlConfig config) => config is ProtoUrlConfig known ? known.NameValue : config.Node.GetValue("name");

        public List<LinkedListNode<IProtoUrlConfig>> NodesOfType(string type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (byType == null) Build();
            else if (staleTypes.Remove(type)) Rebuild(type);
            return byType.TryGetValue(type, out List<LinkedListNode<IProtoUrlConfig>> nodes) ? nodes : none;
        }

        // The positions in NodesOfType(type), in order, of the nodes whose name value is name
        public List<int> PositionsNamed(string type, string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            List<LinkedListNode<IProtoUrlConfig>> nodes = NodesOfType(type);
            int writes = NameWrites;
            if (byNameWrites != writes)
            {
                byName.Clear();
                byNameWrites = writes;
            }
            if (!byName.TryGetValue(type, out Dictionary<string, List<int>> names))
            {
                names = new Dictionary<string, List<int>>();
                for (int i = 0; i < nodes.Count; i++)
                {
                    string nodeName = NameOf(nodes[i].Value);
                    if (nodeName == null) continue;
                    if (!names.TryGetValue(nodeName, out List<int> positions))
                    {
                        positions = new List<int>(1);
                        names.Add(nodeName, positions);
                    }
                    positions.Add(i);
                }
                byName.Add(type, names);
            }
            return names.TryGetValue(name, out List<int> found) ? found : noPositions;
        }

        // The positions of the nodes named one of names (or one followed by a newline, which
        // WildcardMatch also accepts), in order. The list may be the index's own: don't change it.
        public List<int> PositionsNamed(string type, string[] names)
        {
            List<int> result = null;
            bool merged = false;
            foreach (string name in names)
            {
                foreach (List<int> positions in new[] { PositionsNamed(type, name), PositionsNamed(type, name + "\n") })
                {
                    if (positions.Count == 0) continue;
                    if (result == null)
                    {
                        result = positions;
                        continue;
                    }
                    if (!merged)
                    {
                        result = new List<int>(result);
                        merged = true;
                    }
                    result.AddRange(positions);
                }
            }
            if (result == null) return new List<int>();
            if (merged)
            {
                result.Sort();
                int count = 0;
                for (int i = 0; i < result.Count; i++)
                    if (count == 0 || result[count - 1] != result[i]) result[count++] = result[i];
                result.RemoveRange(count, result.Count - count);
            }
            return result;
        }

        // Whoever removes nodes of one type from the list can call this instead of Invalidate:
        // the type's group drops the removed nodes and keeps its order
        public void NodesRemoved(string type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (byType != null && !staleTypes.Contains(type) && byType.TryGetValue(type, out List<LinkedListNode<IProtoUrlConfig>> nodes))
            {
                nodes.RemoveAll(node => node.List == null);
                if (nodes.Count == 0) byType.Remove(type);
            }
            byName.Remove(type);
        }

        public void NamesChanged(string type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            byName.Remove(type);
        }

        public void Invalidate()
        {
            byType = null;
            staleTypes.Clear();
            byName.Clear();
        }

        public void Invalidate(string type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (byType != null) staleTypes.Add(type);
            byName.Remove(type);
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
            byName.Remove(type);
        }

        private void Build()
        {
            byType = new Dictionary<string, List<LinkedListNode<IProtoUrlConfig>>>();
            byName.Clear();
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
