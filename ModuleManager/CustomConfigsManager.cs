using System;
using System.IO;
using UnityEngine;

using static ModuleManager.FilePathRepository;

namespace ModuleManager
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public class CustomConfigsManager : MonoBehaviour
    {
        internal void Start()
        {
            if (HighLogic.CurrentGame.Parameters.Career.TechTreeUrl != techTreeFile && File.Exists(techTreePath))
            {
                Log("Setting modded tech tree as the active one");
                HighLogic.CurrentGame.Parameters.Career.TechTreeUrl = techTreeFile;
            }

            if (PhysicsGlobals.PhysicsDatabaseFilename != physicsFile && File.Exists(physicsPath))
            {
                Log("Setting modded physics as the active one");

                PhysicsGlobals.PhysicsDatabaseFilename = physicsFile;

                if (!PhysicsGlobals.Instance.LoadDatabase())
                    Log("Something went wrong while setting the active physics config.");
            }
        }

        public static void Log(String s)
        {
            print("[CustomConfigsManager] " + s);
        }

    }
}
