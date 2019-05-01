using System;
using Xunit;
using NSubstitute;
using UnityEngine;
using ModuleManager.Logging;

namespace ModuleManagerTests.Logging
{
    public class PrefixLoggerTest
    {
        private IBasicLogger innerLogger;
        private PrefixLogger logger;

        public PrefixLoggerTest()
        {
            innerLogger = Substitute.For<IBasicLogger>();
            logger = new PrefixLogger("MyMod", innerLogger);
        }

        [Fact]
        public void TestConstructor__PrefixNull()
        {
            ArgumentNullException e = Assert.Throws<ArgumentNullException>(delegate
            {
                new PrefixLogger(null, innerLogger);
            });

            Assert.Equal("prefix", e.ParamName);
        }

        [Fact]
        public void TestConstructor__PrefixBlank()
        {
            ArgumentNullException e = Assert.Throws<ArgumentNullException>(delegate
            {
                new PrefixLogger("", innerLogger);
            });

            Assert.Equal("prefix", e.ParamName);
        }

        [Fact]
        public void TestConstructor__LoggerNull()
        {
            ArgumentNullException e = Assert.Throws<ArgumentNullException>(delegate
            {
                new PrefixLogger("blah", null);
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
