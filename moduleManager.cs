using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using KSP;
using UnityEngine;

namespace ModuleManager
{
    // Once MUST be true for the election process to work when 2+ dll of the same version are loaded
    // But I need it to be false for the reload database thingy
    [KSPAddonFixed(KSPAddon.Startup.Instantly, false, typeof(ConfigManager))]
    public class ConfigManager : MonoBehaviour
    {
        #region state

        private bool loaded = false;

        private int patchCount = 0;
        private int errorCount = 0;
        private int needsUnsatisfiedCount = 0;

        private Dictionary<String, int> errorFiles;
        private List<AssemblyName> mods;

        private string status = "Processing Module Manager patch\nPlease Wait...";
        private string errors = "";

        #endregion

        #region Top Level - Update
        public void Update()
        {
            #region Initialization
            /* 
             * It should be a code to reload when the Reload Database debug button is used.
             * But it seem to go balistic after the 2nd reload.
             * 
            if (PartLoader.Instance.Recompile == waitingReload)
            {
                waitingReload = !waitingReload;
                print("[ModuleManager] waitingReload change " + waitingReload + " loaded " + loaded);
                if (!waitingReload)
                {
                    loaded = false;
                    print("[ModuleManager] loaded = false ");
                }
            }
             */

            if (!GameDatabase.Instance.IsReady() && ((HighLogic.LoadedScene == GameScenes.MAINMENU) || (HighLogic.LoadedScene == GameScenes.SPACECENTER)))
            {
                return;
            }

            if (loaded)
                return;

            patchCount = 0;
            errorCount = 0;
            needsUnsatisfiedCount = 0;
            errorFiles = new Dictionary<string, int>();
            #endregion

            #region Type election
            // Check for old version and MMSarbianExt
            var oldMM = AssemblyLoader.loadedAssemblies.Where(a => a.assembly.GetName().Name == Assembly.GetExecutingAssembly().GetName().Name).Where(a => a.assembly.GetName().Version.CompareTo(new System.Version(1, 5, 0)) == -1);
            var oldAssemblies = oldMM.Concat(AssemblyLoader.loadedAssemblies.Where(a => a.assembly.GetName().Name == "MMSarbianExt"));
            if (oldAssemblies.Any())
            {
                var badPaths = oldAssemblies.Select(a => a.path).Select(p => Uri.UnescapeDataString(new Uri(Path.GetFullPath(KSPUtil.ApplicationRootPath)).MakeRelativeUri(new Uri(p)).ToString().Replace('/', Path.DirectorySeparatorChar)));
                status = "You have old versions of Module Manager (older than 1.5) or MMSarbianExt.\nYou will need to remove them for Module Manager and the mods using it to work\nExit KSP and delete those files :\n" + String.Join("\n", badPaths.ToArray());
                PopupDialog.SpawnPopupDialog("Old versions of Module Manager", status, "OK", false, HighLogic.Skin);
                loaded = true;
                print("[ModuleManager] Old version of Module Manager present. Stopping");
                return;
            }

            Assembly currentAssembly = Assembly.GetExecutingAssembly();
            var eligible = from a in AssemblyLoader.loadedAssemblies
                           let ass = a.assembly
                           where ass.GetName().Name == currentAssembly.GetName().Name
                           orderby ass.GetName().Version descending, a.path ascending
                           select a;

            // Elect the newest loaded version of MM to process all patch files.
            // If there is a newer version loaded then don't do anything
            // If there is a same version but earlier in the list, don't do anything either.
            if (eligible.First().assembly != currentAssembly)
            {
                loaded = true;
                print("[ModuleManager] version " + currentAssembly.GetName().Version + " at " + currentAssembly.Location + " lost the election");
                return;
            }
            else
            {
                string candidates = "";
                foreach (AssemblyLoader.LoadedAssembly a in eligible)
                    if (currentAssembly.Location != a.path)
                        candidates += "Version " + a.assembly.GetName().Version + " " + a.path + " " + "\n";
                if (candidates.Length > 0)
                    print("[ModuleManager] version " + currentAssembly.GetName().Version + " at " + currentAssembly.Location + " won the election against\n" + candidates);
            }
            #endregion

            #region Excluding directories
            // Build a list of subdirectory that won't be processed
            List<String> excludePaths = new List<string>();

            foreach (UrlDir.UrlConfig mod in GameDatabase.Instance.root.AllConfigs)
            {
                if (mod.name == "MODULEMANAGER[LOCAL]")
                {
                    string fullpath = mod.url.Substring(0, mod.url.LastIndexOf('/'));
                    string excludepath = fullpath.Substring(0, fullpath.LastIndexOf('/'));
                    excludePaths.Add(excludepath);
                    print("excludepath: " + excludepath);
                }
            }
            if (excludePaths.Any())
                print("[ModuleManager] will not procces patch in these subdirectories:\n" + String.Join("\n", excludePaths.ToArray()));
            #endregion 

            #region List of mods
            List<AssemblyName> modsWithDup = AssemblyLoader.loadedAssemblies.Select(a => (a.assembly.GetName())).ToList();

            mods = new List<AssemblyName>();

            foreach (AssemblyName a in modsWithDup)
            {
                if (!mods.Any(m => m.Name == a.Name))
                    mods.Add(a);
            }

            string modlist = "compiling list of loaded mods...\nMod DLLs found:\n";
            foreach (AssemblyName mod in mods)
            {
                modlist += "  " + mod.Name + " v" + mod.Version.ToString() + "\n";
            }
            modlist += "Non-DLL mods added:";
            foreach (UrlDir.UrlConfig cfgmod in GameDatabase.Instance.root.AllConfigs)
            {
                if (cfgmod.type[0] == '@' || (cfgmod.type[0] == '$'))
                {
                    string name = RemoveWS(cfgmod.name);
                    if (name.Contains(":FOR["))
                    { // check for FOR[] blocks that don't match loaded DLLs and add them to the pass list

                        string dependency = name.Substring(name.IndexOf(":FOR[") + 5);
                        dependency = dependency.Substring(0, dependency.IndexOf(']'));
                        if (mods.Find(a => RemoveWS(a.Name.ToUpper()).Equals(RemoveWS(dependency.ToUpper()))) == null)
                        { // found one, now add it to the list.
                            AssemblyName newMod = new AssemblyName(dependency);
                            newMod.Name = dependency;
                            mods.Add(newMod);
                            modlist += "\n  " + dependency;
                        }
                    }
                }
            }
            log(modlist);
            #endregion

            #region Check Needs
            // Do filtering with NEEDS 
            print("[ModuleManager] Checking NEEDS.");

            CheckNeeds(excludePaths);
            #endregion

            #region Applying patches
            // :First node (and any node without a :pass)
            ApplyPatch(excludePaths, ":FIRST");

            foreach (AssemblyName mod in mods)
            {
                string upperModName = mod.Name.ToUpper();
                ApplyPatch(excludePaths, ":BEFORE[" + upperModName + "]");
                ApplyPatch(excludePaths, ":FOR[" + upperModName + "]");
                ApplyPatch(excludePaths, ":AFTER[" + upperModName + "]");
            }

            // :Final node
            ApplyPatch(excludePaths, ":FINAL");

            PurgeUnused(excludePaths);
            #endregion

            #region Logging
            if (errorCount > 0)
                foreach (String file in errorFiles.Keys)
                    errors += errorFiles[file] + " error" + (errorFiles[file] > 1 ? "s" : "") + " in GameData/" + file + "\n";


            status = "ModuleManager: " 
                + needsUnsatisfiedCount + " unsatisfied need" + (needsUnsatisfiedCount != 1 ? "s" : "") 
                + ", " + patchCount + " patch" + (patchCount != 1 ? "es" : "") + " applied";
            if(errorCount > 0)
                status += ", found " + errorCount + " error" + (errorCount != 1 ? "s" : "");

            print("[ModuleManager] " + status + "\n" + errors);

            loaded = true;
            #endregion

#if DEBUG
            RunTestCases();
#endif
        }
        #endregion

