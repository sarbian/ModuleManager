using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using UnityEngine;

namespace ModuleManager
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public class TechTreeManager: MonoBehaviour
    {
        internal void Start()
        {
            if (File.Exists(MMPatchLoader.techTreePath))
            {
                log("Setting moddeed tech tree as the active one");
                HighLogic.CurrentGame.Parameters.Career.TechTreeUrl = MMPatchLoader.techTreeFile;
            }
        }

        public static void log(String s)
        {
            print("[TechTreeManager] " + s);
        }

    }
}
