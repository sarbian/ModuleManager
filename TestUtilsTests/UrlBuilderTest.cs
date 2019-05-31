using System;
using Xunit;
using TestUtils;

namespace TestUtilsTests
{
    public class UrlBuilderTest
    {
        [Fact]
        public void TestCreateRoot()
        {
            UrlDir root = UrlBuilder.CreateRoot();

            Assert.Equal("root", root.name);
            Assert.Null(root.parent);
            Assert.Same(root, root.root);
        }

        [Fact]
        public void TestCreateDir()
        {
            UrlDir dir = UrlBuilder.CreateDir("abc");

            Assert.Equal("abc", dir.name);

            UrlDir root = dir.parent;
            Assert.NotNull(root);
            Assert.Equal("root", root.name);
            Assert.Null(root.parent);
            Assert.Contains(dir, root.children);

            Assert.Same(root, dir.root);
            Assert.Same(root, root.root);
        }

        [Fact]
        public void TestCreateDir__Parent()
        {
            UrlDir root = UrlBuilder.CreateRoot();

            UrlDir child1 = UrlBuilder.CreateDir("child1", root);

            Assert.Equal("child1", child1.name);
            Assert.Same(root, child1.parent);
            Assert.Same(root, child1.root);

            Assert.Equal("child1", child1.url);

            Assert.Contains(child1, root.children);

            UrlDir child2 = UrlBuilder.CreateDir("child2", child1);

            Assert.Equal("child2", child2.name);
            Assert.Same(child1, child2.parent);
            Assert.Same(root, child2.root);

            Assert.Equal("child1/child2", child2.url);

            Assert.Contains(child2, child1.children);
        }

        [Fact]
        public void TestCreateDir__Url()
        {
            UrlDir dir = UrlBuilder.CreateDir("abc/def");

            Assert.Equal("def", dir.name);

            UrlDir parent = dir.parent;

            Assert.NotNull(parent);
            Assert.Equal("abc", parent.name);
            Assert.Contains(dir, parent.children);

            UrlDir root = parent.parent;

            Assert.NotNull(root);
            Assert.Equal("root", root.name);
            Assert.Contains(parent, root.children);
            Assert.Null(root.parent);

            Assert.Same(root, root.root);
            Assert.Same(root, parent.root);
            Assert.Same(root, dir.root);

            Assert.Contains(dir, root.AllDirectories);
        }

        [Fact]
        public void TestCreateDir__Url__Parent()
        {
            UrlDir root = UrlBuilder.CreateRoot();
            UrlDir parent1 = UrlBuilder.CreateDir("abc", root);

            UrlDir dir = UrlBuilder.CreateDir("def/ghi", parent1);

            Assert.Equal("ghi", dir.name);

            UrlDir parent2 = dir.parent;

            Assert.NotNull(parent2);
            Assert.Equal("def", parent2.name);
            Assert.Contains(dir, parent2.children);

            Assert.Same(parent1, parent2.parent);
            Assert.Contains(parent2, parent1.children);

            Assert.Same(root, dir.root);
            Assert.Same(root, parent2.root);

            Assert.Contains(dir, root.AllDirectories);
            Assert.Contains(dir, parent1.AllDirectories);
        }

        [Fact]
        public void TestCreateDir__Url__AlreadyExists()
        {
            UrlDir root = UrlBuilder.CreateRoot();

            UrlDir dir1 = UrlBuilder.CreateDir("abc/def", root);
            UrlDir dir2 = UrlBuilder.CreateDir("abc/def", root);

            Assert.Same(dir1, dir2);

            Assert.Equal("def", dir1.name);

            UrlDir parent = dir1.parent;

            Assert.NotNull(parent);
            Assert.Equal("abc", parent.name);
            Assert.Contains(dir1, parent.children);

            Assert.Same(root, dir1.root);
            Assert.Same(root, parent.root);
            Assert.Contains(dir1, root.AllDirectories);
        }

        [Fact]
        public void TestCreateGameData()
        {
            UrlDir gameData = UrlBuilder.CreateGameData();

            Assert.Equal("", gameData.name);
            Assert.Equal("", gameData.url);
            Assert.Equal(UrlDir.DirectoryType.GameData, gameData.type);
            UrlDir root = Assert.IsType<UrlDir>(gameData.root);
            Assert.Same(root, gameData.parent);
            Assert.Equal("root", root.name);
            Assert.Null(root.parent);
            Assert.Same(root, root.root);
            Assert.Contains(gameData, root.children);
        }

