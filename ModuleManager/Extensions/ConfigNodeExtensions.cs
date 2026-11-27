using System;
using System.Text;

namespace ModuleManager.Extensions
{
    public static class ConfigNodeExtensions
    {
        public static void ShallowCopyFrom(this ConfigNode toNode, ConfigNode fromeNode)
        {
            toNode.ClearData();
            ConfigNode.ValueList values = fromeNode.values;
            for (int i = 0; i < values.Count; i++)
                toNode.values.Add(values[i]);
            ConfigNode.ConfigNodeList nodes = fromeNode.nodes;
            for (int i = 0; i < nodes.Count; i++)
                toNode.nodes.Add(nodes[i]);
        }

        // Characters that the ConfigNode(string) constructor removes or splits the name on
        private static readonly char[] nameCleanupChars = { '\n', '\r', '\t', '(', ')', ' ' };

        // KSP implementation of ConfigNode.CreateCopy breaks with badly formed nodes (nodes with a blank name)
        public static ConfigNode DeepCopy(this ConfigNode from)
        {
            // ConfigNode(name) strips newlines and tabs from the name and splits an id off it at
            // '(', ')' or ' '. A name with none of those comes out unchanged, so skip that work.
            string name = from.name;
            ConfigNode to;
            if (name != null && name.IndexOfAny(nameCleanupChars) < 0)
                to = new ConfigNode { name = name };
            else
                to = new ConfigNode(name);

            // Indexed loops: the lists' enumerators are non-generic and allocate.
            ConfigNode.ValueList values = from.values;
            ConfigNode.ValueList toValues = to.values;
            for (int i = 0; i < values.Count; i++)
            {
                ConfigNode.Value value = values[i];
                toValues.Add(new ConfigNode.Value(value.name, value.value));
            }
            ConfigNode.ConfigNodeList nodes = from.nodes;
            ConfigNode.ConfigNodeList toNodes = to.nodes;
            for (int i = 0; i < nodes.Count; i++)
                toNodes.Add(DeepCopy(nodes[i]));
            return to;
        }

        // DeepCopy of the node itself (its name and values), with the same subnodes, not copies
        public static ConfigNode CopySharingSubnodes(this ConfigNode from)
        {
            string name = from.name;
            ConfigNode to;
            if (name != null && name.IndexOfAny(nameCleanupChars) < 0)
                to = new ConfigNode { name = name };
            else
                to = new ConfigNode(name);

            ConfigNode.ValueList values = from.values;
            ConfigNode.ValueList toValues = to.values;
            for (int i = 0; i < values.Count; i++)
            {
                ConfigNode.Value value = values[i];
                toValues.Add(new ConfigNode.Value(value.name, value.value));
            }
            ConfigNode.ConfigNodeList nodes = from.nodes;
            ConfigNode.ConfigNodeList toNodes = to.nodes;
            for (int i = 0; i < nodes.Count; i++)
                toNodes.Add(nodes[i]);
            return to;
        }

        // Puts newChild in place of oldChild, at the same position
        public static void ReplaceNode(this ConfigNode parent, ConfigNode oldChild, ConfigNode newChild)
        {
            ConfigNode.ConfigNodeList nodes = parent.nodes;
            ConfigNode[] all = new ConfigNode[nodes.Count];
            for (int i = 0; i < all.Length; i++)
                all[i] = nodes[i];
            nodes.Clear();
            for (int i = 0; i < all.Length; i++)
                nodes.Add(ReferenceEquals(all[i], oldChild) ? newChild : all[i]);
        }

        public static void PrettyPrint(this ConfigNode node, ref StringBuilder sb, string indent)
        {
            if (sb == null) throw new ArgumentNullException(nameof(sb));
            if (indent == null) indent = string.Empty;
            if (node == null)
            {
                sb.Append(indent + "<null node>\n");
                return;
            }
            sb.AppendFormat("{0}{1}\n{2}{{\n", indent, node.name ?? "<null>", indent);
            string newindent = indent + "  ";
            if (node.values == null)
            {
                sb.AppendFormat("{0}<null value list>\n", newindent);
            }
            else
            {
                foreach (ConfigNode.Value value in node.values)
                {
                    if (value == null)
                        sb.AppendFormat("{0}<null value>\n", newindent);
                    else
                        sb.AppendFormat("{0}{1} = {2}\n", newindent, value.name ?? "<null>", value.value ?? "<null>");
                }
            }

            if (node.nodes == null)
            {
                sb.AppendFormat("{0}<null node list>\n", newindent);
            }
            else
            {
                foreach (ConfigNode subnode in node.nodes)
                {
                    subnode.PrettyPrint(ref sb, newindent);
                }
            }

            sb.AppendFormat("{0}}}\n", indent);
        }

        public static void AddValueSafe(this ConfigNode node, string name, string value)
        {
            node.values.Add(new ConfigNode.Value(name, value));
        }

        public static void EscapeValuesRecursive(this ConfigNode theNode)
        {
            foreach (ConfigNode subNode in theNode.nodes)
            {
                subNode.EscapeValuesRecursive();
            }

            foreach (ConfigNode.Value value in theNode.values)
            {
                value.value = value.value.Replace("\n", "\\n");
                value.value = value.value.Replace("\r", "\\r");
                value.value = value.value.Replace("\t", "\\t");
            }
        }

        // Only values with a backslash can hold an escape: the others are left as they are
        public static void UnescapeValuesRecursive(this ConfigNode theNode)
        {
            ConfigNode.ConfigNodeList nodes = theNode.nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                nodes[i].UnescapeValuesRecursive();
            }

            ConfigNode.ValueList values = theNode.values;
            for (int i = 0; i < values.Count; i++)
            {
                ConfigNode.Value value = values[i];
                if (value.value.IndexOf('\\') < 0)
                    continue;
                value.value = value.value.Replace("\\n", "\n");
                value.value = value.value.Replace("\\r", "\r");
                value.value = value.value.Replace("\\t", "\t");
            }
        }
    }
}