        #region Needs checking
        private void CheckNeeds(List<String> excludePaths)
        {
            // Check the NEEDS parts first.
            foreach (UrlDir.UrlConfig mod in GameDatabase.Instance.root.AllConfigs.ToArray())
            {
                try
                {
                    if (IsPathInList(mod.url, excludePaths))
                        continue;

                    if (mod.type.Contains(":NEEDS["))
                    {
                        mod.parent.configs.Remove(mod);
                        string type = mod.type;

                        if (!CheckNeeds(ref type))
                        {
                            print("[ModuleManager] Deleting Node in file " + mod.parent.url + " subnode: " + mod.type + " as it can't satisfy its NEEDS");
                            needsUnsatisfiedCount++;
                            continue;
                        }

                        ConfigNode copy = new ConfigNode(type);
                        ShallowCopy(mod.config, copy);
                        mod.parent.configs.Add(new UrlDir.UrlConfig(mod.parent, copy));
                    }

                    // Recursivly check the contents
                    CheckNeeds(mod.config, mod.parent.url, new List<string>() { mod.type });
                }
                catch (Exception ex)
                {
                    print("[ModuleManager] Exception while checking needs : " + mod.url + "\n" + ex.ToString());
                }
            }
        }

        private void CheckNeeds(ConfigNode subMod, string url, List<string> path)
        {
            try
            {
                path.Add(subMod.name + "[" + subMod.GetValue("name") + "]");

                bool needsCopy = false;
                ConfigNode copy = new ConfigNode();
                for (int i = 0; i < subMod.values.Count; ++i)
                {
                    ConfigNode.Value val = subMod.values[i];
                    string name = val.name;
                    if (CheckNeeds(ref name))
                        copy.AddValue(name, val.value);
                    else
                    {
                        needsCopy = true;
                        print("[ModuleManager] Deleting value in file: " + url + " subnode: " + string.Join("/", path.ToArray()) + " value: " + val.name + " = " + val.value + " as it can't satisfy its NEEDS");
                        needsUnsatisfiedCount++;
                    }
                }

                for (int i = 0; i < subMod.nodes.Count; ++i)
                {
                    ConfigNode node = subMod.nodes[i];
                    string name = node.name;
                    if (CheckNeeds(ref name))
                    {
                        node.name = name;
                        CheckNeeds(node, url, path);
                        copy.AddNode(node);
                    }
                    else
                    {
                        needsCopy = true;
                        print("[ModuleManager] Deleting node in file: " + url + " subnode: " + string.Join("/", path.ToArray()) + "/" + node.name + " as it can't satisfy its NEEDS");
                        needsUnsatisfiedCount++;
                    }
                }

                if (needsCopy)
                    ShallowCopy(copy, subMod);
            }
            finally
            {
                path.RemoveAt(path.Count - 1);
            }
        }

