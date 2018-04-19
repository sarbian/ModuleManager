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
        private readonly IBasicLogger logger;
        private readonly IPatchProgress progress;
        private readonly string[] modList = new[] { "mod1", "mod2" };
        private UrlDir databaseRoot;
        private UrlDir.UrlFile file;
        private readonly IPass pass1;
        private readonly IPass pass2;
        private readonly IPass pass3;
        private readonly IPatchList patchList;
        private readonly PatchApplier patchApplier;

        public PatchApplierTest()
        {
            logger = Substitute.For<IBasicLogger>();
            progress = Substitute.For<IPatchProgress>();
            databaseRoot = UrlBuilder.CreateRoot();
            file = UrlBuilder.CreateFile("abc/def.cfg", databaseRoot);
            pass1 = Substitute.For<IPass>();
            pass2 = Substitute.For<IPass>();
            pass3 = Substitute.For<IPass>();
            pass1.Name.Returns(":PASS1");
            pass2.Name.Returns(":PASS2");
            pass3.Name.Returns(":PASS3");
            patchList = Substitute.For<IPatchList>();
            patchList.GetEnumerator().Returns(new ArrayEnumerator<IPass>(pass1, pass2, pass3));
            patchApplier = new PatchApplier(patchList, databaseRoot, progress, logger);
        }

        [Fact]
        public void TestApplyPatches()
        {
            UrlDir.UrlConfig urlConfig = UrlBuilder.CreateConfig(new TestConfigNode("PART")
            {
                { "name", "000" },
                { "aaa", "001" },
            }, file);

            UrlDir.UrlConfig[] patchUrlConfigs = new UrlDir.UrlConfig[9];
            IPatch[] patches = new IPatch[9];

            for (int i = 0; i < 9; i++)
            {
                patchUrlConfigs[i] = UrlBuilder.CreateConfig(new ConfigNode(), file);
                patches[i] = Substitute.For<IPatch>();
                patches[i].UrlConfig.Returns(patchUrlConfigs[i]);
            }

            pass1.GetEnumerator().Returns(new ArrayEnumerator<IPatch>(patches[0], patches[1], patches[2]));
            pass2.GetEnumerator().Returns(new ArrayEnumerator<IPatch>(patches[3], patches[4], patches[5]));
            pass3.GetEnumerator().Returns(new ArrayEnumerator<IPatch>(patches[6], patches[7], patches[8]));

            patchApplier.ApplyPatches();

            progress.DidNotReceiveWithAnyArgs().Error(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null);
            progress.DidNotReceiveWithAnyArgs().Exception(null, null, null);

            logger.DidNotReceive().Log(LogType.Warning, Arg.Any<string>());
            logger.DidNotReceive().Log(LogType.Error, Arg.Any<string>());
            logger.DidNotReceiveWithAnyArgs().Exception(null, null);

            Received.InOrder(delegate
            {
                logger.Log(LogType.Log, ":PASS1 pass");
                patches[0].Apply(file, progress, logger);
                progress.PatchApplied();
                patches[1].Apply(file, progress, logger);
                progress.PatchApplied();
                patches[2].Apply(file, progress, logger);
                progress.PatchApplied();
                logger.Log(LogType.Log, ":PASS2 pass");
                patches[3].Apply(file, progress, logger);
                progress.PatchApplied();
                patches[4].Apply(file, progress, logger);
                progress.PatchApplied();
                patches[5].Apply(file, progress, logger);
                progress.PatchApplied();
                logger.Log(LogType.Log, ":PASS3 pass");
                patches[6].Apply(file, progress, logger);
                progress.PatchApplied();
                patches[7].Apply(file, progress, logger);
                progress.PatchApplied();
                patches[8].Apply(file, progress, logger);
                progress.PatchApplied();
            });
        }
    }
}
