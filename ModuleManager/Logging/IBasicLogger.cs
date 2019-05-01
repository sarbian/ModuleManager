using System;

namespace ModuleManager.Logging
{
    // Stripped down version of UnityEngine.ILogger
    public interface IBasicLogger
    {
        void Log(ILogMessage message);
    }
}
