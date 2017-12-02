using System;

namespace ModuleManager.Logging
{
    public interface ILogMessage
    {
        void LogTo(IBasicLogger logger);
    }
}
