using System;
using Xunit;
using NSubstitute;
using UnityEngine;
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
        public void TestLog__Info()
        {
            logger.Log(LogType.Log, "useful information");
            queue.Received().Add(Arg.Is<NormalMessage>(m => m.logType == LogType.Log && m.message == "useful information"));
        }

        [Fact]
        public void TestLog__Warning()
        {
            logger.Log(LogType.Warning, "not to alarm you, but something might be wrong");
            queue.Received().Add(Arg.Is<NormalMessage>(m => m.logType == LogType.Warning && m.message == "not to alarm you, but something might be wrong"));
        }

        [Fact]
        public void TestLog__Error()
        {
            logger.Log(LogType.Error, "you broke everything");
            queue.Received().Add(Arg.Is<NormalMessage>(m => m.logType == LogType.Error && m.message == "you broke everything"));
        }


        [Fact]
        public void TestException()
        {
            Exception e = new Exception();
            logger.Exception("An exception was thrown", e);
            queue.Received().Add(Arg.Is<ExceptionMessage>(m => m.message == "An exception was thrown" && m.exception == e));
        }
    }
}
