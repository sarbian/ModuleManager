using System;
using System.Linq;
using System.Text;
using NodeStack = ModuleManager.Collections.ImmutableStack<ConfigNode>;

namespace ModuleManager.Extensions
{
    public static class NodeStackExtensions
    {
        public static string GetPath(this NodeStack stack)
        {
            int length = stack.Sum(node => node.name.Length) + stack.Depth - 1;
            StringBuilder sb = new StringBuilder(length);

            foreach (ConfigNode node in stack)
            {
                string nodeName = node.name;
                sb.Insert(0, node.name);
                if (sb.Length < sb.Capacity) sb.Insert(0, '/');
            }

            return sb.ToString();
        }
    }
}
