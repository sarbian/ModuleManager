using System;
using Xunit;
using NSubstitute;
using ModuleManager.Extensions;
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
            logger.Info("well hi there");

            innerLogger.AssertInfo("[MyMod] well hi there");
        }

        [Fact]
        public void TestLog__Warning()
        {
            logger.Warning("I'm warning you");

            innerLogger.AssertWarning("[MyMod] I'm warning you");
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
