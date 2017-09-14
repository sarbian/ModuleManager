using System;
using System.Collections.Generic;
using System.Linq;
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
    }
}
