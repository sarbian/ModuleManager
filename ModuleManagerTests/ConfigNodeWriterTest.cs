using System;
using System.IO;
using Xunit;
using TestUtils;
using ModuleManager;

namespace ModuleManagerTests
{
    public class ConfigNodeWriterTest : IDisposable
    {
        private readonly string dir = Path.Combine(Path.GetTempPath(), "ConfigNodeWriterTest-" + Guid.NewGuid().ToString("N"));

        public ConfigNodeWriterTest()
        {
            Directory.CreateDirectory(dir);
        }

        public void Dispose()
        {
            Directory.Delete(dir, true);
        }

        [Fact]
        public void TestSave__SameAsConfigNodeSave()
        {
            ConfigNode node = new TestConfigNode("ROOT")
            {
                { "patchedNodeCount", "12" },
                { "unicode", "café 中文 \U0001F680" },
                { "empty", "" },
                new TestConfigNode("UrlConfig")
                {
                    { "parentUrl", "abc/def.cfg" },
                    new TestConfigNode("PART")
                    {
                        { "name", "foo" },
                        { "escaped", "a\\nb\\tc" },
                        new TestConfigNode("MODULE")
                        {
                            { "name", "ModuleThing" },
                            new TestConfigNode("EMPTY"),
                        },
                    },
                },
                new TestConfigNode("UrlConfig"),
            };
            node.values[0].comment = "a comment";
            node.nodes[0].comment = "node comment";
            node.nodes[0].nodes[0].values[1].comment = "";

            string expectedPath = Path.Combine(dir, "expected.cfg");
            string actualPath = Path.Combine(dir, "actual.cfg");
            node.Save(expectedPath);
            File.WriteAllText(actualPath, "something longer that must be overwritten, something longer that must be overwritten");
            ConfigNodeWriter.Save(node, actualPath);

            Assert.Equal(File.ReadAllBytes(expectedPath), File.ReadAllBytes(actualPath));
        }

        [Fact]
        public void TestSaveCache__SameAsCopyEscapeAndSave()
        {
            // Nodes as patches can leave them: values with braces, = in names, newlines and tabs,
            // which adding them to a ConfigNode would have sanitized; names the ConfigNode(string)
            // constructor cleans up; comments
            ConfigNode part = new TestConfigNode("PART")
            {
                { "name", "foo" },
                { "braces", "x" },
                { "eq", "y" },
                { "escaped", "z" },
                { "plain", "café" },
                new TestConfigNode("MODULE")
                {
                    { "name", "ModuleThing" },
                    new TestConfigNode("EMPTY"),
                },
                new TestConfigNode("X"),
                new TestConfigNode("Y"),
                new TestConfigNode("Z"),
            };
            part.values[1].value = "a{b}c";
            part.values[2].name = "k=e{y}";
            part.values[2].value = "v=1";
            part.values[3].value = "line1\nline2\r\tend\\n";
            part.values[4].comment = "comment";
            part.comment = "node comment";
            part.nodes[1].name = "SPLIT (id)";
            part.nodes[2].name = "TAB\tBED";
            part.nodes[3].name = "NEW\nLINE";
            ConfigNode other = new TestConfigNode("RESOURCE_DEFINITION") { { "name", "LiquidFuel" } };

            ConfigNode[] roots = { part, other };
            string[] urls = { "abc/def.cfg", "ghi/jkl.cfg" };

            ConfigNode expected = new ConfigNode();
            expected.AddValue("patchedNodeCount", "2");
            ConfigNode actual = new ConfigNode();
            actual.AddValue("patchedNodeCount", "2");
            for (int i = 0; i < roots.Length; i++)
            {
                ConfigNode expectedUrl = expected.AddNode("UrlConfig");
                expectedUrl.AddValue("parentUrl", urls[i]);
                ConfigNode copy = ModuleManager.Extensions.ConfigNodeExtensions.DeepCopy(roots[i]);
                ModuleManager.Extensions.ConfigNodeExtensions.EscapeValuesRecursive(copy);
                expectedUrl.AddNode(copy);

                ConfigNode actualUrl = actual.AddNode("UrlConfig");
                actualUrl.AddValue("parentUrl", urls[i]);
                actualUrl.AddNode(roots[i]);
            }

            string expectedPath = Path.Combine(dir, "expected.cfg");
            string actualPath = Path.Combine(dir, "actual.cfg");
            ConfigNodeWriter.Save(expected, expectedPath);
            ConfigNodeWriter.SaveCache(actual, actualPath);

            Assert.Equal(File.ReadAllText(expectedPath), File.ReadAllText(actualPath));
            Assert.Equal(File.ReadAllBytes(expectedPath), File.ReadAllBytes(actualPath));
            Assert.Equal("a{b}c", part.values[1].value);
            Assert.Contains("SPLIT", File.ReadAllText(actualPath));
        }

        [Fact]
        public void TestSave__Empty()
        {
            string expectedPath = Path.Combine(dir, "expected.cfg");
            string actualPath = Path.Combine(dir, "actual.cfg");
            new ConfigNode().Save(expectedPath);
            ConfigNodeWriter.Save(new ConfigNode(), actualPath);

            Assert.Equal(File.ReadAllBytes(expectedPath), File.ReadAllBytes(actualPath));
        }
    }
}
