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

        #region type election and other bootstrap stuff.

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
        #endregion

        #region Finding the part
        private string savesRoot;

        private void UpdateSaves()
        {
            savesRoot = Path.Combine(Path.GetFullPath(KSPUtil.ApplicationRootPath), "saves" + Path.DirectorySeparatorChar);

            foreach (string saveDir in Directory.GetDirectories(savesRoot)) 
                UpdateSaveDir(saveDir);
        }


        private void UpdateSaveDir(string saveDir)
        {
            try
            {
                PushLogContext("Save Game: " + saveDir.Substring(savesRoot.Length, saveDir.Length-savesRoot.Length));

                char ds = Path.DirectorySeparatorChar;

                // .craft files
                UpdateCraftDir(saveDir + ds + "Ships" + ds + "VAB");
                UpdateCraftDir(saveDir + ds + "Ships" + ds + "SPH");
                UpdateCraftDir(saveDir + ds + "Subassemblies");

                foreach (string sfsFile in Directory.GetFiles(saveDir))
                    if (sfsFile.EndsWith(".sfs"))
                        UpdateSFS(sfsFile);
            }
            finally
            {
                PopLogContext();
            }
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

        private void UpdateCraft(string vabCraft)
        {
            try
            {
                PushLogContext("Craft file: " + vabCraft.Substring(savesRoot.Length, vabCraft.Length-savesRoot.Length));
                ConfigNode craft = ConfigNode.Load(vabCraft);

                bool needsBackup = false, needsSave = false;
                foreach (ConfigNode part in craft.GetNodes("PART"))
                    UpdatePart(part, ref needsBackup, ref needsSave);

                BackupAndReplace(vabCraft, craft, needsBackup, needsSave);
            }
            finally
            {
                PopLogContext();
            }
        }

        private void UpdateSFS(string sfsFile)
        {
            ConfigNode sfs = ConfigNode.Load(sfsFile);

            try
            {
                PushLogContext("Save file: " + sfsFile.Substring(savesRoot.Length, sfsFile.Length-savesRoot.Length));

                bool needsBackup = false, needsSave = false;
                foreach (ConfigNode game in sfs.GetNodes("GAME"))
                    foreach (ConfigNode flightState in game.GetNodes("FLIGHTSTATE"))
                        foreach (ConfigNode vessel in flightState.GetNodes("VESSEL"))
                            UpdateVessel(vessel, ref needsBackup, ref needsSave);

                BackupAndReplace(sfsFile, sfs, needsBackup, needsSave);
            }
            finally
            {
                PopLogContext();
            }
                
        }

        private void UpdateVessel(ConfigNode vessel, ref bool needsBackup, ref bool needsSave)
        {
            try
            {
                PushLogContext("Vessel: " + vessel.GetValue("name"));

                foreach (ConfigNode part in vessel.GetNodes("PART"))
                    UpdatePart(part, ref needsBackup, ref needsSave);
            }
            finally
            {
                PopLogContext();
            }
        }
        #endregion

        private void UpdatePart(ConfigNode part, ref bool needsBackup, ref bool needsSave)
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

            try
            {
                PushLogContext("Part: " + partName);

                AvailablePart available = PartLoader.getPartInfoByName(partName);

                if (available == null)
                {
                    WriteLogMessage("Backup created - part \"" + partName + "\" has been deleted and ship will be destroyed.");
                    needsBackup = true;
                }
                
                PartModuleList prefabModules = available.partPrefab.Modules;

                if (prefabModules == null)
                    return;

                // Do we need to do anything?
                if (prefabModules.Count == savedModules.Length && backupModules.Length == 0)
                {
                    for (int i = 0; i < savedModules.Length; ++i)
                        if (savedModules[i] != null && savedModules[i].GetValue("name") != prefabModules[i].moduleName)
                            goto needUpdate;
                    return;
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

                // Discard any backups that are already in saved modules
                for (int i = 0; i < backupModules.Length; ++i)
                    for (int j = 0; j < savedModules.Length; ++j)
                        if (savedModules[j] != null && backupModules[i].GetValue("name") == savedModules[j].GetValue("name"))
                        {
                            WriteDebugMessage("Discarding module backup \"" + backupModules[i].GetValue("name") + "\" as exists in the save already.");
                            backupModules[i] = null;
                            backupRemain--;
                            needsSave = true;
                        }


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
                            if (i != j)
                            {
                                WriteDebugMessage("Module \"" + savedModules[j].GetValue("name") + "\" has had order changed. " + j + "=>" + i);
                                needsSave = true;
                            }

                            part.AddNode(savedModules[j]);
                            savedModules[j] = null;
                            savedRemain--;
                            goto foundModule;
                        }
                    for (int j = 0; j < backupModules.Length; ++j)
                        if (backupModules[j] != null && backupModules[j].GetValue("name") == prefabModules[i].moduleName)
                        {
                            // The module will be restored from backup
                            WriteLogMessage("Module \"" + backupModules[j].GetValue("name") + "\" has been restored from backup. ");
                            needsBackup = true;
                            needsSave = true;

                            backupModules[j].AddValue("MM_RESTORED", "true");
                            part.AddNode(backupModules[j]);
                            backupModules[j] = null;
                            backupRemain--;
                            goto foundModule;
                        }
                    // Can't find it anywhere, reinitialize
                    WriteLogMessage("Module \"" + prefabModules[i].moduleName + "\" is not present in the save and will be reinitialized. ");
                    needsBackup = true;
                    needsSave = true;

                    ConfigNode newNode = new ConfigNode("MODULE");
                    newNode.AddValue("name", prefabModules[i].moduleName);
                    newNode.AddValue("MM_REINITIALIZE", "true");
                    part.AddNode(newNode);
                foundModule: ;
                }

                if (savedRemain > 0 || backupRemain > 0)
                {

                    // Discard saves for modules that are explicitly marked as dynamic or have a module available to be used. 
                    // Modules that are explicitly maked as not dynamic (MM_DYNAMIC = false) will be saved in the backup regardless
                    // of if their PartModule class is available.
                    for (int i = 0; i < savedModules.Length; ++i)
                        if (savedModules[i] != null && savedModules[i].GetValue("MM_DYNAMIC") != "false"
                            && (savedModules[i].GetValue("MM_DYNAMIC") == "true" || AssemblyLoader.GetClassByName(typeof(PartModule), savedModules[i].GetValue("name")) != null))
                        {
                            savedModules[i] = null;
                            --savedRemain;
                        }

                    if (savedRemain > 0)
                    {
                        if (moduleBackupConfig == null)
                        {
                            available.partPrefab.AddModule(typeof(ModuleConfigBackup).Name);
                            moduleBackupConfig = new ConfigNode("MODULE");
                            moduleBackupConfig.AddValue("name", typeof(ModuleConfigBackup).Name);
                            part.AddNode(moduleBackupConfig);
                        }
                        // copy the old backups
                        for (int i = 0; i < backupModules.Length; ++i)
                            if (backupModules[i] != null)
                                moduleBackupConfig.AddNode(backupModules[i]);
                        // backup anything in saved that's left over
                        for (int i = 0; i < savedModules.Length; ++i)
                            if (savedModules[i] != null)
                            {
                                savedModules[i].RemoveValues("MM_RESTORED");
                                moduleBackupConfig.AddNode(savedModules[i]);

                                WriteLogMessage("Module \"" + savedModules[i].GetValue("name") + "\" is present in the part but is no longer available. Saved config to backup, will be restored if you reinstall the mod.");
                                needsSave = true;
                                needsBackup = true;
                            }
                    }
                }

                if (!needsSave)
                    return;

                // Stick the resources back at the end just to be consistent
                ConfigNode[] resources = part.GetNodes("RESOURCE");
                part.RemoveNodes("RESOURCE");
                foreach (ConfigNode r in resources)
                    part.AddNode(r);

                //Debug.Log("[SaveGameFixer] Result:\n" + part);
            }
            finally
            {
                PopLogContext();
            }
        }

        #region Backups

        private List<string> logContext = new List<string>();
        private int logCtxCur = 0;
        private string backupDir = null;
        private string logFile = null;

        private void PushLogContext(string p)
        {
            logContext.Add(p);
        }

        private void PopLogContext()
        {
            logContext.RemoveAt(logContext.Count - 1);
            if (logCtxCur > logContext.Count)
                logCtxCur = logContext.Count;
        }

        private void WriteDebugMessage(string logMessage)
        {
            WriteLogMessage(logMessage, true);
        }

        private void WriteLogMessage(string logMessage, bool debugMsg = false)
        {
#if DEBUG
            string dbg = debugMsg ? "[dbg]" : "[log]";

#else
            string dbg = string.Empty;
#endif
            CreateBackupDir();

            StringBuilder sb = new StringBuilder();

            // Write any pending log headers
            string indent;
            for (; logCtxCur < logContext.Count; logCtxCur++)
            {
                indent = new String(' ', 4 * logCtxCur + dbg.Length);
                sb.Append(indent).AppendLine(logContext[logCtxCur]);
                Debug.Log("[SaveGameFixer]" + indent + logContext[logCtxCur]);
            }
            indent = new String(' ', 4 * logCtxCur);
            sb.Append(dbg).Append(indent).AppendLine(logMessage);
            Debug.Log("[SaveGameFixer]" + dbg + indent + logMessage);

            File.AppendAllText(logFile, sb.ToString());
        }

        private void CreateBackupDir()
        {
            if (backupDir == null)
            {
                backupDir = Path.Combine(KSPUtil.ApplicationRootPath, string.Format("saves_backup{1}{0:yyyyMMdd-HHmmss}", DateTime.Now, Path.DirectorySeparatorChar));
                Directory.CreateDirectory(backupDir);
                logFile = Path.Combine(backupDir, "backup.log");
            }
        }

        private void BackupAndReplace(string file, ConfigNode config, bool needsBackup, bool needsSave)
        {
#if !DEBUG
            if (needsBackup)
#endif
            {
                CreateBackupDir();

                string relPath = file.Substring(savesRoot.Length, file.Length - savesRoot.Length);

                string backupTo = Path.Combine(backupDir, relPath);
                Directory.CreateDirectory(Path.GetDirectoryName(backupTo));

                File.Copy(file, backupTo);
            }

            if(needsSave)
                config.Save(file);
        }


        #endregion
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