        [Fact]
        public void TestCreateGameData__SpecifyRoot()
        {
            UrlDir root = UrlBuilder.CreateRoot();
            UrlDir gameData = UrlBuilder.CreateGameData(root);

            Assert.Equal("", gameData.name);
            Assert.Equal("", gameData.url);
            Assert.Equal(UrlDir.DirectoryType.GameData, gameData.type);
            Assert.Same(root, gameData.parent);
            Assert.Contains(gameData, root.children);
        }

        [Fact]
        public void TestCreateGameData__SpecifyRoot__GameDataAlreadyExists()
        {
            UrlDir root = UrlBuilder.CreateRoot();
            UrlDir gameData1 = UrlBuilder.CreateGameData(root);
            UrlDir gameData2 = UrlBuilder.CreateGameData(root);

            Assert.Same(gameData1, gameData2);
        }

        [Fact]
        public void TestCreateFile()
        {
            UrlDir.UrlFile file = UrlBuilder.CreateFile("someFile.txt");

            Assert.Equal("someFile", file.name);
            Assert.Equal("txt", file.fileExtension);
            Assert.Equal(UrlDir.FileType.Unknown, file.fileType);

            UrlDir root = file.parent;
            Assert.NotNull(root);
            Assert.Equal("root", root.name);
            Assert.Null(root.parent);
            Assert.Contains(file, root.files);
            Assert.Same(root, file.root);
        }

        [Fact]
        public void TestCreateFile__Parent()
        {
            UrlDir root = UrlBuilder.CreateRoot();
            UrlDir dir = UrlBuilder.CreateDir("someDir", root);
            UrlDir.UrlFile file = UrlBuilder.CreateFile("someFile.txt", dir);

            Assert.Equal("someFile", file.name);
            Assert.Equal("txt", file.fileExtension);
            Assert.Equal(UrlDir.FileType.Unknown, file.fileType);
            Assert.Same(dir, file.parent);
            Assert.Same(root, file.root);

            Assert.Equal("someDir/someFile", file.url);
            Assert.Contains(file, dir.files);
            Assert.Contains(file, root.AllFiles);
        }

        [InlineData("cfg", UrlDir.FileType.Config)]
        [Theory]
        public void TestCreateFile__Extension(string extension, UrlDir.FileType fileType)
        {
            UrlDir.UrlFile file = UrlBuilder.CreateFile("someFile." + extension);

            Assert.Equal("someFile", file.name);
            Assert.Equal("cfg", file.fileExtension);
            Assert.Equal(fileType, file.fileType);

            UrlDir root = file.parent;
            Assert.NotNull(root);
            Assert.Equal("root", root.name);
            Assert.Null(root.parent);
            Assert.Contains(file, root.files);
            Assert.Same(root, file.root);
        }

        [Fact]
        public void TestCreateFile__Url()
        {
            UrlDir.UrlFile file = UrlBuilder.CreateFile("abc/def/ghi.txt");

            Assert.Equal("ghi", file.name);
            Assert.Equal("txt", file.fileExtension);
            Assert.Equal(UrlDir.FileType.Unknown, file.fileType);
            Assert.Equal("abc/def/ghi", file.url);

            UrlDir parent1 = file.parent;
            Assert.NotNull(parent1);
            Assert.Equal("def", parent1.name);
            Assert.Contains(file, parent1.files);

            UrlDir parent2 = parent1.parent;
            Assert.NotNull(parent2);
            Assert.Equal("abc", parent2.name);
            Assert.Contains(parent1, parent2.children);

            UrlDir root = parent2.parent;
            Assert.NotNull(root);
            Assert.Equal("root", root.name);
            Assert.Contains(parent2, root.children);
            Assert.Null(root.parent);

            Assert.Same(root, file.root);
            Assert.Same(root, parent1.root);
            Assert.Same(root, parent2.root);
            Assert.Same(root, root.root);

            Assert.Contains(file, root.AllFiles);
            Assert.Contains(file, parent2.AllFiles);
        }

