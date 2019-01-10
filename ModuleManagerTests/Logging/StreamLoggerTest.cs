using System;
using System.IO;
using UnityEngine;
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
                    streamLogger.Log(LogType.Log, "a message");
                });

                Assert.Contains("Object has already been disposed", ex.Message);
            }
        }

        [Fact]
        public void TestLog__Log()
        {
            byte[] bytes = new byte[50];
            using (MemoryStream stream = new MemoryStream(bytes, true))
            {
                using (StreamLogger streamLogger = new StreamLogger(stream))
                {
                    streamLogger.Log(LogType.Log, "a message");
                }
            }

            using (MemoryStream stream = new MemoryStream(bytes, false))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    string result = reader.ReadToEnd();
                    Assert.Contains("[LOG ", result);
                    Assert.Contains("] a message", result);
                }
            }
        }

        [Fact]
        public void TestLog__Assert()
        {
            byte[] bytes = new byte[50];
            using (MemoryStream stream = new MemoryStream(bytes, true))
            {
                using (StreamLogger streamLogger = new StreamLogger(stream))
                {
                    streamLogger.Log(LogType.Assert, "a message");
                }
            }

            using (MemoryStream stream = new MemoryStream(bytes, false))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    string result = reader.ReadToEnd();
                    Assert.Contains("[AST ", result);
                    Assert.Contains("] a message", result);
                }
            }
        }

        [Fact]
        public void TestLog__Warning()
        {
            byte[] bytes = new byte[50];
            using (MemoryStream stream = new MemoryStream(bytes, true))
            {
                using (StreamLogger streamLogger = new StreamLogger(stream))
                {
                    streamLogger.Log(LogType.Warning, "a message");
                }
            }

            using (MemoryStream stream = new MemoryStream(bytes, false))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    string result = reader.ReadToEnd();
                    Assert.Contains("[WRN ", result);
                    Assert.Contains("] a message", result);
                }
            }
        }

        [Fact]
        public void TestLog__Error()
        {
            byte[] bytes = new byte[50];
            using (MemoryStream stream = new MemoryStream(bytes, true))
            {
                using (StreamLogger streamLogger = new StreamLogger(stream))
                {
                    streamLogger.Log(LogType.Error, "a message");
                }
            }

            using (MemoryStream stream = new MemoryStream(bytes, false))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    string result = reader.ReadToEnd();
                    Assert.Contains("[ERR ", result);
                    Assert.Contains("] a message", result);
                }
            }
        }

        [Fact]
        public void TestLog__Exception()
        {
            byte[] bytes = new byte[50];
            using (MemoryStream stream = new MemoryStream(bytes, true))
            {
                using (StreamLogger streamLogger = new StreamLogger(stream))
                {
                    streamLogger.Log(LogType.Exception, "a message");
                }
            }

            using (MemoryStream stream = new MemoryStream(bytes, false))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    string result = reader.ReadToEnd();
                    Assert.Contains("[EXC ", result);
                    Assert.Contains("] a message", result);
                }
            }
        }

        [Fact]
        public void TestLog__Unknown()
        {
            byte[] bytes = new byte[50];
            using (MemoryStream stream = new MemoryStream(bytes, true))
            {
                using (StreamLogger streamLogger = new StreamLogger(stream))
                {
                    streamLogger.Log((LogType)1000, "a message");
                }
            }

            using (MemoryStream stream = new MemoryStream(bytes, false))
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    string result = reader.ReadToEnd();
                    Assert.Contains("[UNK ", result);
                    Assert.Contains("] a message", result);
                }
            }
        }
    }
}