        /// <summary>
        /// Returns true if needs are satisfied.
        /// </summary>
        private bool CheckNeeds(ref string name)
        {
            if (name == null)
                return true;

            int idxStart = name.IndexOf(":NEEDS[");
            if (idxStart < 0)
                return true;
            int idxEnd = name.IndexOf(']', idxStart + 7);
            string needsString = name.Substring(idxStart + 7, idxEnd - idxStart - 7).ToUpper();

            name = name.Substring(0, idxStart) + name.Substring(idxEnd + 1);

            // Check to see if all the needed dependencies are present.
            foreach (string andDependencies in needsString.Split(',', '&'))
            {
                bool orMatch = false;
                foreach (string orDependency in andDependencies.Split('|'))
                {
                    if (orDependency.Length == 0)
                        continue;

                    bool not = orDependency[0] == '!';
                    string toFind = not ? orDependency.Substring(1) : orDependency;
                    bool found = mods.Find(a => a.Name.ToUpper() == toFind) != null;

                    if (not == !found)
                    {
                        orMatch = true;
                        break;
                    }
                }
                if (!orMatch)
                    return false;
            }

            return true;
        }

        private void PurgeUnused(List<string> excludePaths)
        {
            foreach (UrlDir.UrlConfig mod in GameDatabase.Instance.root.AllConfigs.ToArray())
            {
                if (IsPathInList(mod.url, excludePaths))
                    continue;

                int lastErrorCount = errorCount;

                string name = RemoveWS(mod.type);

                if (name[0] == '@' || (name[0] == '$') || (name[0] == '!'))
                {

                    mod.parent.configs.Remove(mod);
                }
            }
        }

        #endregion

