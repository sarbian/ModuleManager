using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using NSubstitute;
using TestUtils;
using ModuleManager;
using ModuleManager.Logging;
using ModuleManager.Patches;
using ModuleManager.Patches.PassSpecifiers;
using ModuleManager.Progress;


namespace ModuleManagerTests.Patches
{
    public class InsertPatchTest
    {
        [Fact]
        public void TestConstructor__UrlConfigNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new InsertPatch(null, "A_NODE", Substitute.For<IPassSpecifier>());
            });

            Assert.Equal("urlConfig", ex.ParamName);
        }

        [Fact]
        public void TestConstructor__NodeTypeNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new InsertPatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), null, Substitute.For<IPassSpecifier>());
            });

            Assert.Equal("nodeType", ex.ParamName);
        }

        [Fact]
        public void TestConstructor__PassSpecifierNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new InsertPatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), "A_NODE", null);
            });

            Assert.Equal("passSpecifier", ex.ParamName);
        }

        [Fact]
        public void TestUrlConfig()
        {
            UrlDir.UrlConfig urlConfig = UrlBuilder.CreateConfig("abc/def", new ConfigNode());
            InsertPatch patch = new InsertPatch(urlConfig, "A_NODE", Substitute.For<IPassSpecifier>());

            Assert.Same(urlConfig, patch.UrlConfig);
        }

        [Fact]
        public void TestNodeType()
        {
            InsertPatch patch = new InsertPatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), "A_NODE", Substitute.For<IPassSpecifier>());

            Assert.Equal("A_NODE", patch.NodeType);
        }

        [Fact]
        public void TestPassSpecifier()
        {
            IPassSpecifier passSpecifier = Substitute.For<IPassSpecifier>();
            InsertPatch patch = new InsertPatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), "A_NODE", passSpecifier);

            Assert.Same(passSpecifier, patch.PassSpecifier);
        }

        [Fact]
        public void TestApply()
        {
            UrlDir.UrlConfig urlConfig = UrlBuilder.CreateConfig("abc/def", new TestConfigNode("A_NODE:NEEDS[someMod]:FOR[somePass]")
            {
                { "key1", "value1" },
                { "key2", "value2" },
                new TestConfigNode("NODE_1")
                {
                    { "key3", "value3" },
                },
                new TestConfigNode("NODE_2")
                {
                    { "key4", "value4" },
                },
            });

            InsertPatch patch = new InsertPatch(urlConfig, "A_NODE", Substitute.For<IPassSpecifier>());

            LinkedList<IProtoUrlConfig> databaseConfigs = new LinkedList<IProtoUrlConfig>();

            IProtoUrlConfig config1 = Substitute.For<IProtoUrlConfig>();
            IProtoUrlConfig config2 = Substitute.For<IProtoUrlConfig>();

            databaseConfigs.AddLast(config1);
            databaseConfigs.AddLast(config2);

            patch.Apply(databaseConfigs, Substitute.For<IPatchProgress>(), Substitute.For<IBasicLogger>());

            IProtoUrlConfig[] databaseConfigsArray = databaseConfigs.ToArray();
            Assert.Equal(3, databaseConfigsArray.Length);
            Assert.Same(config1, databaseConfigsArray[0]);
            Assert.Same(config2, databaseConfigsArray[1]);

            Assert.Same(urlConfig.parent, databaseConfigsArray[2].UrlFile);
            Assert.Equal("abc/def.cfg", databaseConfigsArray[2].FileUrl);
            Assert.Equal("A_NODE", databaseConfigsArray[2].NodeType);
            Assert.Equal("abc/def.cfg/A_NODE", databaseConfigsArray[2].FullUrl);

            Assert.NotSame(urlConfig.config, databaseConfigsArray[2].Node);
            Assert.Equal("A_NODE", databaseConfigsArray[2].Node.name);
            Assert.Equal("A_NODE:NEEDS[someMod]:FOR[somePass]", urlConfig.config.name); // make sure this hasn't been changed
            Assert.Equal(2, databaseConfigsArray[2].Node.values.Count);
            Assert.Equal("key1", databaseConfigsArray[2].Node.values[0].name);
            Assert.Equal("value1", databaseConfigsArray[2].Node.values[0].value);
            Assert.Equal("key2", databaseConfigsArray[2].Node.values[1].name);
            Assert.Equal("value2", databaseConfigsArray[2].Node.values[1].value);
            Assert.Equal(2, databaseConfigsArray[2].Node.nodes.Count);
            Assert.Equal("NODE_1", databaseConfigsArray[2].Node.nodes[0].name);
            Assert.Equal(1, databaseConfigsArray[2].Node.nodes[0].values.Count);
            Assert.Equal("key3", databaseConfigsArray[2].Node.nodes[0].values[0].name);
            Assert.Equal("value3", databaseConfigsArray[2].Node.nodes[0].values[0].value);
            Assert.Equal(0, databaseConfigsArray[2].Node.nodes[0].nodes.Count);
            Assert.Equal("NODE_2", databaseConfigsArray[2].Node.nodes[1].name);
            Assert.Equal(1, databaseConfigsArray[2].Node.nodes[1].values.Count);
            Assert.Equal("key4", databaseConfigsArray[2].Node.nodes[1].values[0].name);
            Assert.Equal("value4", databaseConfigsArray[2].Node.nodes[1].values[0].value);
            Assert.Equal(0, databaseConfigsArray[2].Node.nodes[1].nodes.Count);
        }

        [Fact]
        public void TestApply__DatabaseConfigsNull()
        {
            InsertPatch patch = new InsertPatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), "A_NODE", Substitute.For<IPassSpecifier>());
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                patch.Apply(null, Substitute.For<IPatchProgress>(), Substitute.For<IBasicLogger>());
            });

            Assert.Equal("configs", ex.ParamName);
        }

        [Fact]
        public void TestApply__ProgressNull()
        {
            InsertPatch patch = new InsertPatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), "A_NODE", Substitute.For<IPassSpecifier>());
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                patch.Apply(new LinkedList<IProtoUrlConfig>(), null, Substitute.For<IBasicLogger>());
            });

            Assert.Equal("progress", ex.ParamName);
        }

        [Fact]
        public void TestApply__LoggerNull()
        {
            InsertPatch patch = new InsertPatch(UrlBuilder.CreateConfig("abc/def", new ConfigNode()), "A_NODE", Substitute.For<IPassSpecifier>());
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                patch.Apply(new LinkedList<IProtoUrlConfig>(), Substitute.For<IPatchProgress>(), null);
            });

            Assert.Equal("logger", ex.ParamName);
        }
    }
}
