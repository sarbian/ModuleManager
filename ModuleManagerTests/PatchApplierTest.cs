using System;
using Xunit;
using NSubstitute;
using UnityEngine;
using TestUtils;
using ModuleManager;
using ModuleManager.Collections;
using ModuleManager.Logging;
using ModuleManager.Patches;
using ModuleManager.Progress;

namespace ModuleManagerTests
{
    public class PatchApplierTest
    {
        [Fact]
        public void TestConstructor__ProgressNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new PatchApplier(null, Substitute.For<IBasicLogger>());
            });

            Assert.Equal("progress", ex.ParamName);
        }

        [Fact]
        public void TestConstructor__LoggerNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new PatchApplier(Substitute.For<IPatchProgress>(), null);
            });

            Assert.Equal("logger", ex.ParamName);
        }

        [Fact]
        public void TestApplyPatches__UrlFilesNull()
        {
            PatchApplier applier = new PatchApplier(Substitute.For<IPatchProgress>(), Substitute.For<IBasicLogger>());
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                applier.ApplyPatches(null, new IPass[0]);
            });

            Assert.Equal("configFiles", ex.ParamName);
        }

        [Fact]
        public void TestApplyPatches__PatchesNull()
        {
            PatchApplier applier = new PatchApplier(Substitute.For<IPatchProgress>(), Substitute.For<IBasicLogger>());
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                applier.ApplyPatches(new UrlDir.UrlFile[0], null);
            });

            Assert.Equal("patches", ex.ParamName);
        }

        [Fact]
        public void TestApplyPatches()
        {
            IBasicLogger logger = Substitute.For<IBasicLogger>();
            IPatchProgress progress = Substitute.For<IPatchProgress>();
            PatchApplier patchApplier = new PatchApplier(progress, logger);
            UrlDir.UrlFile file1 = UrlBuilder.CreateFile("abc/def.cfg");
            UrlDir.UrlFile file2 = UrlBuilder.CreateFile("ghi/jkl.cfg");
            IPass pass1 = Substitute.For<IPass>();
            IPass pass2 = Substitute.For<IPass>();
            IPass pass3 = Substitute.For<IPass>();
            pass1.Name.Returns(":PASS1");
            pass2.Name.Returns(":PASS2");
            pass3.Name.Returns(":PASS3");

            UrlDir.UrlConfig[] patchUrlConfigs = new UrlDir.UrlConfig[9];
            IPatch[] patches = new IPatch[9];
            for (int i = 0; i < patches.Length; i++)
            {
                patches[i] = Substitute.For<IPatch>();
            }

            pass1.GetEnumerator().Returns(new ArrayEnumerator<IPatch>(patches[0], patches[1], patches[2]));
            pass2.GetEnumerator().Returns(new ArrayEnumerator<IPatch>(patches[3], patches[4], patches[5]));
            pass3.GetEnumerator().Returns(new ArrayEnumerator<IPatch>(patches[6], patches[7], patches[8]));

            IPass[] patchList = new IPass[] { pass1, pass2, pass3 };

            patchApplier.ApplyPatches(new[] { file1, file2 }, new[] { pass1, pass2, pass3 });

            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);

            logger.DidNotReceive().Log(LogType.Warning, Arg.Any<string>());
            logger.DidNotReceive().Log(LogType.Error, Arg.Any<string>());
            logger.DidNotReceiveWithAnyArgs().Exception(null, null);

            Received.InOrder(delegate
            {
                logger.Log(LogType.Log, ":PASS1 pass");
                patches[0].Apply(file1, progress, logger);
                patches[0].Apply(file2, progress, logger);
                progress.PatchApplied();
                patches[1].Apply(file1, progress, logger);
                patches[1].Apply(file2, progress, logger);
                progress.PatchApplied();
                patches[2].Apply(file1, progress, logger);
                patches[2].Apply(file2, progress, logger);
                progress.PatchApplied();
                logger.Log(LogType.Log, ":PASS2 pass");
                patches[3].Apply(file1, progress, logger);
                patches[3].Apply(file2, progress, logger);
                progress.PatchApplied();
                patches[4].Apply(file1, progress, logger);
                patches[4].Apply(file2, progress, logger);
                progress.PatchApplied();
                patches[5].Apply(file1, progress, logger);
                patches[5].Apply(file2, progress, logger);
                progress.PatchApplied();
                logger.Log(LogType.Log, ":PASS3 pass");
                patches[6].Apply(file1, progress, logger);
                patches[6].Apply(file2, progress, logger);
                progress.PatchApplied();
                patches[7].Apply(file1, progress, logger);
                patches[7].Apply(file2, progress, logger);
                progress.PatchApplied();
                patches[8].Apply(file1, progress, logger);
                patches[8].Apply(file2, progress, logger);
                progress.PatchApplied();
            });
        }
    }
}