        #region Applying Patches
        // Apply patch to all relevent nodes
        public void ApplyPatch(List<String> excludePaths, string Stage)
        {
            print("[ModuleManager] " + Stage + (Stage == ":FIRST" ? " (default) pass" : " pass"));

            foreach (UrlDir.UrlConfig mod in GameDatabase.Instance.root.AllConfigs.ToArray())
            {
                int lastErrorCount = errorCount;

                try
                {
                    string name = RemoveWS(mod.type);

                    if (name[0] == '@' || (name[0] == '$') || (name[0] == '!'))
                    {
                        if (!IsBraquetBalanced(mod.type))
                        {
                            print("[ModuleManager] Skipping a patch with unbalanced square brackets or a space (replace them with a '?') :\n" + mod.name + "\n");
                            errorCount++;
                            // And remove it so it's not tried anymore
                            mod.parent.configs.Remove(mod);
                            continue;
                        }

                        // Ensure the stage is correct
                        string upperName = name.ToUpper();

                        int stageIdx = upperName.IndexOf(Stage);
                        if (stageIdx >= 0)
                        {
                            name = name.Substring(0, stageIdx) + name.Substring(stageIdx + Stage.Length);
                        }
                        else if (!(Stage == ":FIRST"
                                    && !upperName.Contains(":BEFORE[")
                                    && !upperName.Contains(":FOR[")
                                    && !upperName.Contains(":AFTER[")
                                    && !upperName.Contains(":FINAL")))
                        {
                            continue;
                        }

                        // TODO: do we want to ensure there's only one phase specifier?

                        try
                        {
                            char[] sep = new char[] { '[', ']' };
                            string cond = "";

                            if (upperName.Contains(":HAS["))
                            {
                                int start = upperName.IndexOf(":HAS[");
                                cond = name.Substring(start + 5, name.LastIndexOf(']') - start - 5);
                                name = name.Substring(0, start);
                            }

                            string[] splits = name.Split(sep, 3);
                            string pattern = splits.Length > 1 ? splits[1] : null;
                            string type = splits[0].Substring(1);

                            foreach (UrlDir.UrlConfig url in GameDatabase.Instance.root.AllConfigs.ToArray())
                            {
                                if (url.type == type
                                    && WildcardMatch(url.name, pattern)
                                    && CheckCondition(url.config, cond)
                                    && !IsPathInList(mod.url, excludePaths)
                                    )
                                {
                                    if (mod.type[0] == '@')
                                    {
                                        print("[ModuleManager] Applying node " + mod.url + " to " + url.url);
                                        patchCount++;
                                        url.config = ModifyNode(url.config, mod.config);
                                    }
                                    else if (mod.type[0] == '$')
                                    {
                                        ConfigNode clone = ModifyNode(url.config, mod.config);
                                        if (url.config.name != mod.name)
                                        {
                                            print("[ModuleManager] Copying Node " + url.config.name + " into " + clone.name);
                                            url.parent.configs.Add(new UrlDir.UrlConfig(url.parent, clone));
                                        }
                                        else
                                        {
                                            errorCount++;
                                            print("[ModuleManager] Error while processing " + mod.config.name + " the copy needs to have a different name than the parent (use @name = xxx)");
                                        }
                                    }
                                    else if (mod.type[0] == '!')
                                    {
                                        print("[ModuleManager] Deleting Node " + url.config.name);
                                        url.parent.configs.Remove(url);
                                    }
                                }
                            }
                        }
                        finally
                        {
                            // The patch was either run or has failed, in any case let's remove it from the database
                            mod.parent.configs.Remove(mod);
                        }
                    }
                }
                catch (Exception e)
                {
                    print("[ModuleManager] Exception while processing node : " + mod.url + "\n" + e.ToString());
                    mod.parent.configs.Remove(mod);
                }
                finally
                {
                    if (lastErrorCount < errorCount)
                        addErrorFiles(mod.parent, errorCount - lastErrorCount);
                }
            }
        }

