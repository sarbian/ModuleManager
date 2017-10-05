using System;
using System.Threading;

namespace ModuleManager.Threading
{
    public static class BackgroundTask
    {
        public static ITaskStatus Start(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            TaskStatus status = new TaskStatus();

            void RunAction()
            {
                try
                {
                    action();
                    status.Finished();
                }
                catch (Exception ex)
                {
                    status.Error(ex);
                }
            }

            Thread thread = new Thread(RunAction);
            thread.Start();

            return new TaskStatusWrapper(status);
        }
    }
}
