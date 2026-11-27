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
        private readonly string type;
        private readonly string[] namePatterns = null;
        private readonly string constraints = "";
        private MMPatchLoader.ParsedConstraints parsedConstraints;

        // Only nodes of this type can match: ConfigNode.name is the type (e.g. PART), not the name value.
        public string Type => type;

        // The name patterns if none has a wildcard, else null. A node can match only if its
        // name value is one of them, or one of them followed by a newline.
        public string[] LiteralNames { get; }

        public NodeMatcher(string type, string name, string constraints)
        {
            if (type == string.Empty) throw new ArgumentException("can't be empty", nameof(type));
            this.type = type ?? throw new ArgumentNullException(nameof(type));

            if (name == string.Empty) throw new ArgumentException("can't be empty (null allowed)", nameof(name));
            if (constraints == string.Empty) throw new ArgumentException("can't be empty (null allowed)", nameof(constraints));

            if (name != null)
            {
                namePatterns = name.Split(',', '|');
                if (Array.TrueForAll(namePatterns, pattern => pattern.IndexOfAny(new[] { '*', '?' }) < 0))
                    LiteralNames = namePatterns;
            }
            if (constraints != null)
            {
                if (!constraints.IsBracketBalanced()) throw new ArgumentException("is not bracket balanced: " + constraints, nameof(constraints));
                this.constraints = constraints;
            }
        }

        public bool IsMatch(ConfigNode node)
        {
            if (node.name != type) return false;
            return IsNameAndConstraintsMatch(node, namePatterns != null ? node.GetValue("name") : null);
        }

        // Same as IsMatch(protoConfig.Node), but takes the name value a ProtoUrlConfig already read.
        public bool IsMatch(IProtoUrlConfig protoConfig)
        {
            ConfigNode node = protoConfig.Node;
            if (node.name != type) return false;
            string name = null;
            if (namePatterns != null)
                name = protoConfig is ProtoUrlConfig known ? known.NameValue : node.GetValue("name");
            return IsNameAndConstraintsMatch(node, name);
        }

        private bool IsNameAndConstraintsMatch(ConfigNode node, string name)
        {
            if (namePatterns != null)
            {
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

            // Looked up on first use, so a constraint that does not parse still throws when checked
            if (parsedConstraints == null) parsedConstraints = MMPatchLoader.GetParsedConstraints(constraints);
            return MMPatchLoader.CheckConstraints(node, parsedConstraints);
        }
    }

    public static class NodeMatcherExtensions
    {
        // Uses NodeMatcher.IsMatch(IProtoUrlConfig) when available, so patches can match a config
        // without looking up its name again. Other matchers get the node.
        public static bool IsMatch(this INodeMatcher nodeMatcher, IProtoUrlConfig protoConfig)
        {
            if (nodeMatcher is NodeMatcher matcher) return matcher.IsMatch(protoConfig);
            return nodeMatcher.IsMatch(protoConfig.Node);
        }
    }
}