        // ModifyNode applies the ConfigNode mod as a 'patch' to ConfigNode original, then returns the patched ConfigNode.
        // it uses FindConfigNodeIn(src, nodeType, nodeName, nodeTag) to recurse.
        public ConfigNode ModifyNode(ConfigNode original, ConfigNode mod)
        {
            ConfigNode newNode = DeepCopy(original);
            
            string vals = "[ModuleManager] modding values";
            foreach (ConfigNode.Value val in mod.values)
            {
                vals += "\n   " + val.name + "= " + val.value;
                
                string valName = val.name;

                if (valName[0] != '@' && valName[0] != '!' && valName[0] != '%')
                {
                    int index = int.MaxValue;
                    if (valName.Contains(",") && int.TryParse(valName.Split(',')[1], out index))
                    {
                        // In this case insert the value at position index (with the same node names)
                        valName = valName.Split(',')[0];

                        string [] oldValues = newNode.GetValues(valName); 
                        if (index < oldValues.Length) 
                        {
                            newNode.RemoveValues(valName);
                            int i = 0;
                            for(; i < index; ++i) 
                                newNode.AddValue(valName, oldValues[i]);
                            newNode.AddValue(valName, val.value);
                            for(; i < oldValues.Length; ++i)
                                newNode.AddValue(valName, oldValues[i]);
                            continue;
                        }
                    }

                    newNode.AddValue(valName, val.value);
                }
                else
                {  // Parsing: 
                    // Format is @key = value or @key *= value or @key += value or @key -= value 
                    // or @key,index = value or @key,index *= value or @key,index += value or @key,index -= value 
                    valName = valName.Substring(1);
                    int index = 0;
                    if (valName.Contains(","))
                    {
                        int.TryParse(valName.Split(',')[1], out index);
                        valName = valName.Split(',')[0];
                    }

                    if (val.name[0] == '@')
                    {
                        string value = val.value;
                        char op = ' ';
                        if (valName.EndsWith(" *")) // @key *= val
                            op = '*';
                        else if (valName.EndsWith(" +")) // @key += val
                            op = '+';
                        else if (valName.EndsWith(" -")) // @key -= val
                            op = '-';
                        else if (valName.EndsWith(" ^"))
                            op = '^';

                        if (op != ' ')
                        {
                            valName = valName.Split(' ')[0];

                            string ovalue = original.GetValue(valName, index);
                            if (ovalue != null)
                            {
                                double s, os;
                                if (op == '^')
                                {
                                    try
                                    {
                                        string[] split = value.Split(value[0]);
                                        value = Regex.Replace(ovalue, split[1], split[2]);
                                    }
                                    catch (Exception ex)
                                    {
                                        print("[ModuleManager] Failed to do a regexp replacement: " + mod.name + " : original value=\"" + ovalue + "\" regexp=\"" + value + "\" \nNote - to use regexp, the first char is used to subdivide the string (much like sed)\n" + ex.ToString());
                                    }
                                }
                                else if (double.TryParse(value, out s) && double.TryParse(ovalue, out os))
                                {
                                    if (op == '*')
                                        value = (s * os).ToString();
                                    else if (op == '+')
                                        value = (s + os).ToString();
                                    else if (op == '-')
                                        value = (s - os).ToString();
                                }
                                vals += ": " + ovalue + " -> " + value;
                            }

                        }

                        newNode.SetValue(valName, value, index);
                    }
                    else if (val.name[0] == '!')
                        newNode.RemoveValues(valName);
                    else if (val.name[0] == '%')
                    {
                        newNode.RemoveValues(valName);
                        newNode.AddValue(valName, val.value);
                    }
                }

            }
            //print(vals);

            foreach (ConfigNode subMod in mod.nodes)
            {
                subMod.name = RemoveWS(subMod.name);

                if (!IsBraquetBalanced(subMod.name))
                {
                    print("[ModuleManager] Skipping a patch subnode with unbalanced square brackets or a space (replace them with a '?') in " + mod.name + " : \n" + subMod.name + "\n");
                    errorCount++;
                    continue;
                }

                char cmd = subMod.name[0];
                string name = subMod.name;

                if (cmd != '@' && cmd != '!' && cmd != '%' && cmd != '$') 
                {
                    int index = int.MaxValue;
                    if (name.Contains(",") && int.TryParse(name.Split(',')[1], out index))
                    {
                        // In this case insert the value at position index (with the same node names)
                        subMod.name = name = name.Split(',')[0];

                        InsertNode(newNode, subMod, index);
                    }
                    else
                    {
                        newNode.AddNode(subMod);
                    }
                }
                else
                {
                    string cond = "";
                    string tag = "";
                    string nodeType, nodeName;
                    int index = 0;
                    string msg = "";

                    List<ConfigNode> subNodes = new List<ConfigNode>();
                    //if (subMod.name[0] == '@' && subMod.name[0] != '%')
                    //    subNode = null;

                    // three ways to specify:
                    // NODE,n will match the nth node (NODE is the same as NODE,0)
                    // NODE,* will match ALL nodes
                    // NODE:HAS[condition] will match ALL nodes with condition
                    if (name.Contains(":HAS["))
                    {
                        int start = name.IndexOf(":HAS[");
                        cond = name.Substring(start + 5, name.LastIndexOf(']') - start - 5);
                        name = name.Substring(0, start);
                    }
                    else if (name.Contains(","))
                    {
                        tag = name.Split(',')[1];
                        name = name.Split(',')[0];
                        int.TryParse(tag, out index);
                    }

                    if (name.Contains("["))
                    {
                        // format @NODETYPE[Name] {...} 
                        // or @NODETYPE[Name, index] {...} 
                        nodeType = name.Substring(1).Split('[')[0];
                        nodeName = name.Split('[')[1].Replace("]", "");
                    }
                    else
                    {
                        // format @NODETYPE {...} or ! instead of @
                        nodeType = name.Substring(1);
                        nodeName = null;
                    }


                    if (tag == "*" || cond.Length > 0)
                    { // get ALL nodes
                        if (cmd == '%')
                        {
                            msg += "  cannot wildcard a % node: " + subMod.name + "\n";
                        }
                        else
                        {
                            ConfigNode n;
                            do
                            {
                                n = FindConfigNodeIn(newNode, nodeType, nodeName, index++);
                                if (n != null && CheckCondition(n, cond)) subNodes.Add(n);
                            } while (n != null && index < newNode.nodes.Count);
                        }
                    }
                    else
                    { // just get one node
                        ConfigNode n = FindConfigNodeIn(newNode, nodeType, nodeName, index);
                        if (n != null) subNodes.Add(n);
                    }

                    if (cmd == '@' || cmd == '!' || cmd == '$')
                    { // find each original subnode to modify, modify it and add the modified.

                        if (subNodes.Count == 0)   // no nodes to modify!
                            msg += "  Could not find node(s) to modify: " + subMod.name + "\n";

                        foreach (ConfigNode subNode in subNodes)
                        {
                            msg += "  Applying subnode " + subMod.name + "\n";
                            ConfigNode newSubNode;
                            switch(cmd) {
                                case '@':
                                    // @ edits in place
                                    newSubNode = ModifyNode(subNode, subMod);
                                    subNode.ClearData();
                                    newSubNode.CopyTo(subNode);
                                    break;
                                case '!':
                                    // Delete the node
                                    newNode.nodes.Remove(subNode);
                                    break;
                                case '$':
                                    // Copy the node
                                    newSubNode = ModifyNode(subNode, subMod);
                                    newNode.nodes.Add(newSubNode);
                                    break;
                            }
                        }
                    }
                    else // cmd == '%'
                    {
                        // if the original exists modify it
                        if (subNodes.Count > 0)
                        {
                            msg += "  Applying subnode " + subMod.name + "\n";
                            ConfigNode newSubNode = ModifyNode(subNodes[0], subMod);
                            subNodes[0].ClearData();
                            newSubNode.CopyTo(subNodes[0]);
                        }
                        else
                        { // if not add the mod node without the % in its name                            
                            msg += "  Adding subnode " + subMod.name + "\n";
                            string newType;
                            string newName;
                            if (subMod.name.Contains("["))
                            {
                                newType = subMod.name.Substring(1).Split('[')[0];
                                newName = subMod.name.Split('[')[1].Replace("]", "");
                            }
                            else
                            {
                                newType = subMod.name.Substring(1);
                                newName = null;
                            }

                            ConfigNode copy = new ConfigNode(newType);

                            if (newName != null)
                                copy.AddValue("name", newName);

                            ConfigNode newSubNode = ModifyNode(copy, subMod);
                            newNode.nodes.Add(newSubNode);
                        }
                    }
                    //print(msg);
                }
            }
            return newNode;
        }
        #endregion

