using System;
using System.IO;
using UnityEngine;

namespace ModuleManager
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public class CustomConfigsManager : MonoBehaviour
    {
        internal void Start()
        {
            if (HighLogic.CurrentGame.Parameters.Career.TechTreeUrl != MMPatchLoader.techTreeFile &&  File.Exists(MMPatchLoader.techTreePath))
            {
                Log("Setting modded tech tree as the active one");
                HighLogic.CurrentGame.Parameters.Career.TechTreeUrl = MMPatchLoader.techTreeFile;
            }

            if (PhysicsGlobals.PhysicsDatabaseFilename != MMPatchLoader.physicsFile && File.Exists(MMPatchLoader.physicsPath))
            {
                Log("Setting modded physics as the active one");

                PhysicsGlobals.PhysicsDatabaseFilename = MMPatchLoader.physicsFile;

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
