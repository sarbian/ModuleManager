using System;
using Xunit;
using NSubstitute;
using UnityEngine;
using ModuleManager.Logging;

namespace ModuleManagerTests.Logging
{
    public class UnityLoggerTest
    {
        private ILogger innerLogger;
        private UnityLogger logger;

        public UnityLoggerTest()
        {
            innerLogger = Substitute.For<ILogger>();
            logger = new UnityLogger(innerLogger);
        }

        [Fact]
        public void TestConstructor__LoggerNull()
        {
            ArgumentNullException e = Assert.Throws<ArgumentNullException>(delegate
            {
                new UnityLogger(null);
            });

            Assert.Equal("logger", e.ParamName);
        }

        [Fact]
        public void TestLog__Info()
        {
            logger.Log(LogType.Log, "well hi there");

            innerLogger.Received().Log(LogType.Log, "well hi there");
        }

        [Fact]
        public void TestLog__Warning()
        {
            logger.Log(LogType.Warning, "I'm warning you");

            innerLogger.Received().Log(LogType.Warning, "I'm warning you");
        }

        [Fact]
        public void TestLog__Error()
        {
            logger.Log(LogType.Error, "You have made a grave mistake");

            innerLogger.Received().Log(LogType.Error, "You have made a grave mistake");
        }

        [Fact]
        public void TestException()
        {
            Exception e = new Exception();
            logger.Exception("An exception was thrown", e);

            innerLogger.Received().Log(LogType.Error, "An exception was thrown");
            innerLogger.Received().LogException(e);
        }
    }
}