        #region Sanity checking & Utility functions

        public static bool IsBraquetBalanced(String str)
        {
            Stack<char> stack = new Stack<char>();

            char c;
            for (int i = 0; i < str.Length; i++)
            {
                c = str[i];
                if (c == '[')
                    stack.Push(c);
                else if (c == ']')
                    if (stack.Count == 0)
                        return false;
                    else if (stack.Peek() == '[')
                        stack.Pop();
                    else
                        return false;
            }
            return stack.Count == 0;
        }

        public static string RemoveWS(string withWhite)
        {   // Removes ALL whitespace of a string.
            return new string(withWhite.ToCharArray().Where(c => !Char.IsWhiteSpace(c)).ToArray());
        }

        public bool IsPathInList(string modPath, List<String> pathList)
        {
            return pathList.Any(modPath.StartsWith);
        }
        #endregion

        #region Condition checking
        // Split condiction while not getting lost in embeded brackets
        public static List<string> SplitCondition(string cond)
        {
            cond = RemoveWS(cond) + ",";
            List<string> conds = new List<string>();
            int start = 0;
            int level = 0;
            for (int end = 0; end < cond.Length; end++)
            {
                if (cond[end] == ',' && level == 0)
                {
                    conds.Add(cond.Substring(start, end - start));
                    start = end + 1;
                }
                else if (cond[end] == '[')
                    level++;
                else if (cond[end] == ']')
                    level--;
            }
            return conds;
        }

