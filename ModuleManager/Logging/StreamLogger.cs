using System;
using System.IO;

namespace ModuleManager.Logging
{
    public class StreamLogger : IBasicLogger, IDisposable
    {
        private readonly Stream stream;
        private readonly StreamWriter streamWriter;
        private bool disposed = false;

        public StreamLogger(Stream stream)
        {
            this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
            if (!stream.CanWrite) throw new ArgumentException("must be writable", nameof(stream));
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
