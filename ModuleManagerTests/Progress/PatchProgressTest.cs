using System;
using Xunit;
using NSubstitute;
using TestUtils;
using ModuleManager;
using ModuleManager.Logging;
using ModuleManager.Progress;
using NodeStack = ModuleManager.Collections.ImmutableStack<ConfigNode>;

namespace ModuleManagerTests
{
    public class PatchProgressTest
    {
        private IBasicLogger logger;
        private PatchProgress progress;

        public PatchProgressTest()
        {
            logger = Substitute.For<IBasicLogger>();
            progress = new PatchProgress(logger);
        }

        [Fact]
        public void TestPatchAdded()
        {
            Assert.Equal(0, progress.TotalPatchCount);
            progress.PatchAdded();
            Assert.Equal(1, progress.TotalPatchCount);
            progress.PatchAdded();
            Assert.Equal(2, progress.TotalPatchCount);
        }

        [Fact]
        public void TestApplyingUpdate()
        {
            UrlDir.UrlConfig original = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_NODE"));
            UrlDir.UrlConfig patch1 = UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode("@SOME_NODE"));
            UrlDir.UrlConfig patch2 = UrlBuilder.CreateConfig("pqr/stu", new ConfigNode("@SOME_NODE"));

            Assert.Equal(0, progress.PatchedNodeCount);

            progress.ApplyingUpdate(original, patch1);
            Assert.Equal(1, progress.PatchedNodeCount);
            logger.Received().Info("Applying update ghi/jkl/@SOME_NODE to abc/def/SOME_NODE");

            progress.ApplyingUpdate(original, patch2);
            Assert.Equal(2, progress.PatchedNodeCount);
            logger.Received().Info("Applying update pqr/stu/@SOME_NODE to abc/def/SOME_NODE");
        }

        [Fact]
        public void TesApplyingCopy()
        {
            UrlDir.UrlConfig original = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_NODE"));
            UrlDir.UrlConfig patch1 = UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode("+SOME_NODE"));
            UrlDir.UrlConfig patch2 = UrlBuilder.CreateConfig("pqr/stu", new ConfigNode("+SOME_NODE"));

            Assert.Equal(0, progress.PatchedNodeCount);

            progress.ApplyingCopy(original, patch1);
            Assert.Equal(1, progress.PatchedNodeCount);
            logger.Received().Info("Applying copy ghi/jkl/+SOME_NODE to abc/def/SOME_NODE");

