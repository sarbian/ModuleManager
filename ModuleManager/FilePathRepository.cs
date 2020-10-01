using System;
using System.IO;

namespace ModuleManager
{
    internal static class FilePathRepository
    {
        internal static readonly string normalizedRootPath = Path.GetFullPath(KSPUtil.ApplicationRootPath);
        internal static readonly string cachePath = Path.Combine(normalizedRootPath, "GameData", "ModuleManager.ConfigCache");

        internal static readonly string techTreeFile = Path.Combine("GameData", "ModuleManager.TechTree");
        internal static readonly string techTreePath = Path.Combine(normalizedRootPath, techTreeFile);

        internal static readonly string physicsFile = Path.Combine("GameData", "ModuleManager.Physics");
        internal static readonly string physicsPath = Path.Combine(normalizedRootPath, physicsFile);
        internal static readonly string defaultPhysicsPath = Path.Combine(normalizedRootPath, "Physics.cfg");

        internal static readonly string partDatabasePath = Path.Combine(normalizedRootPath, "PartDatabase.cfg");

        internal static readonly string shaPath = Path.Combine(normalizedRootPath, "GameData", "ModuleManager.ConfigSHA");

        internal static readonly string logsDirPath = Path.Combine(normalizedRootPath, "Logs", "ModuleManager");
        internal static readonly string logPath = Path.Combine(logsDirPath, "ModuleManager.log");
        internal static readonly string patchLogPath = Path.Combine(logsDirPath, "MMPatch.log");
    }
}
