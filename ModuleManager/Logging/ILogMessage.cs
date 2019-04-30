using System;
using UnityEngine;

namespace ModuleManager.Logging
{
    public interface ILogMessage
    {
        LogType LogType { get; }
        DateTime Timestamp { get; }
        string Message { get; }
        string ToLogString();
    }
}