            progress.ApplyingCopy(original, patch2);
            Assert.Equal(2, progress.PatchedNodeCount);
            logger.Received().Info("Applying copy pqr/stu/+SOME_NODE to abc/def/SOME_NODE");
        }

        [Fact]
        public void TesApplyingDelete()
        {
            UrlDir.UrlConfig original = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_NODE"));
            UrlDir.UrlConfig patch1 = UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode("!SOME_NODE"));
            UrlDir.UrlConfig patch2 = UrlBuilder.CreateConfig("pqr/stu", new ConfigNode("!SOME_NODE"));

            Assert.Equal(0, progress.PatchedNodeCount);

            progress.ApplyingDelete(original, patch1);
            Assert.Equal(1, progress.PatchedNodeCount);
            logger.Received().Info("Applying delete ghi/jkl/!SOME_NODE to abc/def/SOME_NODE");

            progress.ApplyingDelete(original, patch2);
            Assert.Equal(2, progress.PatchedNodeCount);
            logger.Received().Info("Applying delete pqr/stu/!SOME_NODE to abc/def/SOME_NODE");
        }

        [Fact]
        public void TestPatchApplied()
        {
            Assert.Equal(0, progress.AppliedPatchCount);
            progress.PatchApplied();
            Assert.Equal(1, progress.AppliedPatchCount);
            progress.PatchApplied();
            Assert.Equal(2, progress.AppliedPatchCount);
        }

        [Fact]
        public void TestNeedsUnsatisfiedRoot()
        {
            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_NODE"));
            UrlDir.UrlConfig config2 = UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode("SOME_OTHER_NODE"));

            Assert.Equal(0, progress.NeedsUnsatisfiedCount);

            progress.NeedsUnsatisfiedRoot(config1);
            Assert.Equal(1, progress.NeedsUnsatisfiedCount);
            logger.Received().Info("Deleting root node in file abc/def node: SOME_NODE as it can't satisfy its NEEDS");

            progress.NeedsUnsatisfiedRoot(config2);
            Assert.Equal(2, progress.NeedsUnsatisfiedCount);
            logger.Received().Info("Deleting root node in file ghi/jkl node: SOME_OTHER_NODE as it can't satisfy its NEEDS");
        }

        [Fact]
        public void TestNeedsUnsatisfiedNode()
        {
            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_NODE"));
            NodeStack stack1 = new NodeStack(config1.config).Push(new ConfigNode("SOME_CHILD_NODE"));
            UrlDir.UrlConfig config2 = UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode("SOME_OTHER_NODE"));
            NodeStack stack2 = new NodeStack(config2.config).Push(new ConfigNode("SOME_OTHER_CHILD_NODE"));

            Assert.Equal(0, progress.NeedsUnsatisfiedCount);

            progress.NeedsUnsatisfiedNode(config1, stack1);
            Assert.Equal(0, progress.NeedsUnsatisfiedCount);
            logger.Received().Info("Deleting node in file abc/def subnode: SOME_NODE/SOME_CHILD_NODE as it can't satisfy its NEEDS");

            progress.NeedsUnsatisfiedNode(config2, stack2);
            Assert.Equal(0, progress.NeedsUnsatisfiedCount);
            logger.Received().Info("Deleting node in file ghi/jkl subnode: SOME_OTHER_NODE/SOME_OTHER_CHILD_NODE as it can't satisfy its NEEDS");
        }

        [Fact]
        public void TestNeedsUnsatisfiedValue()
        {
            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_NODE"));
            NodeStack stack1 = new NodeStack(config1.config).Push(new ConfigNode("SOME_CHILD_NODE"));
            UrlDir.UrlConfig config2 = UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode("SOME_OTHER_NODE"));
            NodeStack stack2 = new NodeStack(config2.config).Push(new ConfigNode("SOME_OTHER_CHILD_NODE"));

            Assert.Equal(0, progress.NeedsUnsatisfiedCount);

            progress.NeedsUnsatisfiedValue(config1, stack1, "some_value");
            Assert.Equal(0, progress.NeedsUnsatisfiedCount);
            logger.Received().Info("Deleting value in file abc/def subnode: SOME_NODE/SOME_CHILD_NODE value: some_value as it can't satisfy its NEEDS");

            progress.NeedsUnsatisfiedValue(config2, stack2, "some_other_value");
            Assert.Equal(0, progress.NeedsUnsatisfiedCount);
            logger.Received().Info("Deleting value in file ghi/jkl subnode: SOME_OTHER_NODE/SOME_OTHER_CHILD_NODE value: some_other_value as it can't satisfy its NEEDS");
        }

        [Fact]
        public void TestNeedsUnsatisfiedBefore()
        {
            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_NODE"));
            UrlDir.UrlConfig config2 = UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode("SOME_OTHER_NODE"));

            Assert.Equal(0, progress.NeedsUnsatisfiedCount);

            progress.NeedsUnsatisfiedBefore(config1);
            Assert.Equal(1, progress.NeedsUnsatisfiedCount);
            logger.Received().Info("Deleting root node in file abc/def node: SOME_NODE as it can't satisfy its BEFORE");

            progress.NeedsUnsatisfiedBefore(config2);
            Assert.Equal(2, progress.NeedsUnsatisfiedCount);
            logger.Received().Info("Deleting root node in file ghi/jkl node: SOME_OTHER_NODE as it can't satisfy its BEFORE");
        }

        [Fact]
        public void TestNeedsUnsatisfiedFor()
        {
            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_NODE"));
            UrlDir.UrlConfig config2 = UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode("SOME_OTHER_NODE"));

            Assert.Equal(0, progress.NeedsUnsatisfiedCount);

            progress.NeedsUnsatisfiedFor(config1);
            Assert.Equal(1, progress.NeedsUnsatisfiedCount);
            logger.Received().Warning("Deleting root node in file abc/def node: SOME_NODE as it can't satisfy its FOR (this shouldn't happen)");

            progress.NeedsUnsatisfiedFor(config2);
            Assert.Equal(2, progress.NeedsUnsatisfiedCount);
            logger.Received().Warning("Deleting root node in file ghi/jkl node: SOME_OTHER_NODE as it can't satisfy its FOR (this shouldn't happen)");
        }

        [Fact]
        public void TestNeedsUnsatisfiedAfter()
        {
            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_NODE"));
            UrlDir.UrlConfig config2 = UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode("SOME_OTHER_NODE"));

            Assert.Equal(0, progress.NeedsUnsatisfiedCount);

            progress.NeedsUnsatisfiedAfter(config1);
            Assert.Equal(1, progress.NeedsUnsatisfiedCount);
            logger.Received().Info("Deleting root node in file abc/def node: SOME_NODE as it can't satisfy its AFTER");

            progress.NeedsUnsatisfiedAfter(config2);
            Assert.Equal(2, progress.NeedsUnsatisfiedCount);
            logger.Received().Info("Deleting root node in file ghi/jkl node: SOME_OTHER_NODE as it can't satisfy its AFTER");
        }

        [Fact]
        public void TestError()
        {
            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_NODE"));
            UrlDir.UrlConfig config2 = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_OTHER_NODE"));

            Assert.Equal(0, progress.ErrorCount);
            Assert.False(progress.ErrorFiles.ContainsKey("abc/def.cfg"));

            progress.Error(config1, "An error message no one is going to read");
            Assert.Equal(1, progress.ErrorCount);
            Assert.Equal(1, progress.ErrorFiles["abc/def.cfg"]);
            logger.Received().Error("An error message no one is going to read");

            progress.Error(config2, "Maybe someone will read this one");
            Assert.Equal(2, progress.ErrorCount);
            Assert.Equal(2, progress.ErrorFiles["abc/def.cfg"]);
            logger.Received().Error("Maybe someone will read this one");
        }

        [Fact]
        public void TestException()
        {
            Exception e1 = new Exception();
            Exception e2 = new Exception();

            Assert.Equal(0, progress.ExceptionCount);

            progress.Exception("An exception was thrown", e1);
            Assert.Equal(1, progress.ExceptionCount);
            logger.Received().Exception("An exception was thrown", e1);

            progress.Exception("An exception was tossed", e2);
            Assert.Equal(2, progress.ExceptionCount);
            logger.Received().Exception("An exception was tossed", e2);
        }

        [Fact]
        public void TestException__Url()
        {
            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_NODE"));
            Exception e1 = new Exception();
            UrlDir.UrlConfig config2 = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_OTHER_NODE"));
            Exception e2 = new Exception();

            Assert.Equal(0, progress.ExceptionCount);
            Assert.False(progress.ErrorFiles.ContainsKey("abc/def.cfg"));

            progress.Exception(config1, "An exception was thrown", e1);
            Assert.Equal(1, progress.ExceptionCount);
            Assert.Equal(1, progress.ErrorFiles["abc/def.cfg"]);
            logger.Received().Exception("An exception was thrown", e1);

            progress.Exception(config2, "An exception was tossed", e2);
            Assert.Equal(2, progress.ExceptionCount);
            Assert.Equal(2, progress.ErrorFiles["abc/def.cfg"]);
            logger.Received().Exception("An exception was tossed", e2);
        }
    }
}
