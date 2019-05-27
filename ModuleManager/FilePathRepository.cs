using System;
using System.IO;

namespace ModuleManager
{
    internal static class FilePathRepository
    {
        internal static readonly string cachePath = Path.Combine(Path.Combine(KSPUtil.ApplicationRootPath, "GameData"), "ModuleManager.ConfigCache");

        internal static readonly string techTreeFile = Path.Combine("GameData", "ModuleManager.TechTree");
        internal static readonly string techTreePath = Path.Combine(KSPUtil.ApplicationRootPath, techTreeFile);

        internal static readonly string physicsFile = Path.Combine("GameData", "ModuleManager.Physics");
        internal static readonly string physicsPath = Path.Combine(KSPUtil.ApplicationRootPath, physicsFile);
        internal static readonly string defaultPhysicsPath = Path.Combine(KSPUtil.ApplicationRootPath, "Physics.cfg");

        internal static readonly string partDatabasePath = Path.Combine(KSPUtil.ApplicationRootPath, "PartDatabase.cfg");

        internal static readonly string shaPath = Path.Combine(Path.Combine(KSPUtil.ApplicationRootPath, "GameData"), "ModuleManager.ConfigSHA");

        internal static readonly string logsDirPath = Path.Combine(Path.Combine(KSPUtil.ApplicationRootPath, "Logs"), "ModuleManager");
        internal static readonly string logPath = Path.Combine(logsDirPath, "ModuleManager.log");
        internal static readonly string patchLogPath = Path.Combine(logsDirPath, "MMPatch.log");

        internal static readonly string partDatabaseShaPath = Path.Combine(Path.Combine(KSPUtil.ApplicationRootPath, "GameData"), "ModuleManager.PartDatabasSha");
    }
}
