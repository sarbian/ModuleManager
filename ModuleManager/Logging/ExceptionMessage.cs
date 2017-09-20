using System;

namespace ModuleManager.Logging
{
    public class ExceptionMessage : ILogMessage
    {
        public readonly string message;
        public readonly Exception exception;

        public ExceptionMessage(string message, Exception exception)
        {
            this.message = message;
            this.exception = exception;
        }

        public void LogTo(IBasicLogger logger)
        {
            logger.Exception(message, exception);
        }
    }
}