        public static bool CheckCondition(ConfigNode node, string conds)
        {
            conds = RemoveWS(conds);
            if (conds.Length == 0)
                return true;

            List<string> condsList = SplitCondition(conds);

            if (condsList.Count == 1)
            {
                conds = condsList[0];



                string remainCond = "";
                if (conds.Contains("HAS["))
                {
                    int start = conds.IndexOf("HAS[") + 4;
                    remainCond = conds.Substring(start, condsList[0].LastIndexOf(']') - start);
                    conds = conds.Substring(0, start - 5);
                }

                char[] sep = new char[] { '[', ']' };
                string[] splits = conds.Split(sep, 3);
                string type = splits[0].Substring(1);
                string name = splits.Length > 1 ? splits[1] : null;

                switch (conds[0])
                {
                    case '@':
                    case '!':
                        // @MODULE[ModuleAlternator] or !MODULE[ModuleAlternator]
                        bool not = (conds[0] == '!');
                        ConfigNode subNode = ConfigManager.FindConfigNodeIn(node, type, name);
                        if (subNode != null)
                            return not ^ CheckCondition(subNode, remainCond);
                        return not ^ false;
                    case '#':
                        // #module[Winglet]
                        if (node.HasValue(type) && node.GetValue(type).Equals(name))
                            return CheckCondition(node, remainCond);
                        return false;
                    case '~':
                        // ~breakingForce[]  breakingForce is not present
                        if (!(node.HasValue(type)))
                            return CheckCondition(node, remainCond);
                        return false;
                    default:
                        return false;
                }
            }
            return condsList.TrueForAll(c => CheckCondition(node, c));
        }

        public static bool WildcardMatch(String s, String wildcard)
        {
            if (wildcard == null) return true;
            String pattern = "^" + Regex.Escape(wildcard).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
            Regex regex;
            regex = new Regex(pattern);

            return (regex.IsMatch(s));
        }
        #endregion

        #region Config Node Utilities
        private static void InsertNode(ConfigNode newNode, ConfigNode subMod, int index)
        {
            string modName = subMod.name;

            ConfigNode[] oldValues = newNode.GetNodes(modName);
            if (index < oldValues.Length)
            {
                newNode.RemoveNodes(modName);
                int i = 0;
                for (; i < index; ++i)
                    newNode.AddNode(oldValues[i]);
                newNode.AddNode(subMod);
                for (; i < oldValues.Length; ++i)
                    newNode.AddNode(oldValues[i]);
            }
            else
            {
                newNode.AddNode(subMod);
            }
        }

        private static void ShallowCopy(ConfigNode from, ConfigNode to)
        {
            to.ClearData();
            foreach (ConfigNode.Value value in from.values)
                to.values.Add(value);
            foreach (ConfigNode node in from.nodes)
                to.nodes.Add(node);
        }

        private static ConfigNode DeepCopy(ConfigNode from)
        {
            ConfigNode to = new ConfigNode(from.name);
            foreach (ConfigNode.Value value in from.values)
                to.AddValue(value.name, value.value);
            foreach (ConfigNode node in from.nodes)
            {
                ConfigNode newNode = DeepCopy(node);
                to.nodes.Add(newNode);
            }
            return to;
        }

        //FindConfigNodeIn finds and returns a ConfigNode in src of type nodeType.
        //If nodeName is not null, it will only find a node of type nodeType with the value name=nodeName.
        //If nodeTag is not null, it will only find a node of type nodeType with the value name=nodeName and tag=nodeTag.
        public static ConfigNode FindConfigNodeIn(ConfigNode src, string nodeType,
                                                   string nodeName = null, int index = 0)
        {
            int found = 0;
            foreach (ConfigNode n in src.GetNodes(nodeType))
            {
                if (nodeName == null)
                {
                    if (index == found)
                        return n;
                    else
                        found++;
                }
                else if (n.HasValue("name") && WildcardMatch(n.GetValue("name"), nodeName))
                {
                    if (found == index)
                    {
                        return n;
                    }
                    else
                    {
                        found++;
                    }
                }
            }
            return null;
        }

