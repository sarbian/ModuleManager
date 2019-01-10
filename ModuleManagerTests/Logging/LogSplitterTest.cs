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
            logSplitter.Log(LogType.Log, "some stuff");
            logger1.Received().Log(LogType.Log, "some stuff");
            logger2.Received().Log(LogType.Log, "some stuff");
        }

        [Fact]
        public void TestException()
        {
            IBasicLogger logger1 = Substitute.For<IBasicLogger>();
            IBasicLogger logger2 = Substitute.For<IBasicLogger>();
            LogSplitter logSplitter = new LogSplitter(logger1, logger2);
            Exception ex = new Exception();
            logSplitter.Exception("some stuff", ex);
            logger1.Received().Exception("some stuff", ex);
            logger2.Received().Exception("some stuff", ex);
        }
    }
}
