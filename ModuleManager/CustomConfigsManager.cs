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
        }

        public static void Log(String s)
        {
            print("[CustomConfigsManager] " + s);
        }

    }
}
