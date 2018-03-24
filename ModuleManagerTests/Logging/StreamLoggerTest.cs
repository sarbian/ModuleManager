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
                new StreamLogger(null, Substitute.For<IBasicLogger>());
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
                    new StreamLogger(stream, Substitute.For<IBasicLogger>());
                });

                Assert.Equal("stream", ex.ParamName);
                Assert.Contains("must be writable", ex.Message);
            }
        }

        [Fact]
        public void TestConstructor__ExceptionLoggerNull()
        {
            using (MemoryStream stream = new MemoryStream(new byte[0], true))
            {
                ArgumentNullException ex = Assert.Throws<ArgumentNullException>(delegate
                {
                    new StreamLogger(stream, null);
                });

                Assert.Equal("exceptionLogger", ex.ParamName);
            }
        }

        [Fact]
        public void TestLog__AlreadyDisposed()
        {
            using (MemoryStream stream = new MemoryStream(new byte[0], true))
            {
                StreamLogger streamLogger = new StreamLogger(stream, Substitute.For<IBasicLogger>());
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
            IBasicLogger exceptionLogger = Substitute.For<IBasicLogger>();
            byte[] bytes = new byte[50];
            using (MemoryStream stream = new MemoryStream(bytes, true))
            {
                using (StreamLogger streamLogger = new StreamLogger(stream, exceptionLogger))
                {
                    streamLogger.Log(LogType.Log, "a message");
                }
            }

            exceptionLogger.DidNotReceiveWithAnyArgs().Exception(null, null);

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
            IBasicLogger exceptionLogger = Substitute.For<IBasicLogger>();
            byte[] bytes = new byte[50];
            using (MemoryStream stream = new MemoryStream(bytes, true))
            {
                using (StreamLogger streamLogger = new StreamLogger(stream, exceptionLogger))
                {
                    streamLogger.Log(LogType.Assert, "a message");
                }
            }

            exceptionLogger.DidNotReceiveWithAnyArgs().Exception(null, null);

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
            IBasicLogger exceptionLogger = Substitute.For<IBasicLogger>();
            byte[] bytes = new byte[50];
            using (MemoryStream stream = new MemoryStream(bytes, true))
            {
                using (StreamLogger streamLogger = new StreamLogger(stream, exceptionLogger))
                {
                    streamLogger.Log(LogType.Warning, "a message");
                }
            }

            exceptionLogger.DidNotReceiveWithAnyArgs().Exception(null, null);

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
            IBasicLogger exceptionLogger = Substitute.For<IBasicLogger>();
            byte[] bytes = new byte[50];
            using (MemoryStream stream = new MemoryStream(bytes, true))
            {
                using (StreamLogger streamLogger = new StreamLogger(stream, exceptionLogger))
                {
                    streamLogger.Log(LogType.Error, "a message");
                }
            }

            exceptionLogger.DidNotReceiveWithAnyArgs().Exception(null, null);

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
            IBasicLogger exceptionLogger = Substitute.For<IBasicLogger>();
            byte[] bytes = new byte[50];
            using (MemoryStream stream = new MemoryStream(bytes, true))
            {
                using (StreamLogger streamLogger = new StreamLogger(stream, exceptionLogger))
                {
                    streamLogger.Log(LogType.Exception, "a message");
                }
            }

            exceptionLogger.DidNotReceiveWithAnyArgs().Exception(null, null);

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
            IBasicLogger exceptionLogger = Substitute.For<IBasicLogger>();
            byte[] bytes = new byte[50];
            using (MemoryStream stream = new MemoryStream(bytes, true))
            {
                using (StreamLogger streamLogger = new StreamLogger(stream, exceptionLogger))
                {
                    streamLogger.Log((LogType)1000, "a message");
                }
            }

            exceptionLogger.DidNotReceiveWithAnyArgs().Exception(null, null);

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
