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
                log("Setting modded tech tree as the active one");
                HighLogic.CurrentGame.Parameters.Career.TechTreeUrl = MMPatchLoader.techTreeFile;
            }

            if (PhysicsGlobals.PhysicsDatabaseFilename != MMPatchLoader.physicsFile && File.Exists(MMPatchLoader.physicsPath))
            {
                log("Setting modded physics as the active one");

                PhysicsGlobals.PhysicsDatabaseFilename = MMPatchLoader.physicsFile;

                if (!PhysicsGlobals.Instance.LoadDatabase())
                    log("Something went wrong while setting the active physics config.");
            }
        }

        public static void log(String s)
        {
            print("[CustomConfigsManager] " + s);
        }

    }
}
