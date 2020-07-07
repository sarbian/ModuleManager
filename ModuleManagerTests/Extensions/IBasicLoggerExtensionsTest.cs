using System;
using Xunit;
using NSubstitute;
using ModuleManager.Logging;
using ModuleManager.Extensions;

namespace ModuleManagerTests.Extensions
{
    public class IBasicLoggerExtensionsTest
    {
        private readonly IBasicLogger logger = Substitute.For<IBasicLogger>();

        [Fact]
        public void TestInfo()
        {
            logger.Info("well hi there");
            logger.AssertInfo("well hi there");
        }

        [Fact]
        public void TestWarning()
        {
            logger.Warning("I'm warning you");
            logger.AssertWarning("I'm warning you");
        }

        [Fact]
        public void TestError()
        {
            logger.Error("You have made a grave mistake");
            logger.AssertError("You have made a grave mistake");
        }

        [Fact]
        public void TestException()
        {
            Exception ex = new Exception();
            logger.Exception(ex);
            logger.AssertException(ex);
        }

        [Fact]
        public void TestException__Null()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                logger.Exception(null);
            });

            Assert.Equal("exception", ex.ParamName);
        }

        [Fact]
        public void TestException__Message()
        {
            Exception ex = new Exception();
            logger.Exception("a message", ex);
            logger.AssertException("a message", ex);
        }

        [Fact]
        public void TestException__Message__MessageNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                logger.Exception(null, new Exception());
            });

            Assert.Equal("message", ex.ParamName);
        }

        [Fact]
        public void TestException__Message__ExceptionNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                logger.Exception("a message", null);
            });

            Assert.Equal("exception", ex.ParamName);
        }
    }
}
