using System;
using ModuleManager.Extensions;

namespace ModuleManager
{
    public interface INodeMatcher
    {
        bool IsMatch(ConfigNode node);
    }

    public class NodeMatcher : INodeMatcher
    {
        private static readonly char[] sep = { '[', ']' };

        private string type;
        private string[] namePatterns = null;
        private string constraints = "";

        public NodeMatcher(string type, string name, string constraints)
        {
            if (type == string.Empty) throw new ArgumentException("can't be empty", nameof(type));
            this.type = type ?? throw new ArgumentNullException(nameof(type));

            if (name == string.Empty) throw new ArgumentException("can't be empty (null allowed)", nameof(name));
            if (constraints == string.Empty) throw new ArgumentException("can't be empty (null allowed)", nameof(constraints));

            if (name != null) namePatterns = name.Split(',', '|');
            if (constraints != null)
            {
                if (!constraints.IsBracketBalanced()) throw new ArgumentException("is not bracket balanced: " + constraints, nameof(constraints));
                this.constraints = constraints;
            }
        }

        public bool IsMatch(ConfigNode node)
        {
            if (node.name != type) return false;

            if (namePatterns != null)
            {
                string name = node.GetValue("name");
                if (name == null) return false;

                bool match = false;
                foreach (string pattern in namePatterns)
                {
                    if (MMPatchLoader.WildcardMatch(name, pattern))
                    {
                        match = true;
                        break;
                    }
                }

                if (!match) return false;
            }

            return MMPatchLoader.CheckConstraints(node, constraints);
        }
    }
}
