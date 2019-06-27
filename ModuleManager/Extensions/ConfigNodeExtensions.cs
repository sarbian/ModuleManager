using System;
using System.Text;

namespace ModuleManager.Extensions
{
    public static class ConfigNodeExtensions
    {
        public static void ShallowCopyFrom(this ConfigNode toNode, ConfigNode fromeNode)
        {
            toNode.ClearData();
            foreach (ConfigNode.Value value in fromeNode.values)
                toNode.values.Add(value);
            foreach (ConfigNode node in fromeNode.nodes)
                toNode.nodes.Add(node);
        }

        // KSP implementation of ConfigNode.CreateCopy breaks with badly formed nodes (nodes with a blank name)
        public static ConfigNode DeepCopy(this ConfigNode from)
        {
            ConfigNode to = new ConfigNode(from.name);
            foreach (ConfigNode.Value value in from.values)
                to.values.Add(new ConfigNode.Value(value.name, value.value));
            foreach (ConfigNode node in from.nodes)
            {
                ConfigNode newNode = DeepCopy(node);
                to.nodes.Add(newNode);
            }
            return to;
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
    }
}
