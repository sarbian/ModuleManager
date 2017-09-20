using System;
using Xunit;
using NSubstitute;
using UnityEngine;
using ModuleManager.Logging;

namespace ModuleManagerTests.Logging
{
    public class NormalMessageTest
    {
        private IBasicLogger logger = Substitute.For<IBasicLogger>();

        [Fact]
        public void TestLogTo__Info()
        {
            NormalMessage message = new NormalMessage(LogType.Log, "everything is ok");
            message.LogTo(logger);
            logger.Received().Log(LogType.Log, "everything is ok");
        }

        [Fact]
        public void TestLogTo__Warning()
        {
            NormalMessage message = new NormalMessage(LogType.Warning, "I'm warning you");
            message.LogTo(logger);
            logger.Received().Log(LogType.Warning, "I'm warning you");
        }

        [Fact]
        public void TestLogTo__Error()
        {
            NormalMessage message = new NormalMessage(LogType.Error, "You went too far");
            message.LogTo(logger);
            logger.Received().Log(LogType.Error, "You went too far");
        }
    }
}
