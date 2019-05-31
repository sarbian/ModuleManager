using System;
using System.IO;
using Xunit;
using NSubstitute;
using ModuleManager.Logging;

namespace ModuleManagerTests.Logging
{
    public class StreamLoggerTest
    {
        [Fact]
        public void TestConstructor__StreamNull()
        {
            ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
            {
                new StreamLogger(null);
            });

            Assert.Equal("stream", ex.ParamName);
        }

        [Fact]
        public void TestConstructor__CantWrite()
        {
            using (MemoryStream stream = new MemoryStream(new byte[0], false))
            {
                ArgumentException ex = Assert.Throws<ArgumentException>(delegate
                {
                    new StreamLogger(stream);
                });

                Assert.Equal("stream", ex.ParamName);
                Assert.Contains("must be writable", ex.Message);
            }
        }

        [Fact]
        public void TestLog__AlreadyDisposed()
        {
            using (MemoryStream stream = new MemoryStream(new byte[0], true))
            {
                StreamLogger streamLogger = new StreamLogger(stream);
                streamLogger.Dispose();

                InvalidOperationException ex = Assert.Throws<InvalidOperationException>(delegate
                {
                    streamLogger.Log(Substitute.For<ILogMessage>());
                });

                Assert.Contains("Object has already been disposed", ex.Message);
            }
        }

        [Fact]
        public void TestLog()
        {
            ILogMessage message = Substitute.For<ILogMessage>();
            message.ToLogString().Returns("[OMG wtf] bbq");
            byte[] bytes = new byte[15];
            using (MemoryStream stream = new MemoryStream(bytes, true))
            {
                using (StreamLogger streamLogger = new StreamLogger(stream))
                {
                    streamLogger.Log(message);
                }
            }

            using (MemoryStream stream = new MemoryStream(bytes, false))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    string result = reader.ReadToEnd().Trim();
                    Assert.Equal("[OMG wtf] bbq", result);
                }
            }
        }

        [Fact]
        public void TestLog__MessageNull()
        {
            using (MemoryStream stream = new MemoryStream(new byte[0], true))
            {
                StreamLogger streamLogger = new StreamLogger(stream);

                ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
                {
                    streamLogger.Log(null);
                });

                Assert.Equal("message", ex.ParamName);
            }
        }
    }
}
