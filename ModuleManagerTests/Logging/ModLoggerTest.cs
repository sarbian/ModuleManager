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
        public void TestLog__Info()
        {
            logger.Log(LogType.Log, "well hi there");

            innerLogger.Received().Log(LogType.Log, "[MyMod] well hi there");
        }

        [Fact]
        public void TestLog__Warning()
        {
            logger.Log(LogType.Warning, "I'm warning you");

            innerLogger.Received().Log(LogType.Warning, "[MyMod] I'm warning you");
        }

        [Fact]
        public void TestLog__Error()
        {
            logger.Log(LogType.Error, "You have made a grave mistake");

            innerLogger.Received().Log(LogType.Error, "[MyMod] You have made a grave mistake");
        }

        [Fact]
        public void TestException()
        {
            Exception e = new Exception();
            logger.Exception("An exception was thrown", e);
            
            innerLogger.Received().Exception("[MyMod] An exception was thrown", e);
        }
    }
}
