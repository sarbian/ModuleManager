using System;
using Xunit;
using NSubstitute;
using UnityEngine;
using ModuleManager.Logging;

namespace ModuleManagerTests.Logging
{
    public class LogSplitterTest
    {
        [Fact]
        public void TestConstructor__Logger1Null()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new LogSplitter(null, Substitute.For<IBasicLogger>());
            });

            Assert.Equal("logger1", ex.ParamName);
        }

        [Fact]
        public void TestConstructor__Logger2Null()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new LogSplitter(Substitute.For<IBasicLogger>(), null);
            });

            Assert.Equal("logger2", ex.ParamName);
        }

        [Fact]
        public void TestLog()
        {
            IBasicLogger logger1 = Substitute.For<IBasicLogger>();
            IBasicLogger logger2 = Substitute.For<IBasicLogger>();
            LogSplitter logSplitter = new LogSplitter(logger1, logger2);
            ILogMessage message = Substitute.For<ILogMessage>();
            logSplitter.Log(message);
            logger1.Received().Log(message);
            logger2.Received().Log(message);
        }

        [Fact]
        public void TestLog__MessageNull()
        {
            LogSplitter logSplitter = new LogSplitter(Substitute.For<IBasicLogger>(), Substitute.For<IBasicLogger>());
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                logSplitter.Log(null);
            });

            Assert.Equal("message", ex.ParamName);
        }
    }
}
