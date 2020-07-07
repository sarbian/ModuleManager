using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using ModuleManager.Collections;
using ModuleManager.Extensions;
using ModuleManager.Logging;
using ModuleManager.Threading;

using static ModuleManager.FilePathRepository;

namespace ModuleManager
{
    public class MMPatchRunner
    {
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

            if (!Directory.Exists(logsDirPath)) Directory.CreateDirectory(logsDirPath);

            kspLogger.Info("Patching started on a new thread, all output will be directed to " + logPath);

            MessageQueue<ILogMessage> mmLogQueue = new MessageQueue<ILogMessage>();
            QueueLogRunner logRunner = new QueueLogRunner(mmLogQueue);
            ITaskStatus loggingThreadStatus = BackgroundTask.Start(delegate
            {
                using StreamLogger streamLogger = new StreamLogger(new FileStream(logPath, FileMode.Create));
                streamLogger.Info("Log started at " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                logRunner.Run(streamLogger);
                streamLogger.Info("Done!");
            });

            // Wait for game database to be initialized for the 2nd time and wait for any plugins to initialize
            yield return null;
            yield return null;

            IBasicLogger mmLogger = new QueueLogger(mmLogQueue);

            IEnumerable<ModListGenerator.ModAddedByAssembly> modsAddedByAssemblies = ModListGenerator.GetAdditionalModsFromStaticMethods(mmLogger);

            IEnumerable<IProtoUrlConfig> databaseConfigs = null;

            MMPatchLoader patchLoader = new MMPatchLoader(modsAddedByAssemblies, mmLogger);

            ITaskStatus patchingThreadStatus = BackgroundTask.Start(delegate
            {
                databaseConfigs = patchLoader.Run();
            });

            while(true)
            {
                yield return null;

                if (!patchingThreadStatus.IsRunning)
                    logRunner.RequestStop();

                Status = patchLoader.status;
                Errors = patchLoader.errors;

                if (!patchingThreadStatus.IsRunning && !loggingThreadStatus.IsRunning) break;
            }

            if (patchingThreadStatus.IsExitedWithError)
            {
                kspLogger.Exception("The patching thread threw an exception", patchingThreadStatus.Exception);
                FatalErrorHandler.HandleFatalError("The patching thread threw an exception");
                yield break;
            }

            if (loggingThreadStatus.IsExitedWithError)
            {
                kspLogger.Exception("The logging thread threw an exception", loggingThreadStatus.Exception);
                FatalErrorHandler.HandleFatalError("The logging thread threw an exception");
                yield break;
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
