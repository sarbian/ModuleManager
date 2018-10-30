using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ModuleManager.Collections;
using ModuleManager.Extensions;
using ModuleManager.Logging;
using ModuleManager.Threading;

namespace ModuleManager
{
    public class MMPatchRunner
    {
        private const float TIME_TO_WAIT_FOR_LOGS = 0.05f;

        private readonly IBasicLogger kspLogger;

        public string Status { get; private set; } = "";
        public string Errors { get; private set; } = "";

        public MMPatchRunner(IBasicLogger kspLogger)
        {
            this.kspLogger = kspLogger ?? throw new ArgumentNullException(nameof(kspLogger));
        }

        public IEnumerator Run()
        {
            PostPatchLoader.Instance.databaseConfigs = null;

            string logsDirPath = Path.Combine(KSPUtil.ApplicationRootPath, "Logs");
            if (!Directory.Exists(logsDirPath)) Directory.CreateDirectory(logsDirPath);
            string logPath = Path.Combine(logsDirPath, "ModuleManager.log");

            kspLogger.Info("Patching started on a new thread, all output will be directed to " + logPath);

            MessageQueue<ILogMessage> kspLogQueue = new MessageQueue<ILogMessage>();
            MessageQueue<ILogMessage> mmLogQueue = new MessageQueue<ILogMessage>();
            bool logThreadExitFlag = false;
            ITaskStatus loggingThreadStatus = BackgroundTask.Start(delegate
            {
                QueueLogger kspLogger = new QueueLogger(kspLogQueue);
                using (StreamLogger streamLogger = new StreamLogger(new FileStream(logPath, FileMode.Create), kspLogger))
                {
                    while (!logThreadExitFlag)
                    {
                        float waitTargetTime = Time.realtimeSinceStartup + TIME_TO_WAIT_FOR_LOGS;

                        foreach (ILogMessage message in mmLogQueue.TakeAll())
                        {
                            message.LogTo(streamLogger);
                        }

                        float timeRemaining = waitTargetTime - Time.realtimeSinceStartup;
                        if (timeRemaining > 0)
                            System.Threading.Thread.Sleep((int)(timeRemaining * 1000));
                    }

                    foreach (ILogMessage message in mmLogQueue.TakeAll())
                    {
                        message.LogTo(streamLogger);
                    }

                    streamLogger.Info("Done!");
                }
            });

            // Wait for game database to be initialized for the 2nd time
            yield return null;

            IEnumerable<IProtoUrlConfig> databaseConfigs = null;

            MMPatchLoader patchLoader = new MMPatchLoader(new QueueLogger(mmLogQueue));

            ITaskStatus patchingThreadStatus = BackgroundTask.Start(delegate
            {
                databaseConfigs = patchLoader.Run();
            });

            while(true)
            {
                yield return null;

                if (!patchingThreadStatus.IsRunning)
                    logThreadExitFlag = true;

                Status = patchLoader.status;
                Errors = patchLoader.errors;

                foreach (ILogMessage message in kspLogQueue.TakeAll())
                {
                    message.LogTo(kspLogger);
                }

                if (!patchingThreadStatus.IsRunning && !loggingThreadStatus.IsRunning) break;
            }

            foreach (ILogMessage message in kspLogQueue.TakeAll())
            {
                message.LogTo(kspLogger);
            }

            if (patchingThreadStatus.IsExitedWithError)
            {
                kspLogger.Exception("The patching thread threw an exception", patchingThreadStatus.Exception);
                FatalErrorHandler.HandleFatalError("The patching thread threw an exception");
            }

            if (loggingThreadStatus.IsExitedWithError)
            {
                kspLogger.Exception("The logging thread threw an exception", loggingThreadStatus.Exception);
                FatalErrorHandler.HandleFatalError("The logging thread threw an exception");
            }

            if (databaseConfigs == null)
            {
                kspLogger.Error("The patcher returned a null collection of configs");
                FatalErrorHandler.HandleFatalError("The patcher returned a null collection of configs");
                yield break;
            }

            PostPatchLoader.Instance.databaseConfigs = databaseConfigs;
        }
    }
}
