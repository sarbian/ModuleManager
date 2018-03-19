using System;
using UnityEngine;

namespace ModuleManager.Logging
{
    // Stripped down version of UnityEngine.ILogger
    public interface IBasicLogger
    {
        void Log(LogType logType, string message);
        void Exception(string message, Exception exception);
    }
}
