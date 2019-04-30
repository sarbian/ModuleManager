using System;
using Xunit;
using NSubstitute;
using UnityEngine;
using ModuleManager.Logging;

namespace ModuleManagerTests.Logging
{
    public class LogMessageTest
    {
        [Fact]
        public void TestConstructor()
        {
            LogMessage logMessage = new LogMessage(LogType.Log, "a message");
            Assert.Equal(LogType.Log, logMessage.LogType);
            Assert.True(logMessage.Timestamp <= DateTime.Now);
            Assert.True(logMessage.Timestamp > DateTime.Now - new TimeSpan(0, 0, 5));
            Assert.Equal("a message", logMessage.Message);
        }

        [Fact]
        public void TestConstructor__NullMessage()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new LogMessage(LogType.Log, null);
            });

            Assert.Equal("message", ex.ParamName);
        }

        [Fact]
        public void TestConstructor__FromOtherMessage()
        {
            ILogMessage logMessage = Substitute.For<ILogMessage>();
            logMessage.LogType.Returns(LogType.Log);
            logMessage.Message.Returns("the old message");
            logMessage.Timestamp.Returns(new DateTime(2000, 1, 1, 12, 34, 45, 678));
            LogMessage newLogMessage = new LogMessage(logMessage, "a new message");
            Assert.Equal(LogType.Log, newLogMessage.LogType);
            Assert.Equal(logMessage.Timestamp, newLogMessage.Timestamp);
            Assert.Equal("a new message", newLogMessage.Message);
        }

        [Fact]
        public void TestConstructor__FromOtherMessage__LogMessageNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new LogMessage(null, "a new message");
            });

            Assert.Equal("logMessage", ex.ParamName);
        }

        [Fact]
        public void TestConstructor__FromOtherMessage__NewMessageNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new LogMessage(Substitute.For<ILogMessage>(), null);
            });

            Assert.Equal("newMessage", ex.ParamName);
        }

        [Fact]
        public void TestToLogMessage__Info()
        {
            LogMessage message = new LogMessage(LogType.Log, "everything is ok");
            Assert.Matches(@"^\[LOG \d\d:\d\d:\d\d.\d\d\d\] everything is ok$", message.ToLogString());
        }

        [Fact]
        public void TestToLogMessage__Warning()
        {
            LogMessage message = new LogMessage(LogType.Warning, "I'm warning you");
            Assert.Matches(@"^\[WRN \d\d:\d\d:\d\d.\d\d\d\] I'm warning you$", message.ToLogString());
        }

        [Fact]
        public void TestToLogMessage__Error()
        {
            LogMessage message = new LogMessage(LogType.Error, "You went too far");
            Assert.Matches(@"^\[ERR \d\d:\d\d:\d\d.\d\d\d\] You went too far$", message.ToLogString());
        }

        [Fact]
        public void TestToLogMessage__Exception()
        {
            LogMessage message = new LogMessage(LogType.Exception, "You went too far");
            Assert.Matches(@"^\[EXC \d\d:\d\d:\d\d.\d\d\d\] You went too far$", message.ToLogString());
        }

        [Fact]
        public void TestToLogMessage__Assert()
        {
            LogMessage message = new LogMessage(LogType.Assert, "You went too far");
            Assert.Matches(@"^\[AST \d\d:\d\d:\d\d.\d\d\d\] You went too far$", message.ToLogString());
        }

        [Fact]
        public void TestToLogMessage__Unknown()
        {
            LogMessage message = new LogMessage((LogType)9999, "You went too far");
            Assert.Matches(@"^\[\?\?\? \d\d:\d\d:\d\d.\d\d\d\] You went too far$", message.ToLogString());
        }

        [Fact]
        public void TestToString()
        {
            LogMessage message = new LogMessage(LogType.Log, "everything is ok");
            Assert.Equal("[ModuleManager.Logging.LogMessage LogType=Log Message=everything is ok]", message.ToString());
        }

        [Fact]
        public void TestToLogMessage__Timestamp()
        {
            ILogMessage logMessage = Substitute.For<ILogMessage>();
            logMessage.LogType.Returns(LogType.Log);
            logMessage.Timestamp.Returns(new DateTime(2000, 1, 1, 12, 34, 56, 789));
            LogMessage message = new LogMessage(logMessage, "everything is ok");
            Assert.Equal("[LOG 12:34:56.789] everything is ok", message.ToLogString());
        }
    }
}
