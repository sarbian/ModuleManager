using System;

namespace ModuleManager.Logging
{
    public class LogSplitter : IBasicLogger
    {
        private readonly IBasicLogger logger1;
        private readonly IBasicLogger logger2;

        public LogSplitter(IBasicLogger logger1, IBasicLogger logger2)
        {
            this.logger1 = logger1 ?? throw new ArgumentNullException(nameof(logger1));
            this.logger2 = logger2 ?? throw new ArgumentNullException(nameof(logger2));
        }

        public void Log(ILogMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            logger1.Log(message);
            logger2.Log(message);
        }
    }
}
