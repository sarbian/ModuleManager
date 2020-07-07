using System;
using Xunit;
using NSubstitute;
using UnityEngine;
using ModuleManager.Extensions;
using ModuleManager.Logging;

namespace ModuleManagerTests.Logging
{
    public class UnityLoggerTest
    {
        private readonly ILogger innerLogger = Substitute.For<ILogger>();
        private readonly UnityLogger logger;

        public UnityLoggerTest()
        {
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
            logger.Info("well hi there");

            innerLogger.Received().Log(LogType.Log, "well hi there");
        }

        [Fact]
        public void TestLog__Warning()
        {
            logger.Warning("I'm warning you");

            innerLogger.Received().Log(LogType.Warning, "I'm warning you");
        }

        [Fact]
        public void TestLog__MessageNull()
        {
            ArgumentNullException e = Assert.Throws<ArgumentNullException>(delegate
            {
                logger.Log(null);
            });

            Assert.Equal("message", e.ParamName);
        }
    }
}
