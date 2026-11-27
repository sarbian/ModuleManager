using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Xunit;
using NSubstitute;
using TestUtils;
using ModuleManager;
using ModuleManager.Extensions;
using ModuleManager.Logging;

namespace ModuleManagerTests
{
    public class CheckFilesChangeTest
    {
        // What CheckFilesChange did before it indexed the FILE nodes: search them for every file
        private static bool OldCheckFilesChange(UrlDir.UrlFile[] files, ConfigNode shaConfigNode, Dictionary<string, string> filesSha, StringBuilder changes)
        {
            ConfigNode GetFileNode(string filename)
            {
                for (int i = 0; i < shaConfigNode.nodes.Count; i++)
                {
                    ConfigNode file = shaConfigNode.nodes[i];
                    if (file.name == "FILE" && file.GetValue("filename") == filename)
                        return file;
                }
                return null;
            }

            bool noChange = true;
            changes.Append("Changes :\n");
            for (int i = 0; i < files.Length; i++)
            {
                string url = files[i].GetUrlWithExtension();
                ConfigNode fileNode = GetFileNode(url);
                string fileSha = fileNode?.GetValue("SHA");
                if (fileNode == null)
                    continue;
                if (fileSha == null || filesSha[url] != fileSha)
                {
                    changes.Append("Changed : " + fileNode.GetValue("filename") + ".cfg\n");
                    noChange = false;
                }
            }
            for (int i = 0; i < files.Length; i++)
            {
                string url = files[i].GetUrlWithExtension();
                ConfigNode fileNode = GetFileNode(url);
                if (fileNode == null)
                {
                    changes.Append("Added   : " + url + "\n");
                    noChange = false;
                }
                shaConfigNode.RemoveNode(fileNode);
            }
            foreach (ConfigNode fileNode in shaConfigNode.GetNodes())
            {
                changes.Append("Deleted : " + fileNode.GetValue("filename") + "\n");
                noChange = false;
            }
            return noChange;
        }

        private static ConfigNode File(string filename, string sha)
        {
            ConfigNode node = new ConfigNode("FILE");
            if (filename != null) node.AddValue("filename", filename);
            if (sha != null) node.AddValue("SHA", sha);
            return node;
        }

        private static ConfigNode MakeShaNode(bool withChanges)
        {
            ConfigNode node = new ConfigNode("FilesSHA");
            node.AddNode(File("abc/a.cfg", "A"));
            node.AddNode(File("abc/b.cfg", withChanges ? "B-old" : "B"));
            node.AddNode(File("abc/c.cfg", "C"));
            if (withChanges)
            {
                node.AddNode(File("abc/gone.cfg", "G"));
                node.AddNode(File("abc/a.cfg", "A-dup"));
                node.AddNode(File("abc/c.cfg", null));
                node.AddNode(File(null, "nameless"));
                node.AddNode(new ConfigNode("NOT_A_FILE"));
            }
            return node;
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void TestCheckFilesChange(bool withChanges, bool duplicateFile)
        {
            UrlDir gameData = UrlBuilder.CreateGameData();
            List<UrlDir.UrlFile> fileList = new List<UrlDir.UrlFile>
            {
                UrlBuilder.CreateFile("abc/a.cfg", gameData),
                UrlBuilder.CreateFile("abc/b.cfg", gameData),
                UrlBuilder.CreateFile("abc/c.cfg", gameData),
            };
            if (withChanges) fileList.Add(UrlBuilder.CreateFile("abc/new.cfg", gameData));
            if (duplicateFile) fileList.Add(fileList[0]);
            UrlDir.UrlFile[] files = fileList.ToArray();

            Dictionary<string, string> filesSha = new Dictionary<string, string>
            {
                { "abc/a.cfg", "A" }, { "abc/b.cfg", "B" }, { "abc/c.cfg", "C" }, { "abc/new.cfg", "N" },
            };

            StringBuilder expectedChanges = new StringBuilder();
            bool expected = OldCheckFilesChange(files, MakeShaNode(withChanges), filesSha, expectedChanges);

            IBasicLogger logger = Substitute.For<IBasicLogger>();
            MMPatchLoader loader = new MMPatchLoader(new ModListGenerator.ModAddedByAssembly[0], logger);
            Dictionary<string, string> loaderFilesSha = (Dictionary<string, string>)typeof(MMPatchLoader)
                .GetField("filesSha", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(loader);
            foreach (KeyValuePair<string, string> pair in filesSha) loaderFilesSha.Add(pair.Key, pair.Value);
            MethodInfo checkFilesChange = typeof(MMPatchLoader).GetMethod("CheckFilesChange", BindingFlags.NonPublic | BindingFlags.Instance);

            bool result = (bool)checkFilesChange.Invoke(loader, new object[] { files, MakeShaNode(withChanges) });

            Assert.Equal(expected, result);
            Assert.Equal(!withChanges, result);
            if (expected)
                logger.AssertNoInfo();
            else
                logger.AssertInfo(expectedChanges.ToString());
        }
    }
}
