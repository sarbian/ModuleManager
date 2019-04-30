using System;
using Xunit;
using NSubstitute;
using UnityEngine;
using ModuleManager.Logging;

namespace ModuleManagerTests.Logging
{
    public class ModLoggerTest
    {
        private IBasicLogger innerLogger;
        private ModLogger logger;

        public ModLoggerTest()
        {
            innerLogger = Substitute.For<IBasicLogger>();
            logger = new ModLogger("MyMod", innerLogger);
        }

        [Fact]
        public void TestConstructor__PrefixNull()
        {
            ArgumentNullException e = Assert.Throws<ArgumentNullException>(delegate
            {
                new ModLogger(null, innerLogger);
            });

            Assert.Equal("prefix", e.ParamName);
        }

        [Fact]
        public void TestConstructor__PrefixBlank()
        {
            ArgumentNullException e = Assert.Throws<ArgumentNullException>(delegate
            {
                new ModLogger("", innerLogger);
            });

            Assert.Equal("prefix", e.ParamName);
        }

        [Fact]
        public void TestConstructor__LoggerNull()
        {
            ArgumentNullException e = Assert.Throws<ArgumentNullException>(delegate
            {
                new ModLogger("blah", null);
            });

            Assert.Equal("logger", e.ParamName);
        }

        [Fact]
        public void TestLog()
        {
            ILogMessage logMessage = Substitute.For<ILogMessage>();
            logMessage.LogType.Returns(LogType.Log);
            logMessage.Message.Returns("well hi there");
            logMessage.Timestamp.Returns(new DateTime(2000, 1, 1, 12, 34, 45, 678));

            logger.Log(logMessage);

            innerLogger.Received().Log(Arg.Is<ILogMessage>(msg =>
                msg.LogType == LogType.Log &&
                msg.Timestamp == logMessage.Timestamp &&
                msg.Message == "[MyMod] well hi there"
            ));
        }

        [Fact]
        public void TestLog__Null()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                logger.Log(null);
            });

            Assert.Equal("message", ex.ParamName);
        }
    }
}
