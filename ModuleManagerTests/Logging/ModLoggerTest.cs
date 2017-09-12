using System;
using Xunit;
using NSubstitute;
using UnityEngine;
using ModuleManager.Logging;

namespace ModuleManagerTests.Logging
{
    public class ModLoggerTest
    {
        private ILogger innerLogger;
        private ModLogger logger;

        public ModLoggerTest()
        {
            innerLogger = Substitute.For<ILogger>();
            logger = new ModLogger("MyMod", innerLogger);
        }
        [Fact]
        public void TestLog()
        {
            logger.Log(LogType.Log, "this is a log message");
            logger.Log(LogType.Error, "this is another log message");

            innerLogger.Received().Log(LogType.Log, "[MyMod] this is a log message");
            innerLogger.Received().Log(LogType.Error, "[MyMod] this is another log message");
        }

        [Fact]
        public void TestInfo()
        {
            logger.Info("well hi there");

            innerLogger.Received().Log(LogType.Log, "[MyMod] well hi there");
        }

        [Fact]
        public void TestWarning()
        {
            logger.Warning("I'm warning you");

            innerLogger.Received().Log(LogType.Warning, "[MyMod] I'm warning you");
        }

        [Fact]
        public void TestError()
        {
            logger.Error("You have made a grave mistake");

            innerLogger.Received().Log(LogType.Error, "[MyMod] You have made a grave mistake");
        }

        [Fact]
        public void TestException()
        {
            Exception e = new Exception();
            logger.Exception("An exception was thrown", e);

            innerLogger.Received().Log(LogType.Error, "[MyMod] An exception was thrown");
            innerLogger.Received().LogException(e);
        }
    }
}
