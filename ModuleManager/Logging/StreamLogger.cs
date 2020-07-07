using System;
using System.IO;

namespace ModuleManager.Logging
{
    public sealed class StreamLogger : IBasicLogger, IDisposable
    {
        private readonly StreamWriter streamWriter;
        private bool disposed = false;

        public StreamLogger(Stream stream)
        {
            streamWriter = new StreamWriter(stream);
        }

        public void Log(ILogMessage message)
        {
            if (disposed) throw new InvalidOperationException("Object has already been disposed");
            if (message == null) throw new ArgumentNullException(nameof(message));

            streamWriter.WriteLine(message.ToLogString());
        }

        public void Dispose()
        {
            // Flushes and closes the StreamWriter and the underlying stream
            streamWriter.Close();

            disposed = true;
        }
    }
}
