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
            if (File.Exists(MMPatchLoader.techTreePath))
            {
                log("Setting moddeed tech tree as the active one");
                HighLogic.CurrentGame.Parameters.Career.TechTreeUrl = MMPatchLoader.techTreeFile;
            }

            if (File.Exists(MMPatchLoader.physicsPath))
            {
                log("Setting moddeed physics as the active one");

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
