using System;
using Xunit;
using NSubstitute;
using TestUtils;
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
        public void Test__Constructor__Nested()
        {
            IBasicLogger logger2 = Substitute.For<IBasicLogger>();
            PatchProgress progress2 = new PatchProgress(progress, logger2);

            Assert.Same(progress.Counter, progress2.Counter);

            Assert.Equal(0, progress.Counter.patchedNodes);

            UrlDir.UrlConfig original = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_NODE"));
            UrlDir.UrlConfig patch1 = UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode("@SOME_NODE"));

            progress2.ApplyingUpdate(original, patch1);
            Assert.Equal(1, progress.Counter.patchedNodes);
            logger.DidNotReceiveWithAnyArgs().Info(null);
            logger2.Received().Info("Applying update ghi/jkl/@SOME_NODE to abc/def/SOME_NODE");
        }

        [Fact]
        public void TestPatchAdded()
        {
            Assert.Equal(0, progress.Counter.totalPatches);
            progress.PatchAdded();
            Assert.Equal(1, progress.Counter.totalPatches);
            progress.PatchAdded();
            Assert.Equal(2, progress.Counter.totalPatches);
        }

        [Fact]
        public void TestApplyingUpdate()
        {
            UrlDir.UrlConfig original = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_NODE"));
            UrlDir.UrlConfig patch1 = UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode("@SOME_NODE"));
            UrlDir.UrlConfig patch2 = UrlBuilder.CreateConfig("pqr/stu", new ConfigNode("@SOME_NODE"));

            Assert.Equal(0, progress.Counter.patchedNodes);

            progress.ApplyingUpdate(original, patch1);
            Assert.Equal(1, progress.Counter.patchedNodes);
            logger.Received().Info("Applying update ghi/jkl/@SOME_NODE to abc/def/SOME_NODE");

            progress.ApplyingUpdate(original, patch2);
            Assert.Equal(2, progress.Counter.patchedNodes);
            logger.Received().Info("Applying update pqr/stu/@SOME_NODE to abc/def/SOME_NODE");
        }

        [Fact]
        public void TesApplyingCopy()
        {
            UrlDir.UrlConfig original = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_NODE"));
            UrlDir.UrlConfig patch1 = UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode("+SOME_NODE"));
            UrlDir.UrlConfig patch2 = UrlBuilder.CreateConfig("pqr/stu", new ConfigNode("+SOME_NODE"));

            Assert.Equal(0, progress.Counter.patchedNodes);

            progress.ApplyingCopy(original, patch1);
            Assert.Equal(1, progress.Counter.patchedNodes);
            logger.Received().Info("Applying copy ghi/jkl/+SOME_NODE to abc/def/SOME_NODE");

            progress.ApplyingCopy(original, patch2);
            Assert.Equal(2, progress.Counter.patchedNodes);
            logger.Received().Info("Applying copy pqr/stu/+SOME_NODE to abc/def/SOME_NODE");
        }

        [Fact]
        public void TesApplyingDelete()
        {
            UrlDir.UrlConfig original = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_NODE"));
            UrlDir.UrlConfig patch1 = UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode("!SOME_NODE"));
            UrlDir.UrlConfig patch2 = UrlBuilder.CreateConfig("pqr/stu", new ConfigNode("!SOME_NODE"));

            Assert.Equal(0, progress.Counter.patchedNodes);

            progress.ApplyingDelete(original, patch1);
            Assert.Equal(1, progress.Counter.patchedNodes);
            logger.Received().Info("Applying delete ghi/jkl/!SOME_NODE to abc/def/SOME_NODE");

            progress.ApplyingDelete(original, patch2);
            Assert.Equal(2, progress.Counter.patchedNodes);
            logger.Received().Info("Applying delete pqr/stu/!SOME_NODE to abc/def/SOME_NODE");
        }

        [Fact]
        public void TestPatchApplied()
        {
            Assert.Equal(0, progress.Counter.appliedPatches);
            progress.PatchApplied();
            Assert.Equal(1, progress.Counter.appliedPatches);
            progress.PatchApplied();
            Assert.Equal(2, progress.Counter.appliedPatches);
        }

        [Fact]
        public void TestNeedsUnsatisfiedRoot()
        {
            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_NODE"));
            UrlDir.UrlConfig config2 = UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode("SOME_OTHER_NODE"));

            Assert.Equal(0, progress.Counter.needsUnsatisfied);

            progress.NeedsUnsatisfiedRoot(config1);
            Assert.Equal(1, progress.Counter.needsUnsatisfied);
            logger.Received().Info("Deleting root node in file abc/def node: SOME_NODE as it can't satisfy its NEEDS");

            progress.NeedsUnsatisfiedRoot(config2);
            Assert.Equal(2, progress.Counter.needsUnsatisfied);
            logger.Received().Info("Deleting root node in file ghi/jkl node: SOME_OTHER_NODE as it can't satisfy its NEEDS");
        }

        [Fact]
        public void TestNeedsUnsatisfiedNode()
        {
            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_NODE"));
            NodeStack stack1 = new NodeStack(config1.config).Push(new ConfigNode("SOME_CHILD_NODE"));
            UrlDir.UrlConfig config2 = UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode("SOME_OTHER_NODE"));
            NodeStack stack2 = new NodeStack(config2.config).Push(new ConfigNode("SOME_OTHER_CHILD_NODE"));

            Assert.Equal(0, progress.Counter.needsUnsatisfied);

            progress.NeedsUnsatisfiedNode(config1, stack1);
            Assert.Equal(0, progress.Counter.needsUnsatisfied);
            logger.Received().Info("Deleting node in file abc/def subnode: SOME_NODE/SOME_CHILD_NODE as it can't satisfy its NEEDS");

            progress.NeedsUnsatisfiedNode(config2, stack2);
            Assert.Equal(0, progress.Counter.needsUnsatisfied);
            logger.Received().Info("Deleting node in file ghi/jkl subnode: SOME_OTHER_NODE/SOME_OTHER_CHILD_NODE as it can't satisfy its NEEDS");
        }

        [Fact]
        public void TestNeedsUnsatisfiedValue()
        {
            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_NODE"));
            NodeStack stack1 = new NodeStack(config1.config).Push(new ConfigNode("SOME_CHILD_NODE"));
            UrlDir.UrlConfig config2 = UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode("SOME_OTHER_NODE"));
            NodeStack stack2 = new NodeStack(config2.config).Push(new ConfigNode("SOME_OTHER_CHILD_NODE"));

            Assert.Equal(0, progress.Counter.needsUnsatisfied);

            progress.NeedsUnsatisfiedValue(config1, stack1, "some_value");
            Assert.Equal(0, progress.Counter.needsUnsatisfied);
            logger.Received().Info("Deleting value in file abc/def subnode: SOME_NODE/SOME_CHILD_NODE value: some_value as it can't satisfy its NEEDS");

            progress.NeedsUnsatisfiedValue(config2, stack2, "some_other_value");
            Assert.Equal(0, progress.Counter.needsUnsatisfied);
            logger.Received().Info("Deleting value in file ghi/jkl subnode: SOME_OTHER_NODE/SOME_OTHER_CHILD_NODE value: some_other_value as it can't satisfy its NEEDS");
        }

        [Fact]
        public void TestNeedsUnsatisfiedBefore()
        {
            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_NODE"));
            UrlDir.UrlConfig config2 = UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode("SOME_OTHER_NODE"));

            Assert.Equal(0, progress.Counter.needsUnsatisfied);

            progress.NeedsUnsatisfiedBefore(config1);
            Assert.Equal(1, progress.Counter.needsUnsatisfied);
            logger.Received().Info("Deleting root node in file abc/def node: SOME_NODE as it can't satisfy its BEFORE");

            progress.NeedsUnsatisfiedBefore(config2);
            Assert.Equal(2, progress.Counter.needsUnsatisfied);
            logger.Received().Info("Deleting root node in file ghi/jkl node: SOME_OTHER_NODE as it can't satisfy its BEFORE");
        }

        [Fact]
        public void TestNeedsUnsatisfiedFor()
        {
            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_NODE"));
            UrlDir.UrlConfig config2 = UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode("SOME_OTHER_NODE"));

            Assert.Equal(0, progress.Counter.needsUnsatisfied);

            progress.NeedsUnsatisfiedFor(config1);
            Assert.Equal(1, progress.Counter.needsUnsatisfied);
            logger.Received().Warning("Deleting root node in file abc/def node: SOME_NODE as it can't satisfy its FOR (this shouldn't happen)");

            progress.NeedsUnsatisfiedFor(config2);
            Assert.Equal(2, progress.Counter.needsUnsatisfied);
            logger.Received().Warning("Deleting root node in file ghi/jkl node: SOME_OTHER_NODE as it can't satisfy its FOR (this shouldn't happen)");
        }

        [Fact]
        public void TestNeedsUnsatisfiedAfter()
        {
            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_NODE"));
            UrlDir.UrlConfig config2 = UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode("SOME_OTHER_NODE"));

            Assert.Equal(0, progress.Counter.needsUnsatisfied);

            progress.NeedsUnsatisfiedAfter(config1);
            Assert.Equal(1, progress.Counter.needsUnsatisfied);
            logger.Received().Info("Deleting root node in file abc/def node: SOME_NODE as it can't satisfy its AFTER");

            progress.NeedsUnsatisfiedAfter(config2);
            Assert.Equal(2, progress.Counter.needsUnsatisfied);
            logger.Received().Info("Deleting root node in file ghi/jkl node: SOME_OTHER_NODE as it can't satisfy its AFTER");
        }

        [Fact]
        public void TestError()
        {
            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_NODE"));
            UrlDir.UrlConfig config2 = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_OTHER_NODE"));

            Assert.Equal(0, progress.Counter.errors);
            Assert.False(progress.Counter.errorFiles.ContainsKey("abc/def.cfg"));

            progress.Error(config1, "An error message no one is going to read");
            Assert.Equal(1, progress.Counter.errors);
            Assert.Equal(1, progress.Counter.errorFiles["abc/def.cfg"]);
            logger.Received().Error("An error message no one is going to read");

            progress.Error(config2, "Maybe someone will read this one");
            Assert.Equal(2, progress.Counter.errors);
            Assert.Equal(2, progress.Counter.errorFiles["abc/def.cfg"]);
            logger.Received().Error("Maybe someone will read this one");
        }

        [Fact]
        public void TestException()
        {
            Exception e1 = new Exception();
            Exception e2 = new Exception();

            Assert.Equal(0, progress.Counter.exceptions);

            progress.Exception("An exception was thrown", e1);
            Assert.Equal(1, progress.Counter.exceptions);
            logger.Received().Exception("An exception was thrown", e1);

            progress.Exception("An exception was tossed", e2);
            Assert.Equal(2, progress.Counter.exceptions);
            logger.Received().Exception("An exception was tossed", e2);
        }

        [Fact]
        public void TestException__Url()
        {
            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_NODE"));
            Exception e1 = new Exception();
            UrlDir.UrlConfig config2 = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_OTHER_NODE"));
            Exception e2 = new Exception();

            Assert.Equal(0, progress.Counter.exceptions);
            Assert.False(progress.Counter.errorFiles.ContainsKey("abc/def.cfg"));

            progress.Exception(config1, "An exception was thrown", e1);
            Assert.Equal(1, progress.Counter.exceptions);
            Assert.Equal(1, progress.Counter.errorFiles["abc/def.cfg"]);
            logger.Received().Exception("An exception was thrown", e1);

            progress.Exception(config2, "An exception was tossed", e2);
            Assert.Equal(2, progress.Counter.exceptions);
            Assert.Equal(2, progress.Counter.errorFiles["abc/def.cfg"]);
            logger.Received().Exception("An exception was tossed", e2);
        }
    }
}
