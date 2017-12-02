using System;

namespace ModuleManager.Threading
{
    public interface ITaskStatus
    {
        bool IsRunning { get; }
        bool IsFinished { get; }
        bool IsExitedWithError { get; }
        Exception Exception { get; }
    }
}
