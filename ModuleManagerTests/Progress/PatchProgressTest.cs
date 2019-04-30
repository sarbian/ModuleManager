using System;
using Xunit;
using NSubstitute;
using UnityEngine;
using TestUtils;
using ModuleManager;
using ModuleManager.Logging;
using ModuleManager.Progress;

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

            IProtoUrlConfig original = Substitute.For<IProtoUrlConfig>();
            original.FullUrl.Returns("abc/def.cfg/SOME_NODE");
            UrlDir.UrlConfig patch1 = UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode("@SOME_NODE"));

            progress2.ApplyingUpdate(original, patch1);
            Assert.Equal(1, progress.Counter.patchedNodes);
            logger.AssertNoLog();
            logger2.AssertInfo("Applying update ghi/jkl/@SOME_NODE to abc/def.cfg/SOME_NODE");
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
            IProtoUrlConfig original = Substitute.For<IProtoUrlConfig>();
            original.FullUrl.Returns("abc/def.cfg/SOME_NODE");
            UrlDir.UrlConfig patch1 = UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode("@SOME_NODE"));
            UrlDir.UrlConfig patch2 = UrlBuilder.CreateConfig("pqr/stu", new ConfigNode("@SOME_NODE"));

            Assert.Equal(0, progress.Counter.patchedNodes);

            progress.ApplyingUpdate(original, patch1);
            Assert.Equal(1, progress.Counter.patchedNodes);
            logger.AssertInfo("Applying update ghi/jkl/@SOME_NODE to abc/def.cfg/SOME_NODE");

            progress.ApplyingUpdate(original, patch2);
            Assert.Equal(2, progress.Counter.patchedNodes);
            logger.AssertInfo("Applying update pqr/stu/@SOME_NODE to abc/def.cfg/SOME_NODE");
        }

        [Fact]
        public void TesApplyingCopy()
        {
            IProtoUrlConfig original = Substitute.For<IProtoUrlConfig>();
            original.FullUrl.Returns("abc/def.cfg/SOME_NODE");
            UrlDir.UrlConfig patch1 = UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode("+SOME_NODE"));
            UrlDir.UrlConfig patch2 = UrlBuilder.CreateConfig("pqr/stu", new ConfigNode("+SOME_NODE"));

            Assert.Equal(0, progress.Counter.patchedNodes);

            progress.ApplyingCopy(original, patch1);
            Assert.Equal(1, progress.Counter.patchedNodes);
            logger.AssertInfo("Applying copy ghi/jkl/+SOME_NODE to abc/def.cfg/SOME_NODE");

            progress.ApplyingCopy(original, patch2);
            Assert.Equal(2, progress.Counter.patchedNodes);
            logger.AssertInfo("Applying copy pqr/stu/+SOME_NODE to abc/def.cfg/SOME_NODE");
        }

        [Fact]
        public void TesApplyingDelete()
        {
            IProtoUrlConfig original = Substitute.For<IProtoUrlConfig>();
            original.FullUrl.Returns("abc/def.cfg/SOME_NODE");
            UrlDir.UrlConfig patch1 = UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode("!SOME_NODE"));
            UrlDir.UrlConfig patch2 = UrlBuilder.CreateConfig("pqr/stu", new ConfigNode("!SOME_NODE"));

            Assert.Equal(0, progress.Counter.patchedNodes);

            progress.ApplyingDelete(original, patch1);
            Assert.Equal(1, progress.Counter.patchedNodes);
            logger.AssertInfo("Applying delete ghi/jkl/!SOME_NODE to abc/def.cfg/SOME_NODE");

            progress.ApplyingDelete(original, patch2);
            Assert.Equal(2, progress.Counter.patchedNodes);
            logger.AssertInfo("Applying delete pqr/stu/!SOME_NODE to abc/def.cfg/SOME_NODE");
        }

        [Fact]
        public void TestPatchApplied()
        {
            int eventCounter = 0;
            progress.OnPatchApplied.Add(() => eventCounter++);
            Assert.Equal(0, progress.Counter.appliedPatches);
            progress.PatchApplied();
            Assert.Equal(1, progress.Counter.appliedPatches);
            Assert.Equal(1, eventCounter);
            progress.PatchApplied();
            Assert.Equal(2, progress.Counter.appliedPatches);
            Assert.Equal(2, eventCounter);
        }

        [Fact]
        public void TestNeedsUnsatisfiedRoot()
        {
            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_NODE"));
            UrlDir.UrlConfig config2 = UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode("SOME_OTHER_NODE"));

            Assert.Equal(0, progress.Counter.needsUnsatisfied);

            progress.NeedsUnsatisfiedRoot(config1);
            Assert.Equal(1, progress.Counter.needsUnsatisfied);
            logger.AssertInfo("Deleting root node in file abc/def node: SOME_NODE as it can't satisfy its NEEDS");

            progress.NeedsUnsatisfiedRoot(config2);
            Assert.Equal(2, progress.Counter.needsUnsatisfied);
            logger.AssertInfo("Deleting root node in file ghi/jkl node: SOME_OTHER_NODE as it can't satisfy its NEEDS");
        }

        [Fact]
        public void TestNeedsUnsatisfiedNode()
        {
            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_NODE"));
            UrlDir.UrlConfig config2 = UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode("SOME_OTHER_NODE"));

            Assert.Equal(0, progress.Counter.needsUnsatisfied);

            progress.NeedsUnsatisfiedNode(config1, "SOME/NODE/PATH/SOME_CHILD_NODE");
            Assert.Equal(0, progress.Counter.needsUnsatisfied);
            logger.AssertInfo("Deleting node in file abc/def subnode: SOME/NODE/PATH/SOME_CHILD_NODE as it can't satisfy its NEEDS");

            progress.NeedsUnsatisfiedNode(config2, "SOME/NODE/PATH/SOME_OTHER_CHILD_NODE");
            Assert.Equal(0, progress.Counter.needsUnsatisfied);
            logger.AssertInfo("Deleting node in file ghi/jkl subnode: SOME/NODE/PATH/SOME_OTHER_CHILD_NODE as it can't satisfy its NEEDS");
        }

        [Fact]
        public void TestNeedsUnsatisfiedValue()
        {
            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_NODE"));
            UrlDir.UrlConfig config2 = UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode("SOME_OTHER_NODE"));

            Assert.Equal(0, progress.Counter.needsUnsatisfied);

            progress.NeedsUnsatisfiedValue(config1, "SOME/NODE/PATH/some_value");
            Assert.Equal(0, progress.Counter.needsUnsatisfied);
            logger.AssertInfo("Deleting value in file abc/def value: SOME/NODE/PATH/some_value as it can't satisfy its NEEDS");

            progress.NeedsUnsatisfiedValue(config2, "SOME/NODE/PATH/some_other_value");
            Assert.Equal(0, progress.Counter.needsUnsatisfied);
            logger.AssertInfo("Deleting value in file ghi/jkl value: SOME/NODE/PATH/some_other_value as it can't satisfy its NEEDS");
        }

        [Fact]
        public void TestNeedsUnsatisfiedBefore()
        {
            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_NODE"));
            UrlDir.UrlConfig config2 = UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode("SOME_OTHER_NODE"));

            Assert.Equal(0, progress.Counter.needsUnsatisfied);

            progress.NeedsUnsatisfiedBefore(config1);
            Assert.Equal(1, progress.Counter.needsUnsatisfied);
            logger.AssertInfo("Deleting root node in file abc/def node: SOME_NODE as it can't satisfy its BEFORE");

            progress.NeedsUnsatisfiedBefore(config2);
            Assert.Equal(2, progress.Counter.needsUnsatisfied);
            logger.AssertInfo("Deleting root node in file ghi/jkl node: SOME_OTHER_NODE as it can't satisfy its BEFORE");
        }

        [Fact]
        public void TestNeedsUnsatisfiedFor()
        {
            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_NODE"));
            UrlDir.UrlConfig config2 = UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode("SOME_OTHER_NODE"));

            Assert.Equal(0, progress.Counter.needsUnsatisfied);

            progress.NeedsUnsatisfiedFor(config1);
            Assert.Equal(1, progress.Counter.needsUnsatisfied);
            logger.AssertWarning("Deleting root node in file abc/def node: SOME_NODE as it can't satisfy its FOR (this shouldn't happen)");

            progress.NeedsUnsatisfiedFor(config2);
            Assert.Equal(2, progress.Counter.needsUnsatisfied);
            logger.AssertWarning("Deleting root node in file ghi/jkl node: SOME_OTHER_NODE as it can't satisfy its FOR (this shouldn't happen)");
        }

        [Fact]
        public void TestNeedsUnsatisfiedAfter()
        {
            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_NODE"));
            UrlDir.UrlConfig config2 = UrlBuilder.CreateConfig("ghi/jkl", new ConfigNode("SOME_OTHER_NODE"));

            Assert.Equal(0, progress.Counter.needsUnsatisfied);

            progress.NeedsUnsatisfiedAfter(config1);
            Assert.Equal(1, progress.Counter.needsUnsatisfied);
            logger.AssertInfo("Deleting root node in file abc/def node: SOME_NODE as it can't satisfy its AFTER");

            progress.NeedsUnsatisfiedAfter(config2);
            Assert.Equal(2, progress.Counter.needsUnsatisfied);
            logger.AssertInfo("Deleting root node in file ghi/jkl node: SOME_OTHER_NODE as it can't satisfy its AFTER");
        }

        [Fact]
        public void TestStartingPass()
        {
            EventData<IPass>.OnEvent onEvent = Substitute.For<EventData<IPass>.OnEvent>();
            progress.OnPassStarted.Add(onEvent);
            IPass pass1 = Substitute.For<IPass>();
            pass1.Name.Returns(":SOME_PASS");

            progress.PassStarted(pass1);

            logger.AssertInfo(":SOME_PASS pass");
            onEvent.Received()(pass1);
        }

        [Fact]
        public void TestStartingPass__NullArgument()
        {
            EventData<IPass>.OnEvent onEvent = Substitute.For<EventData<IPass>.OnEvent>();
            progress.OnPassStarted.Add(onEvent);

            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                progress.PassStarted(null);
            });

            Assert.Equal("pass", ex.ParamName);

            logger.AssertNoLog();
            onEvent.DidNotReceiveWithAnyArgs()(null);
        }

        [Fact]
        public void TestWarning()
        {
            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_NODE"));
            UrlDir.UrlConfig config2 = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_OTHER_NODE"));

            Assert.Equal(0, progress.Counter.warnings);

            progress.Warning(config1, "I'm warning you");
            Assert.Equal(1, progress.Counter.warnings);
            Assert.Equal(1, progress.Counter.warningFiles["abc/def.cfg"]);
            logger.AssertWarning("I'm warning you");

            progress.Warning(config2, "You should probably pay attention to this");
            Assert.Equal(2, progress.Counter.warnings);
            Assert.Equal(2, progress.Counter.warningFiles["abc/def.cfg"]);
            logger.AssertWarning("You should probably pay attention to this");
        }

        [Fact]
        public void TestError()
        {
            Assert.Equal(0, progress.Counter.errors);

            progress.Error("An error message no one is going to read");
            Assert.Equal(1, progress.Counter.errors);

            progress.Error("Maybe someone will read this one");
            Assert.Equal(2, progress.Counter.errors);
        }

        [Fact]
        public void TestError__Config()
        {
            UrlDir.UrlConfig config1 = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_NODE"));
            UrlDir.UrlConfig config2 = UrlBuilder.CreateConfig("abc/def", new ConfigNode("SOME_OTHER_NODE"));

            Assert.Equal(0, progress.Counter.errors);
            Assert.False(progress.Counter.errorFiles.ContainsKey("abc/def.cfg"));

            progress.Error(config1, "An error message no one is going to read");
            Assert.Equal(1, progress.Counter.errors);
            Assert.Equal(1, progress.Counter.errorFiles["abc/def.cfg"]);
            logger.AssertError("An error message no one is going to read");

            progress.Error(config2, "Maybe someone will read this one");
            Assert.Equal(2, progress.Counter.errors);
            Assert.Equal(2, progress.Counter.errorFiles["abc/def.cfg"]);
            logger.AssertError("Maybe someone will read this one");
        }

        [Fact]
        public void TestException()
        {
            Exception e1 = new Exception();
            Exception e2 = new Exception();

            Assert.Equal(0, progress.Counter.exceptions);

            progress.Exception("An exception was thrown", e1);
            Assert.Equal(1, progress.Counter.exceptions);
            logger.AssertException("An exception was thrown", e1);

            progress.Exception("An exception was tossed", e2);
            Assert.Equal(2, progress.Counter.exceptions);
            logger.AssertException("An exception was tossed", e2);
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
            logger.AssertException("An exception was thrown", e1);

            progress.Exception(config2, "An exception was tossed", e2);
            Assert.Equal(2, progress.Counter.exceptions);
            Assert.Equal(2, progress.Counter.errorFiles["abc/def.cfg"]);
            logger.AssertException("An exception was tossed", e2);
        }

        [Fact]
        public void TestProgressFraction()
        {
            Assert.Equal(0, progress.ProgressFraction);

            progress.Counter.needsUnsatisfied.Increment();
            progress.Counter.needsUnsatisfied.Increment();

            progress.Counter.totalPatches.Increment();
            progress.Counter.totalPatches.Increment();
            progress.Counter.totalPatches.Increment();
            progress.Counter.totalPatches.Increment();

            Assert.Equal(0, progress.ProgressFraction);

            progress.Counter.appliedPatches.Increment();

            Assert.Equal(0.25, progress.ProgressFraction);

            progress.Counter.appliedPatches.Increment();

            Assert.Equal(0.5, progress.ProgressFraction);

            progress.Counter.appliedPatches.Increment();

            Assert.Equal(0.75, progress.ProgressFraction);

            progress.Counter.appliedPatches.Increment();

            Assert.Equal(1, progress.ProgressFraction);
        }
    }
}
