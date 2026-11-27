using System;
using System.IO;

namespace ModuleManager
{
    // Writes a ConfigNode to a file exactly as ConfigNode.Save(path) does, without the flush
    // ConfigNode.Save does at the end of every node, which costs more than the writing for the
    // cache's hundred thousand nodes.
    public static class ConfigNodeWriter
    {
        public static void Save(ConfigNode node, string path)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (path == null) throw new ArgumentNullException(nameof(path));

            using StreamWriter writer = Open(path);
            Save(node, writer);
        }

        // The encoding a StreamWriter uses by default, as ConfigNode.Save's does
        private static StreamWriter Open(string path) => new StreamWriter(File.Open(path, FileMode.Create), new System.Text.UTF8Encoding(false, true), 1 << 16);

        // Writes the cache node: its values and its UrlConfig nodes as they are, and the nodes in
        // each UrlConfig node as their DeepCopy would be written once escaped with
        // EscapeValuesRecursive, without making that copy (see WriteCopy).
        public static void SaveCache(ConfigNode cache, string path)
        {
            if (cache == null) throw new ArgumentNullException(nameof(cache));
            if (path == null) throw new ArgumentNullException(nameof(path));

            using StreamWriter writer = Open(path);
            SaveCache(cache, writer);
        }

        public static void SaveCache(ConfigNode cache, TextWriter writer)
        {
            if (cache == null) throw new ArgumentNullException(nameof(cache));
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            WriteValues(writer, cache, "");
            for (int i = 0; i < cache.nodes.Count; i++)
            {
                ConfigNode urlConfig = cache.nodes[i];
                WriteHeader(writer, urlConfig, urlConfig.name, "");
                WriteValues(writer, urlConfig, "\t");
                for (int j = 0; j < urlConfig.nodes.Count; j++)
                    WriteCopy(writer, urlConfig.nodes[j], "\t");
                writer.WriteLine("}");
            }
        }

        // Same text as ConfigNode.Save: UTF-8 without a byte order mark, and
        //   name // comment
        //   {
        //   <tab>key = value // comment
        //   ...
        //   }
        // with the comments only where there are some.
        public static void Save(ConfigNode node, TextWriter writer)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            WriteValues(writer, node, "");
            for (int i = 0; i < node.nodes.Count; i++)
                WriteNode(writer, node.nodes[i], "");
        }

        private static void WriteValues(TextWriter writer, ConfigNode node, string indent)
        {
            ConfigNode.ValueList values = node.values;
            for (int i = 0; i < values.Count; i++)
            {
                ConfigNode.Value value = values[i];
                writer.Write(indent);
                writer.Write(value.name);
                writer.Write(" = ");
                writer.Write(value.value);
                if (!string.IsNullOrEmpty(value.comment))
                {
                    writer.Write(" // ");
                    writer.Write(value.comment);
                }
                writer.WriteLine();
            }
        }

        private static void WriteNode(TextWriter writer, ConfigNode node, string indent)
        {
            WriteHeader(writer, node, node.name, indent);

            string inner = indent + "\t";
            WriteValues(writer, node, inner);
            for (int i = 0; i < node.nodes.Count; i++)
                WriteNode(writer, node.nodes[i], inner);

            writer.Write(indent);
            writer.WriteLine("}");
        }

        private static void WriteHeader(TextWriter writer, ConfigNode node, string name, string indent)
        {
            writer.Write(indent);
            writer.Write(name);
            if (!string.IsNullOrEmpty(node.comment))
            {
                writer.Write(" // ");
                writer.Write(node.comment);
            }
            writer.WriteLine();
            writer.Write(indent);
            writer.WriteLine("{");
        }

        // Characters that the ConfigNode(string) constructor removes or splits the name on
        private static readonly char[] nameCleanupChars = { '\n', '\r', '\t', '(', ')', ' ' };
        private static readonly char[] escapedChars = { '\n', '\r', '\t' };

        // Writes node as DeepCopy(node), then EscapeValuesRecursive, would give it:
        // - the name as the ConfigNode(string) constructor makes it (DeepCopy skips that when it
        //   has nothing to do), and no comment;
        // - each value as ConfigNode.ValueList.Add sanitizes it ({ and } become [ and ], and = in
        //   names becomes -), then with newlines, returns and tabs escaped, and no comment.
        private static void WriteCopy(TextWriter writer, ConfigNode node, string indent)
        {
            string name = node.name;
            if (name == null || name.IndexOfAny(nameCleanupChars) >= 0)
                name = new ConfigNode(name).name;
            writer.Write(indent);
            writer.WriteLine(name);
            writer.Write(indent);
            writer.WriteLine("{");

            string inner = indent + "\t";
            ConfigNode.ValueList values = node.values;
            for (int i = 0; i < values.Count; i++)
            {
                ConfigNode.Value value = values[i];
                string valueName = Sanitize(value.name, true);
                string text = Sanitize(value.value, false);
                if (text.IndexOfAny(escapedChars) >= 0)
                    text = text.Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
                writer.Write(inner);
                writer.Write(valueName);
                writer.Write(" = ");
                writer.Write(text);
                writer.WriteLine();
            }
            for (int i = 0; i < node.nodes.Count; i++)
                WriteCopy(writer, node.nodes[i], inner);

            writer.Write(indent);
            writer.WriteLine("}");
        }

        // ConfigNode.Value.Sanitize, for one string: throws on null as it does
        private static string Sanitize(string s, bool isName)
        {
            for (int i = s.Length - 1; i >= 0; i--)
            {
                char c = s[i];
                if (c == '{' || c == '}' || (isName && c == '='))
                {
                    s = s.Replace('{', '[').Replace('}', ']');
                    if (isName) s = s.Replace('=', '-');
                    break;
                }
            }
            return s;
        }
    }
}
