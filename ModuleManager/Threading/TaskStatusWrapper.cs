using System;

namespace ModuleManager.Threading
{
    public class TaskStatusWrapper : ITaskStatus
    {
        private ITaskStatus inner;

        public TaskStatusWrapper(ITaskStatus inner)
        {
            this.inner = inner;
        }

        public bool IsRunning => inner.IsRunning;
        public bool IsFinished => inner.IsFinished;
        public bool IsExitedWithError => inner.IsExitedWithError;
        public Exception Exception => inner.Exception;
    }
}
