using System.Linq;
using Xunit;
using NSubstitute;
using TestUtils;
using ModuleManager;
using ModuleManager.Collections;
using ModuleManager.Logging;
using ModuleManager.Patches;
using ModuleManager.Patches.PassSpecifiers;
using ModuleManager.Progress;

namespace ModuleManagerTests
{
    public class PatchApplierRenameTest
    {
        [Fact]
        public void TestApplyPatches__Rename()
        {
            // Patches that change a node's name value are matched by its new name afterward,
            // through the name value each new ProtoUrlConfig stores.
            IPatchProgress progress = Substitute.For<IPatchProgress>();
            PatchApplier patchApplier = new PatchApplier(progress, Substitute.For<IBasicLogger>());
            IPassSpecifier passSpecifier = Substitute.For<IPassSpecifier>();

            IPatch Insert(string name) => new InsertPatch(
                UrlBuilder.CreateConfig("abc/" + name, new TestConfigNode("PART") { { "name", name } }), "PART", passSpecifier);
            IPatch Edit(string url, string name, string key, string value) => new EditPatch(
                UrlBuilder.CreateConfig(url, new TestConfigNode("@PART[" + name + "]") { { key, value } }), new NodeMatcher("PART", name, null), passSpecifier);

            IPass pass1 = Substitute.For<IPass>();
            pass1.GetEnumerator().Returns(new ArrayEnumerator<IPatch>(Insert("a"), Insert("other")));

            IPass pass2 = Substitute.For<IPass>();
            pass2.GetEnumerator().Returns(new ArrayEnumerator<IPatch>(
                Edit("ghi/e1", "a", "@name", "b"),
                Edit("ghi/e2", "b", "x", "1"),
                Edit("ghi/e3", "a", "y", "2"),
                new CopyPatch(UrlBuilder.CreateConfig("ghi/c1", new TestConfigNode("+PART[b]") { { "@name", "c" } }), new NodeMatcher("PART", "b", null), passSpecifier),
                Edit("ghi/e4", "c", "z", "3"),
                Edit("ghi/e5", "b", "w", "4"),
                new DeletePatch(UrlBuilder.CreateConfig("ghi/d1", new ConfigNode("!PART[other]")), new NodeMatcher("PART", "other", null), passSpecifier)
            ));

            IProtoUrlConfig[] configs = patchApplier.ApplyPatches(new[] { pass1, pass2 }).ToArray();

            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);

            Assert.Equal(2, configs.Length);
            Assert.Equal(new TestConfigNode("PART") { { "name", "b" }, { "x", "1" }, { "w", "4" } }.ToString(), configs[0].Node.ToString());
            Assert.Equal(new TestConfigNode("PART") { { "name", "c" }, { "x", "1" }, { "z", "3" } }.ToString(), configs[1].Node.ToString());
        }

        [Fact]
        public void TestApplyPatches__Rename__SpecialCommand()
        {
            // A special command writes to another root node's name value in place, with no new
            // ProtoUrlConfig, and patches match that node by its new name afterward.
            IPatchProgress progress = Substitute.For<IPatchProgress>();
            PatchApplier patchApplier = new PatchApplier(progress, Substitute.For<IBasicLogger>());
            IPassSpecifier passSpecifier = Substitute.For<IPassSpecifier>();

            IPatch Insert(string name) => new InsertPatch(
                UrlBuilder.CreateConfig("abc/" + name, new TestConfigNode("PART") { { "name", name } }), "PART", passSpecifier);
            IPatch Edit(string url, string name, string key, string value) => new EditPatch(
                UrlBuilder.CreateConfig(url, new TestConfigNode("@PART[" + name + "]") { { key, value } }), new NodeMatcher("PART", name, null), passSpecifier);

            IPass pass1 = Substitute.For<IPass>();
            pass1.GetEnumerator().Returns(new ArrayEnumerator<IPatch>(Insert("source"), Insert("target")));

            IPass pass2 = Substitute.For<IPass>();
            pass2.GetEnumerator().Returns(new ArrayEnumerator<IPatch>(
                Edit("ghi/e1", "source", "*@PART[target]/name", "renamed"),
                Edit("ghi/e2", "renamed", "x", "1"),
                Edit("ghi/e3", "target", "y", "2")
            ));

            IProtoUrlConfig[] configs = patchApplier.ApplyPatches(new[] { pass1, pass2 }).ToArray();

            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);

            Assert.Equal(2, configs.Length);
            Assert.Equal(new TestConfigNode("PART") { { "name", "source" } }.ToString(), configs[0].Node.ToString());
            Assert.Equal(new TestConfigNode("PART") { { "name", "renamed" }, { "x", "1" } }.ToString(), configs[1].Node.ToString());
        }
    }
}
