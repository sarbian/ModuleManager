using System;
using Xunit;
using NSubstitute;
using UnityEngine;
using ModuleManager.Logging;
using ModuleManager.Extensions;

namespace ModuleManagerTests.Extensions
{
    public class IBasicLoggerExtensionsTest
    {
        private IBasicLogger logger;

        public IBasicLoggerExtensionsTest()
        {
            logger = Substitute.For<IBasicLogger>();
        }

        [Fact]
        public void TestInfo()
        {
            logger.Info("well hi there");
            logger.Received().Log(LogType.Log, "well hi there");
        }

        [Fact]
        public void TestWarning()
        {
            logger.Warning("I'm warning you");
            logger.Received().Log(LogType.Warning, "I'm warning you");
        }

        [Fact]
        public void TestError()
        {
            logger.Error("You have made a grave mistake");
            logger.Received().Log(LogType.Error, "You have made a grave mistake");
        }
    }
}
