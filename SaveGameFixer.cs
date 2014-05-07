using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ModuleManager
{
    [KSPAddonFixed(KSPAddon.Startup.MainMenu, true, typeof(SaveGameFixer))]
    internal class SaveGameFixer : MonoBehaviour
    {

        // This is stolen unchanged from KSPAPIExtensions
        private static bool RunTypeElection(Type targetCls, String assemName)
        {
            if (targetCls.Assembly.GetName().Name != assemName)
                throw new InvalidProgramException("Assembly: " + targetCls.Assembly.GetName().Name + " at location: " + targetCls.Assembly.Location + " is not in the expected assembly. Code has been copied and this will cause problems.");

            // If we are loaded from the first loaded assembly that has this class, then we are responsible to destroy
            var candidates = (from ass in AssemblyLoader.loadedAssemblies
                              where ass.assembly.GetType(targetCls.FullName, false) != null
                              && ass.assembly.GetName().Name == assemName
                              orderby ass.assembly.GetName().Version descending, ass.path ascending
                              select ass).ToArray();
            var winner = candidates.First();

            if (targetCls.Assembly != winner.assembly)
                return false;

            if (candidates.Length > 1)
            {
                string losers = string.Join("\n", (from t in candidates
                                                   where t != winner
                                                   select string.Format("Version: {0} Location: {1}", t.assembly.GetName().Version, t.path)).ToArray());

                Debug.Log("[" + targetCls.Name + "] version " + winner.assembly.GetName().Version + " at " + winner.path + " won the election against\n" + losers);
            }
            else
                Debug.Log("[" + targetCls.Name + "] Elected unopposed version= " + winner.assembly.GetName().Version + " at " + winner.path);

            return true;
        }

        internal void Awake()
        {
            try
            {
                if (!RunTypeElection(typeof(SaveGameFixer), "ModuleManager"))
                    return;
                // So at this point we know we have won the election, and will be using the class versions as in this assembly.

                UpdateSaves();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                // Destroy ourself because there's no reason to still hang around
                UnityEngine.Object.Destroy(gameObject);
                enabled = false;
            }
        }


        private void UpdateSaves()
        {
            foreach (string saveDir in Directory.GetDirectories(KSPUtil.ApplicationRootPath + Path.DirectorySeparatorChar + "saves")) 
                UpdateSaveDir(saveDir);
        }

        private void UpdateSaveDir(string saveDir)
        {
            char ds = Path.DirectorySeparatorChar;

            // .craft files
            UpdateCraftDir(saveDir + ds + "Ships" + ds + "VAB");
            UpdateCraftDir(saveDir + ds + "Ships" + ds + "SPH");
            UpdateCraftDir(saveDir + ds + "Subassemblies");

            foreach (string sfsFile in Directory.GetFiles(saveDir))
                if (sfsFile.EndsWith(".sfs"))
                    UpdateSFS(sfsFile);
        }

        private void UpdateCraftDir(string dir)
        {
            string[] files;
            try
            {
                files = Directory.GetFiles(dir);
            }
            catch (Exception)
            {
                return;
            }
            foreach (string vabCraft in Directory.GetFiles(dir))
                if (vabCraft.EndsWith(".craft"))
                    UpdateCraft(vabCraft);
        }

        private void UpdateSFS(string sfsFile)
        {
            ConfigNode sfs = ConfigNode.Load(sfsFile);

            bool modified = false;
            foreach (ConfigNode game in sfs.GetNodes("GAME"))
                foreach (ConfigNode flightState in game.GetNodes("FLIGHTSTATE"))
                    foreach (ConfigNode vessel in flightState.GetNodes("VESSEL"))
                        foreach (ConfigNode part in vessel.GetNodes("PART"))
                            modified |= UpdatePart(part, sfsFile);

            if (modified)
                sfs.Save(sfsFile);
        }

        private void UpdateCraft(string vabCraft)
        {
            ConfigNode craft = ConfigNode.Load(vabCraft);

            bool modified = false;
            foreach (ConfigNode part in craft.GetNodes("PART"))
                modified |= UpdatePart(part, vabCraft);

            if(modified)
                craft.Save(vabCraft);
        }

        private bool UpdatePart(ConfigNode part, string source)
        {
            // The modules saved with the part
            ConfigNode[] savedModules = part.GetNodes("MODULE");
            int savedRemain = savedModules.Length;

            //Debug.LogWarning("Saved modules: "  + string.Join(",", (from s in savedModules select s.GetValue("name")).ToArray()));

            // The modules saved as backups
            ConfigNode[] backupModules = new ConfigNode[0];
            for(int i = 0; i < savedModules.Length; ++i)
                if (savedModules[i].GetValue("name") == typeof(ModuleConfigBackup).Name)
                {
                    backupModules = savedModules[i].GetNodes("MODULE");
                    savedModules[i] = null;
                    savedRemain--;
                    break;
                }
            //Debug.LogWarning("Backup modules: " + string.Join(",", (from s in backupModules select s.GetValue("name")).ToArray()));
            int backupRemain = backupModules.Length;

            // The modules in the part prefab 
            string partName = part.GetValue("name");
            if (partName == null)
            {
                partName = part.GetValue("part");
                partName = partName.Substring(0, partName.LastIndexOf('_'));
            }

            AvailablePart available = PartLoader.getPartInfoByName(partName);

            if (available == null)
            {
                Debug.LogWarning("Unable to find part: " + partName);
                return false;
            }

            PartModuleList prefabModules = available.partPrefab.Modules;

            // Do we need to do anything?
            if (prefabModules.Count == savedModules.Length && backupModules.Length == 0)
            {
                for (int i = 0; i < savedModules.Length; ++i)
                    if (savedModules[i] != null && savedModules[i].GetValue("name") != prefabModules[i].moduleName)
                        goto needUpdate;
                return false;
            needUpdate: ;
            }

            // Yes we do!
#if false
            string prefabNames = "Prefab modules: ";
            for (int i = 0; i < prefabModules.Count; ++i)
                prefabNames += (prefabModules[i] as PartModule).moduleName + ",";
            prefabNames = prefabNames.Substring(0, prefabNames.Length-1);

            Debug.Log("[SaveGameFixer] Fixing Part: " + partName + " in file: " + source + "\n" + prefabNames
                + "\nSaved modules: " + string.Join(",", (from s in savedModules select (s==null?"***":s.GetValue("name"))).ToArray())
                + "\nBackup modules: " + string.Join(",", (from s in backupModules select (s==null?"***":s.GetValue("name"))).ToArray())
                //+ "\nConfig: \n" + part
                );
#endif

            part.RemoveNodes("MODULE");

            ConfigNode moduleBackupConfig = null;

            for (int i = 0; i < prefabModules.Count; ++i)
            {
                if (prefabModules[i] is ModuleConfigBackup)
                {
                    moduleBackupConfig = new ConfigNode("MODULE");
                    moduleBackupConfig.AddValue("name", typeof(ModuleConfigBackup).Name);
                    part.AddNode(moduleBackupConfig);
                    continue;
                }
                for (int j = 0; j < savedModules.Length; ++j)
                    if (savedModules[j] != null && savedModules[j].GetValue("name") == prefabModules[i].moduleName) 
                    {
                        // The module is saved normally
                        part.AddNode(savedModules[j]);
                        savedModules[j] = null;
                        savedRemain--;
                        goto foundModule;
                    }
                if (backupModules != null)
                    for (int j = 0; j < backupModules.Length; ++j)
                        if (backupModules[j] != null && backupModules[j].GetValue("name") == prefabModules[i].moduleName)
                        {
                            // The module will be restored from backup
                            backupModules[j].AddValue("MM_RESTORED", "true");
                            part.AddNode(backupModules[j]);
                            backupModules[j] = null;
                            backupRemain--;
                            goto foundModule;
                        }
                // Can't find it anywhere, reinitialize
                ConfigNode newNode = new ConfigNode("MODULE");
                newNode.AddValue("name", prefabModules[i].moduleName);
                newNode.AddValue("MM_REINITIALIZE", "true");
                part.AddNode(newNode);
            foundModule: ;
            }

            if (savedRemain > 0 || backupRemain > 0)
            {
                if (moduleBackupConfig == null)
                {
                    available.partPrefab.AddModule(typeof(ModuleConfigBackup).Name);
                    moduleBackupConfig = new ConfigNode("MODULE");
                    moduleBackupConfig.AddValue("name", typeof(ModuleConfigBackup).Name);
                    part.AddNode(moduleBackupConfig);
                }
                for (int i = 0; i < savedModules.Length; ++i)
                    if (savedModules[i] != null && savedModules[i].GetValue("MM_DYNAMIC") != "true")
                    {
                        savedModules[i].RemoveValues("MM_RESTORED");
                        moduleBackupConfig.AddNode(savedModules[i]);
                    }
                for (int i = 0; i < backupModules.Length; ++i)
                    if (backupModules[i] != null)
                        moduleBackupConfig.AddNode(backupModules[i]);
            }
            
            // Stick the resources back at the end just to be consistent
            ConfigNode[] resources = part.GetNodes("RESOURCE");
            part.RemoveNodes("RESOURCE");
            foreach (ConfigNode r in resources)
                part.AddNode(r);

            //Debug.Log("[SaveGameFixer] Result:\n" + part);

            return true;
        }


    }



    internal class ModuleConfigBackup : PartModule
    {

        public ConfigNode removedConfigs;

        public override void OnLoad(ConfigNode node)
        {
            if (removedConfigs == null)
                removedConfigs = new ConfigNode();

            foreach (ConfigNode subNode in node.GetNodes("MODULE"))
                removedConfigs.AddNode(subNode);
        }

        public override void OnSave(ConfigNode node)
        {
            if (removedConfigs == null)
                return;

            foreach (ConfigNode subNode in removedConfigs.GetNodes("MODULE"))
                node.AddNode(subNode);
        }
    }

}
