using System;
using Xunit;
using NSubstitute;
using ModuleManager.Logging;

namespace ModuleManagerTests.Logging
{
    public class ExceptionMessageTest
    {
        [Fact]
        public void TestLogTo()
        {
            IBasicLogger logger = Substitute.For<IBasicLogger>();

            Exception e = new Exception();
            ExceptionMessage message = new ExceptionMessage("An exception was thrown", e);
            message.LogTo(logger);

            logger.Received().Exception("An exception was thrown", e);
        }
    }
}