        private static bool CompareRecursive(ConfigNode expectNode, ConfigNode gotNode)
        {
            if (expectNode.values.Count != gotNode.values.Count || expectNode.nodes.Count != gotNode.nodes.Count)
                return false;
            for (int i = 0; i < expectNode.values.Count; ++i)
            {
                ConfigNode.Value eVal = expectNode.values[i];
                ConfigNode.Value gVal = gotNode.values[i];
                if (eVal.name != gVal.name || eVal.value != gVal.value)
                    return false;
            }
            for (int i = 0; i < expectNode.nodes.Count; ++i)
            {
                ConfigNode eNode = expectNode.nodes[i];
                ConfigNode gNode = gotNode.nodes[i];
                if (!CompareRecursive(eNode, gNode))
                    return false;
            }
            return true;
        }

        #endregion

        #region logging

        public void addErrorFiles(UrlDir.UrlFile file, int n = 1)
        {
            string key = file.url + "." + file.fileExtension;
            if (key[0] == '/')
                key = key.Substring(1);
            if (!errorFiles.ContainsKey(key))
                errorFiles.Add(key, n);
            else
                errorFiles[key] = errorFiles[key] + n;

        }

        public static void log(String s)
        {
            print("[ModuleManager] " + s);
        }
        #endregion

        #region GUI stuff.

        public void OnGUI()
        {
            if (HighLogic.LoadedScene != GameScenes.LOADING)
                return;

            var centeredStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
            centeredStyle.alignment = TextAnchor.UpperCenter;
            centeredStyle.fontSize = 16;
            Vector2 sizeOfLabel = centeredStyle.CalcSize(new GUIContent(status));
            GUI.Label(new Rect(Screen.width / 2 - (sizeOfLabel.x / 2), Mathf.FloorToInt(0.8f * Screen.height), sizeOfLabel.x, sizeOfLabel.y), status, centeredStyle);

            if (errorCount > 0)
            {
                var errorStyle = new GUIStyle(GUI.skin.GetStyle("Label"));
                errorStyle.alignment = TextAnchor.UpperLeft;
                errorStyle.fontSize = 16;
                Vector2 sizeOfError = errorStyle.CalcSize(new GUIContent(errors));
                GUI.Label(new Rect(Screen.width / 2 - (sizeOfLabel.x / 2), Mathf.FloorToInt(0.8f * Screen.height) + sizeOfLabel.y, sizeOfError.x, sizeOfError.y), errors, errorStyle);

            }

        }


        #endregion

        #region Tests

        private void RunTestCases()
        {
            print("[ModuleManager] Running tests...");

            // Do MM testcases
            foreach (UrlDir.UrlConfig expect in GameDatabase.Instance.GetConfigs("MMTEST_EXPECT"))
            {
                // So for each of the expects, we expect all the configs before that node to match exactly.
                UrlDir.UrlFile parent = expect.parent;
                if (parent.configs.Count != expect.config.CountNodes + 1)
                {
                    print("[ModuleManager] Test " + parent.name + " failed as expecte number of nodes differs expected:" + expect.config.CountNodes + " found: " + parent.configs.Count);
                    for (int i = 0; i < parent.configs.Count; ++i)
                    {
                        print(parent.configs[i].config);
                    }
                    continue;
                }
                for (int i = 0; i < expect.config.CountNodes; ++i)
                {
                    ConfigNode gotNode = parent.configs[i].config;
                    ConfigNode expectNode = expect.config.nodes[i];
                    if (!CompareRecursive(expectNode, gotNode))
                    {
                        print("[ModuleManager] Test " + parent.name + "[" + i + "] failed as expected output and actual output differ.\nexpected:\n" + expectNode + "\nActually got:\n" + gotNode);
                    }
                }
                // Purge the tests
                parent.configs.Clear();
            }
            print("[ModuleManager] tests complete.");
        }

        #endregion
    }

    /// <summary>
    /// KSPAddon with equality checking using an additional type parameter. Fixes the issue where AddonLoader prevents multiple start-once addons with the same start scene.
    /// </summary>
    public class KSPAddonFixed : KSPAddon, IEquatable<KSPAddonFixed>
    {
        private readonly Type type;

        public KSPAddonFixed(KSPAddon.Startup startup, bool once, Type type)
            : base(startup, once)
        {
            this.type = type;
        }

        public override bool Equals(object obj)
        {
            if (obj.GetType() != this.GetType()) { return false; }
            return Equals((KSPAddonFixed)obj);
        }

        public bool Equals(KSPAddonFixed other)
        {
            if (this.once != other.once) { return false; }
            if (this.startup != other.startup) { return false; }
            if (this.type != other.type) { return false; }
            return true;
        }

        public override int GetHashCode()
        {
            return this.startup.GetHashCode() ^ this.once.GetHashCode() ^ this.type.GetHashCode();
        }
    }
}
