using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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

            if (!ReferenceEquals(targetCls.Assembly, winner.assembly))
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

        private static bool hasRun;

        internal void Awake()
        {
            try
            {
                // Guard against multiple copies of the same DLL
                if (hasRun)
                {
                    Assembly currentAssembly = Assembly.GetExecutingAssembly();
                    Debug.Log("[SaveGameFixer] Multiple copies of current version. Using the first copy. Version: " + currentAssembly.GetName().Version);
                    return;
                }
                hasRun = true;

                if (!RunTypeElection(typeof(SaveGameFixer), "ModuleManager"))
                    return;

                // So at this point we know we have won the election, and will be using the class versions as in this assembly.

                // Disabled for now since .24 fix the module loading order.
                // UpdateSaves();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            finally
            {
                // Destroy ourself because there's no reason to still hang around
                Destroy(gameObject);
                enabled = false;
            }
        }
        #endregion

        #region State

        // Files and directories
        private readonly string gameDataRoot = Path.Combine(Path.GetFullPath(KSPUtil.ApplicationRootPath), "GameData" + Path.DirectorySeparatorChar);
        private readonly string savesRoot = Path.Combine(Path.GetFullPath(KSPUtil.ApplicationRootPath), "saves" + Path.DirectorySeparatorChar);
        private string backupDir;
        private string logFile;

        // Bits and pieces for logging
        private readonly StringBuilder backupLog = new StringBuilder();
        private readonly List<string> logContext = new List<string>();
        private int logCtxCur;

        // Flags
        private bool logOnly = true;
        private bool needsBackup;
        private bool needsSave;
        private bool partMissing;

        #endregion

        #region Finding the part


        private void UpdateSaves()
        {
            foreach (string saveDir in Directory.GetDirectories(savesRoot)) 
                UpdateSaveDir(saveDir);

            // Write the backup log if needed
            if (!logOnly && backupLog.Length > 0)
            {
                CreateBackupDir();
                File.AppendAllText(logFile, backupLog.ToString());
            }
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
                {
                    string filename = Path.GetFileName(sfsFile);
                    if(filename == "persistent.sfs" || filename == "quicksave.sfs")
                        UpdateSFS(sfsFile);                    
                }
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
            foreach (string vabCraft in files)
                if (vabCraft.EndsWith(".craft"))
                    UpdateCraft(vabCraft);
        }

        private void UpdateCraft(string vabCraft)
        {
            try
            {
                PushLogContext("Craft file: " + vabCraft.Substring(savesRoot.Length, vabCraft.Length-savesRoot.Length));
                ConfigNode craft = ConfigNode.Load(vabCraft);

                needsBackup = false; needsSave = false; partMissing = false;

                foreach (ConfigNode part in craft.GetNodes("PART"))
                    UpdatePart(part);

                // If a part is missing don't do anything special. The game just locks the craft, it doesn't destory them.
                if (partMissing)
                {
                    WriteDebugMessage("Craft has mising parts in the VAB, the craft file will be locked.");
                    WriteDebugMessage("Delete the craft to get rid of this message.");
                }

                BackupAndReplace(vabCraft, craft);


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

                needsBackup = false; needsSave = false; partMissing = false;

                foreach (ConfigNode game in sfs.GetNodes("GAME"))
                    foreach (ConfigNode flightState in game.GetNodes("FLIGHTSTATE"))
                        foreach (ConfigNode vessel in flightState.GetNodes("VESSEL"))
                            UpdateVessel(vessel);

                // Backup if missing parts.
                // TODO: handle missing parts more gracefully, like missing modules are handled.
                if (partMissing) 
                {
                    WriteLogMessage("Save game has vessels with missing parts. These vessels will be deleted on loading the save.");
                    WriteLogMessage("The persistence file has been backed up. Note that this will keep occuring every load until");
                    WriteLogMessage("either the save game is loaded and the ships are destroyed, or the missing parts are not missing.");
                    needsBackup = true;
                }

                BackupAndReplace(sfsFile, sfs);
            }
            finally
            {
                PopLogContext();
            }
                
        }

        private void UpdateVessel(ConfigNode vessel)
        {
            try
            {
                PushLogContext("Vessel: " + vessel.GetValue("name"));

                foreach (ConfigNode part in vessel.GetNodes("PART"))
                    UpdatePart(part);
            }
            finally
            {
                PopLogContext();
            }
        }
        #endregion

        private void UpdatePart(ConfigNode part)
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
                    WriteLogMessage("Part \"" + partName + "\" has been deleted.");
                    partMissing = true;
                    return;
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

                // Discard any backups that are already in saved modules
                // ReSharper disable once ForCanBeConvertedToForeach
                for (int i = 0; i < backupModules.Length; ++i)
                    for (int j = 0; j < savedModules.Length; ++j)
                        if (savedModules[j] != null && backupModules[i] != null && backupModules[i].GetValue("name") == savedModules[j].GetValue("name"))
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
                        // ReSharper disable once ForCanBeConvertedToForeach
                        for (int i = 0; i < backupModules.Length; ++i)
                            if (backupModules[i] != null)
                                moduleBackupConfig.AddNode(backupModules[i]);
                        // backup anything in saved that's left over
                        // ReSharper disable once ForCanBeConvertedToForeach
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
            // Write any pending log headers
            string indent;
            for (; logCtxCur < logContext.Count; logCtxCur++)
            {
                indent = new String(' ', 4 * logCtxCur);
                backupLog.Append(' ').Append(indent).AppendLine(logContext[logCtxCur]);
                Debug.Log(indent + logContext[logCtxCur]);
            }
            indent = new String(' ', 4 * logCtxCur);
            backupLog.Append(debugMsg ? ' ' : '*').Append(indent).AppendLine(logMessage);
            if (debugMsg)
                Debug.Log(indent + logMessage);
            else
                Debug.LogWarning(indent + logMessage);
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

        private void BackupAndReplace(string file, ConfigNode config)
        {

            if (needsBackup)
            {
                CreateBackupDir();

                string relPath = file.Substring(savesRoot.Length, file.Length - savesRoot.Length);

                string backupTo = Path.Combine(backupDir, relPath);
                // ReSharper disable once AssignNullToNotNullAttribute
                Directory.CreateDirectory(Path.GetDirectoryName(backupTo));

                File.Copy(file, backupTo);

                logOnly = false;
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
