using System;
using Xunit;
using NSubstitute;
using ModuleManager.Collections;
using ModuleManager.Logging;

namespace ModuleManagerTests.Logging
{
    public class QueueLoggerTest
    {
        private IMessageQueue<ILogMessage> queue;
        private QueueLogger logger;

        public QueueLoggerTest()
        {
            queue = Substitute.For<IMessageQueue<ILogMessage>>();
            logger = new QueueLogger(queue);
        }

        [Fact]
        public void TestConstructor__QueueNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new QueueLogger(null);
            });

            Assert.Equal("queue", ex.ParamName);
        }

        [Fact]
        public void TestLog()
        {
            ILogMessage message = Substitute.For<ILogMessage>();
            logger.Log(message);
            queue.Received().Add(message);
        }

        [Fact]
        public void TestLog__MessageNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                logger.Log(null);
            });

            Assert.Equal("message", ex.ParamName);
        }
        
    }
}
