using System;
using System.IO;
using UnityEngine;

namespace ModuleManager.Logging
{
    public class StreamLogger : IBasicLogger, IDisposable
    {
        private readonly Stream stream;
        private readonly StreamWriter streamWriter;
        private readonly IBasicLogger exceptionLogger;
        private bool disposed = false;

        public StreamLogger(Stream stream, IBasicLogger exceptionLogger)
        {
            this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
            if (!stream.CanWrite) throw new ArgumentException("must be writable", nameof(stream));
            streamWriter = new StreamWriter(stream);
            this.exceptionLogger = exceptionLogger ?? throw new ArgumentNullException(nameof(exceptionLogger));
        }

        public void Log(LogType logType, string message)
        {
            if (disposed) throw new InvalidOperationException("Object has already been disposed");
            try
            {
                string prefix;
                if (logType == LogType.Log)
                    prefix = "LOG";
                else if (logType == LogType.Warning)
                    prefix = "WRN";
                else if (logType == LogType.Error)
                    prefix = "ERR";
                else if (logType == LogType.Assert)
                    prefix = "AST";
                else if (logType == LogType.Exception)
                    prefix = "EXC";
                else
                    prefix = "UNK";

                streamWriter.WriteLine("[{0} {1}] {2}", prefix, DateTime.Now.ToString(), message);
            }
            catch (Exception e)
            {
                exceptionLogger.Exception("Exception while attempting to log to stream", e);
            }
        }

        public void Exception(string message, Exception exception)
        {
            Log(LogType.Exception, exception?.ToString() ?? "<null exception>");
        }

        public void Dispose()
        {
            try
            {
                // Flushes and closes the StreamWriter and the underlying stream
                streamWriter.Close();
            }
            catch(Exception e)
            {
                exceptionLogger.Exception("Exception while attempting to close stream writer", e);
            }

            disposed = true;
        }
    }
}
