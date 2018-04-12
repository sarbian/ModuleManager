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

        public NodeMatcher(string nodeName)
        {
            if (nodeName == null) throw new ArgumentNullException(nameof(nodeName));
            if (nodeName == "") throw new ArgumentException("can't be empty", nameof(nodeName));
            if (!nodeName.IsBracketBalanced()) throw new FormatException("node name is not bracket balanced: " + nodeName);
            string name = nodeName;

            int indexOfHas = name.IndexOf(":HAS[", StringComparison.InvariantCultureIgnoreCase);
            
            if (indexOfHas == 0)
            {
                throw new FormatException("node name cannot begin with :HAS : " + nodeName);
            }
            else if (indexOfHas > 0)
            {
                int closingBracketIndex = name.LastIndexOf(']', name.Length - 1, name.Length - indexOfHas - 1);
                // Really shouldn't happen if we're bracket balanced but just in case
                if (closingBracketIndex == -1) throw new FormatException("Malformed :HAS[] block detected: " + nodeName);

                constraints = name.Substring(indexOfHas + 5, closingBracketIndex - indexOfHas - 5);
                name = name.Substring(0, indexOfHas);
            }

            int bracketIndex = name.IndexOf('[');
            if (bracketIndex == 0)
            {
                throw new FormatException("node name cannot begin with a bracket: " + nodeName);
            }
            else if (bracketIndex > 0)
            {
                int closingBracketIndex = name.LastIndexOf(']', name.Length - 1, name.Length - bracketIndex - 1);
                // Really shouldn't happen if we're bracket balanced but just in case
                if (closingBracketIndex == -1) throw new FormatException("Malformed brackets detected: " + nodeName);
                string patterns = name.Substring(bracketIndex + 1, closingBracketIndex - bracketIndex - 1);
                namePatterns = patterns.Split(',', '|');
                type = name.Substring(0, bracketIndex);
            }
            else
            {
                type = name;
                namePatterns = null;
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
