using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ModuleManager.UnityLogHandle
{
    class InterceptLogHandler : ILogHandler
    {
        private readonly ILogHandler baseLogHandler;
        private readonly List<Assembly> brokenAssemblies = new List<Assembly>();
        private readonly int gamePathLength;

        public static string Warnings { get; private set; } = "";

        public InterceptLogHandler()
        {
            baseLogHandler = Debug.unityLogger.logHandler;
            Debug.unityLogger.logHandler = this;
            gamePathLength = Path.GetFullPath(KSPUtil.ApplicationRootPath).Length;
        }

        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            baseLogHandler.LogFormat(logType, context, format, args);
        }

        public void LogException(Exception exception, Object context)
        {
            baseLogHandler.LogException(exception, context);

            if (exception is ReflectionTypeLoadException ex)
            {
                ModuleManager.Log("Intercepted a ReflectionTypeLoadException. List of broken DLLs:");
                var assemblies = ex.Types.Where(x => x != null).Select(x => x.Assembly).Distinct();
                foreach (Assembly assembly in assemblies)
                {
                    if (Warnings == "")
                    {
                        Warnings = "ModuleManager mod(s) DLL that are not compatible with this version of KSP\n";
                    }
                    if (!brokenAssemblies.Contains(assembly))
                    {
                        brokenAssemblies.Add(assembly);
                        Warnings += assembly.GetName().Name + " " + assembly.GetName().Version + " " + assembly.Location.Remove(0, gamePathLength) + "\n";
                    }
                    ModuleManager.Log(assembly.GetName().Name + " " + assembly.GetName().Version + " " + assembly.Location.Remove(0, gamePathLength));
                }
            }
            baseLogHandler.LogException(exception, context);
        }
    }
}
