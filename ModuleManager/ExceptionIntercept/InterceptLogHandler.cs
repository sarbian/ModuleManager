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

        public InterceptLogHandler(ILogHandler baseLogHandler)
        {
            this.baseLogHandler = baseLogHandler ?? throw new ArgumentNullException(nameof(baseLogHandler));
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
                string message = "Intercepted a ReflectionTypeLoadException. List of broken DLLs:\n";
                try
                {
                    var assemblies = ex.Types.Where(x => x != null).Select(x => x.Assembly).Distinct();
                    foreach (Assembly assembly in assemblies)
                    {
                        if (Warnings == "")
                        {
                            Warnings = "Mod(s) DLL that are not compatible with this version of KSP\n";
                        }
                        string modInfo = assembly.GetName().Name + " " + assembly.GetName().Version + " " +
                                         assembly.Location.Remove(0, gamePathLength) + "\n";
                        if (!brokenAssemblies.Contains(assembly))
                        {
                            brokenAssemblies.Add(assembly);
                            Warnings += modInfo;
                        }
                        message += modInfo;
                    }
                }
                catch (Exception e)
                {
                    message += "Exception " + e.GetType().Name + " while handling the exception...";
                }
                ModuleManager.Log(message);
            }
        }
    }
}