        [Fact]
        public void TestCreateFile__Url__AlreadyExists()
        {
            UrlDir root = UrlBuilder.CreateRoot();
            UrlDir.UrlFile file1 = UrlBuilder.CreateFile("abc/def.txt", root);
            UrlDir.UrlFile file2 = UrlBuilder.CreateFile("abc/def.txt", root);

            Assert.Same(file1, file2);

            Assert.Equal("def", file1.name);
            Assert.Equal("txt", file1.fileExtension);
            Assert.Equal(UrlDir.FileType.Unknown, file1.fileType);
            Assert.Equal("abc/def", file1.url);

            UrlDir parent1 = file1.parent;
            Assert.NotNull(parent1);
            Assert.Equal("abc", parent1.name);
            Assert.Contains(file1, parent1.files);

            Assert.Same(root, file1.root);
            Assert.Same(root, parent1.root);

            Assert.Contains(file1, root.AllFiles);
        }

        [Fact]
        public void TestCreateConfig()
        {
            ConfigNode node = new TestConfigNode("SOME_NODE")
            {
                { "name", "blah" },
                { "foo", "bar" },
            };
            UrlDir.UrlFile file = UrlBuilder.CreateFile("abc/def.cfg");
            UrlDir.UrlConfig config = UrlBuilder.CreateConfig(node, file);

            Assert.Equal("SOME_NODE", config.type);
            Assert.Equal("blah", config.name);
            Assert.Same(node, config.config);
            Assert.Equal("abc/def/blah", config.url); // I don't know why this is correct, but it is
            Assert.Same(file, config.parent);
            Assert.Contains(config, file.configs);
        }

        [Fact]
        public void TestCreateConfig__Url()
        {
            ConfigNode node = new TestConfigNode("SOME_NODE")
            {
                { "name", "blah" },
                { "foo", "bar" },
            };
            UrlDir.UrlConfig config = UrlBuilder.CreateConfig("abc/def", node);

            Assert.Equal("SOME_NODE", config.type);
            Assert.Equal("blah", config.name);
            Assert.Same(node, config.config);
            Assert.Equal("abc/def/blah", config.url);

            UrlDir.UrlFile file = config.parent;
            Assert.NotNull(file);
            Assert.Equal("def", file.name);
            Assert.Equal("cfg", file.fileExtension);
            Assert.Equal(UrlDir.FileType.Config, file.fileType);
            Assert.Contains(config, file.configs);

            UrlDir parent = file.parent;
            Assert.NotNull(parent);
            Assert.Equal("abc", parent.name);
            Assert.Contains(file, parent.files);

            UrlDir root = parent.parent;
            Assert.NotNull(root);
            Assert.Equal("root", root.name);
            Assert.Contains(parent, root.children);
            Assert.Null(root.parent);

            Assert.Same(root, file.root);
            Assert.Same(root, parent.root);
            Assert.Same(root, root.root);

            Assert.Contains(config, root.AllConfigs);
            Assert.Contains(config, root.GetConfigs("SOME_NODE"));
        }

        [Fact]
        public void TestCreateConfig__Url__FileAlreadyExists()
        {
            UrlDir root = UrlBuilder.CreateRoot();

            ConfigNode node1 = new TestConfigNode("SOME_NODE")
            {
                { "name", "blah" },
                { "foo", "bar" },
            };

            ConfigNode node2 = new TestConfigNode("SOME_OTHER_NODE")
            {
                { "name", "bleh" },
                { "jazz", "hands" },
            };

            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig("abc/def", node1, root);
            UrlDir.UrlConfig config2 = UrlBuilder.CreateConfig("abc/def", node2, root);

            UrlDir.UrlFile file = config1.parent;
            Assert.NotNull(file);

            Assert.Same(file, config2.parent);
            
            Assert.Equal("def", file.name);
            Assert.Equal("cfg", file.fileExtension);
            Assert.Equal(UrlDir.FileType.Config, file.fileType);
            Assert.Contains(config1, file.configs);
            Assert.Contains(config2, file.configs);

            UrlDir parent = file.parent;
            Assert.NotNull(parent);
            Assert.Equal("abc", parent.name);
            Assert.Contains(file, parent.files);

            Assert.Contains(parent, root.children);

            Assert.Same(root, file.root);
            Assert.Same(root, parent.root);
            Assert.Same(root, root.root);

            Assert.Contains(config1, root.AllConfigs);
            Assert.Contains(config1, root.GetConfigs("SOME_NODE"));

            Assert.Contains(config2, root.AllConfigs);
            Assert.Contains(config2, root.GetConfigs("SOME_OTHER_NODE"));
        }
    }
}
