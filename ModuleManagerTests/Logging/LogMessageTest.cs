using System;
using Xunit;
using UnityEngine;
using ModuleManager.Logging;

namespace ModuleManagerTests.Logging
{
    public class LogMessageTest
    {
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
        public void TestToLogMessage__Info()
        {
            LogMessage message = new LogMessage(LogType.Log, "everything is ok");
            Assert.Matches(@"^\[LOG \d\d\d\d-\d\d-\d\d \d\d:\d\d:\d\d.\d\d\d\] everything is ok$", message.ToLogString());
        }

        [Fact]
        public void TestToLogMessage__Warning()
        {
            LogMessage message = new LogMessage(LogType.Warning, "I'm warning you");
            Assert.Matches(@"^\[WRN \d\d\d\d-\d\d-\d\d \d\d:\d\d:\d\d.\d\d\d\] I'm warning you$", message.ToLogString());
        }

        [Fact]
        public void TestToLogMessage__Error()
        {
            LogMessage message = new LogMessage(LogType.Error, "You went too far");
            Assert.Matches(@"^\[ERR \d\d\d\d-\d\d-\d\d \d\d:\d\d:\d\d.\d\d\d\] You went too far$", message.ToLogString());
        }

        [Fact]
        public void TestToLogMessage__Exception()
        {
            LogMessage message = new LogMessage(LogType.Exception, "You went too far");
            Assert.Matches(@"^\[EXC \d\d\d\d-\d\d-\d\d \d\d:\d\d:\d\d.\d\d\d\] You went too far$", message.ToLogString());
        }

        [Fact]
        public void TestToLogMessage__Assert()
        {
            LogMessage message = new LogMessage(LogType.Assert, "You went too far");
            Assert.Matches(@"^\[AST \d\d\d\d-\d\d-\d\d \d\d:\d\d:\d\d.\d\d\d\] You went too far$", message.ToLogString());
        }

        [Fact]
        public void TestToLogMessage__Unknown()
        {
            LogMessage message = new LogMessage((LogType)9999, "You went too far");
            Assert.Matches(@"^\[\?\?\? \d\d\d\d-\d\d-\d\d \d\d:\d\d:\d\d.\d\d\d\] You went too far$", message.ToLogString());
        }

        [Fact]
        public void TestToString()
        {
            LogMessage message = new LogMessage(LogType.Log, "everything is ok");
            Assert.Equal("[ModuleManager.Logging.LogMessage LogType=Log Message=everything is ok]", message.ToString());
        }
    }
}
