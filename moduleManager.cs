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
        //FindConfigNodeIn finds and returns a ConfigNode in src of type nodeType.
        //If nodeName is not null, it will only find a node of type nodeType with the value name=nodeName.
        //If nodeTag is not null, it will only find a node of type nodeType with the value name=nodeName and tag=nodeTag.
        public static ConfigNode FindConfigNodeIn(ConfigNode src, string nodeType,
                                                   string nodeName = null, int index = 0)
        {
#if DEBUG
			if (nodeTag == null)
				print ("Searching node for " + nodeType + "[" + nodeName + "]");
			else
				print ("Searching node for " + nodeType + "[" + nodeName + "," + nodeTag + "]");
#endif
            int found = 0;
            foreach (ConfigNode n in src.GetNodes(nodeType)) {
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
#if DEBUG
                print ("found node " + found.ToString() + "!");
#endif
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
                    else if (stack.Peek() == '[' )
                        stack.Pop();
                    else
                        return false;
            }
            return stack.Count == 0;
        }

        // Added that to prevent a crash in KSP 'CopyTo' when a subnode has an empty name like
        // Like a pair of curly bracket without a name before them
        public static bool IsSane(ConfigNode node)
        {
            if (node.name.Length == 0)
                return false;
            foreach (ConfigNode subnode in node.nodes)
                if (!IsSane(subnode))
                    return false;
            return true;
        }

        public static string RemoveWS(string withWhite)
        {   // Removes ALL whitespace of a string.
            return new string(withWhite.ToCharArray().Where(c => !Char.IsWhiteSpace(c)).ToArray());
        }

        // ModifyNode applies the ConfigNode mod as a 'patch' to ConfigNode original, then returns the patched ConfigNode.
        // it uses FindConfigNodeIn(src, nodeType, nodeName, nodeTag) to recurse.
        public static ConfigNode ModifyNode(ConfigNode original, ConfigNode mod)
        {
            if (!IsSane(original) || !IsSane(mod))
            {
                print("[ModuleManager] A node has an empty name. Skipping it. Original: " + original.name);
                return original;
            }

            ConfigNode newNode = original.CreateCopy();

            string vals = "[ModuleManager] modding values";
            foreach (ConfigNode.Value val in mod.values)
            {
                vals += "\n   " + val.name + "= " + val.value;
                if (val.name[0] != '@' && val.name[0] != '!' && val.name[0] != '%')
                    newNode.AddValue(val.name, val.value);
                else
                {  // Parsing: 
                   // Format is @key = value or @key *= value or @key += value or @key -= value 
                   // or @key,index = value or @key,index *= value or @key,index += value or @key,index -= value 
                    string valName = val.name.Substring(1);
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
                        if (val.name.Contains(" *")) // @key *= val
                            op = '*';
                        else if (val.name.Contains(" +")) // @key += val
                            op = '+';
                        else if (val.name.Contains(" -")) // @key -= val
                            op = '-';
                        
                        if (op != ' ')
                        {
                            valName = valName.Split(' ')[0];
                                                        
                            string ovalue = original.GetValue(valName, index);
                            if(ovalue != null)
                            {
                                double s, os;
                                if (double.TryParse(value, out s) && double.TryParse(ovalue, out os))
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
            print(vals);

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
                if (cmd != '@' && cmd != '!' && cmd != '%' && cmd != '$')
                    newNode.AddNode(subMod);
                else
                {
                    string name = subMod.name;
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
                        if(n != null) subNodes.Add(n);
                    }

                    if (cmd == '@' || cmd == '!' || cmd == '$')
                    { // find each original subnode to modify, modify it and add the modified.

                        if (subNodes.Count == 0)   // no nodes to modify!
                            msg += "  Could not find node(s) to modify: " + subMod.name + "\n";

                        foreach (ConfigNode subNode in subNodes)
                        {
                            msg += "  Applying subnode " + subMod.name + "\n";
                            if (cmd != '$')
                            {// @ and ! both remove the original
                                newNode.nodes.Remove(subNode);
                            }
                            if (cmd != '!')
                            { // @ and $ both add the modified
                                ConfigNode newSubNode = ModifyNode(subNode, subMod);
                                newNode.nodes.Add(newSubNode);
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
                            newNode.nodes.Remove(subNodes[0]);
                            newNode.nodes.Add(newSubNode);
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
                    print(msg);
                }
            }
            return newNode;
        }

        public static List<UrlDir.UrlConfig> AllConfigsStartingWith(string match)
        {
            List<UrlDir.UrlConfig> nodes = new List<UrlDir.UrlConfig>();
            foreach (UrlDir.UrlConfig url in GameDatabase.Instance.root.AllConfigs) {
                if (url.type.StartsWith(match))
                    url.config.name = url.type;
                nodes.Add(url);
            }
            return nodes;
        }

        bool loaded = false;

        static int patchCount = 0;
        static int errorCount = 0;
        List<AssemblyName> mods;

        public void OnGUI()
        {
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

            // Check for old version and MMSarbianExt
            var oldMM = AssemblyLoader.loadedAssemblies.Where(a => a.assembly.GetName().Name == Assembly.GetExecutingAssembly().GetName().Name).Where(a => a.assembly.GetName().Version.CompareTo(new System.Version(1, 6)) == -1);
            var oldAssemblies = oldMM.Concat(AssemblyLoader.loadedAssemblies.Where(a => a.assembly.GetName().Name == "MMSarbianExt"));
            if (oldAssemblies.Any())
            {
                var badPaths = oldAssemblies.Select(a => a.path).Select(p => Uri.UnescapeDataString(new Uri(Path.GetFullPath(KSPUtil.ApplicationRootPath)).MakeRelativeUri(new Uri(p)).ToString().Replace('/', Path.DirectorySeparatorChar)));
                PopupDialog.SpawnPopupDialog("Old versions of Module Manager", "You have old versions of Module Manager (older than 1.5) or MMSarbianExt.\nYou will need to remove them for Module Manager to work\nExit KSP and delete those files :\n" + String.Join("\n", badPaths.ToArray()), "OK", false, HighLogic.Skin);
                loaded = true;
                print("[ModuleManager] Old version of Module Manager present. Stopping");
                return;
            }

            Assembly currentAssembly = Assembly.GetExecutingAssembly();
            var eligible = AssemblyLoader.loadedAssemblies.Where(a => a.assembly.GetName().Name == currentAssembly.GetName().Name);

            //print("[ModuleManager] starting \n" + String.Join("\n", eligible.Select(a => a.path).ToArray()));

            // Elect the newest loaded version of MM to process all patch files.
            // If there is a newer version loaded then don't do anything
            if (eligible.Any(a => 
                    a.assembly.GetName().Version.CompareTo(currentAssembly.GetName().Version) == 1
                    || a.assembly.Location.CompareTo(currentAssembly.Location) < 0)) {
                loaded = true;
                print("[ModuleManager] version " + currentAssembly.GetName().Version + " at " + currentAssembly.Location + " lost the election");
                return;
            } else {
                string candidates = "";
                foreach (AssemblyLoader.LoadedAssembly a in eligible)
                    if (currentAssembly.Location != a.path)
                        candidates += "Version " + a.assembly.GetName().Version + " " + a.path + " " + "\n";
                if (candidates.Length > 0)
                    print("[ModuleManager] version " + currentAssembly.GetName().Version + " at " + currentAssembly.Location + " won the election against\n" + candidates);
            }             

            // Build a list of subdirectory that won't be processed
            List<String> excludePaths = new List<string>();

            foreach (UrlDir.UrlConfig mod in GameDatabase.Instance.root.AllConfigs) {
                if (mod.name == "MODULEMANAGER[LOCAL]") {
                    string fullpath = mod.url.Substring(0, mod.url.LastIndexOf('/'));
                    string excludepath = fullpath.Substring(0, fullpath.LastIndexOf('/'));
                    excludePaths.Add(excludepath);
                    print("excludepath: " + excludepath);
                }
            }
            if (excludePaths.Any())
                print("[ModuleManager] will not procces patch in these subdirectories:\n" + String.Join("\n", excludePaths.ToArray()));

            patchCount = 0;

            List<AssemblyName> modsWithDup = AssemblyLoader.loadedAssemblies.Select(a => (a.assembly.GetName())).ToList();

            mods = new List<AssemblyName>();

            foreach (AssemblyName a in modsWithDup  )
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
                    if(name.Contains(":FOR["))
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
            
            // :First node (and any node without a :pass)
            ApplyPatch(excludePaths, ":FIRST");

            foreach(AssemblyName mod in mods)
            {
                ApplyPatch(excludePaths, ":BEFORE[" + mod.Name + "]");
                ApplyPatch(excludePaths, ":FOR[" + mod.Name + "]");
                ApplyPatch(excludePaths, ":AFTER[" + mod.Name + "]");
            }
            
            // :Final node
            ApplyPatch(excludePaths, ":FINAL");

            print("[ModuleManager] Applied " + patchCount + " patches and found " + errorCount + " errors" );
            loaded = true;

        }
        // Apply patch to all relevent nodes
        public void ApplyPatch(List<String> excludePaths, string Stage)
        {
            print("[ModuleManager] " + Stage + (Stage == ":FIRST" ? " (default) pass" : " pass"));
            foreach (UrlDir.UrlConfig mod in GameDatabase.Instance.root.AllConfigs) {
                if (mod.type[0] == '@' || (mod.type[0] == '$' )) {
                    try {
                        string name = RemoveWS(mod.name);
                        

                        string dependencies = "";
                        if(name.Contains(":NEEDS["))
                        {
                            dependencies = name.Substring(name.IndexOf(":NEEDS[") + 7).Replace("]", "");
                            name = name.Remove(name.IndexOf(":NEEDS["));
                        }
                        bool unresolvedDependencies = false;
                        if (dependencies.Length > 0)
                        {
                            foreach (string dependency in dependencies.Split(','))
                            {
                                string check = RemoveWS(dependency);
                                if(check[0] == '!' ^ (mods.Find(a => a.Name.ToUpper().Equals(check.ToUpper())) == null))
                                {
                                    
                                    unresolvedDependencies = true;
                                    if (
                                 (Stage == ":FIRST"
                                   && !name.ToUpper().Contains(":BEFORE[")
                                   && !name.ToUpper().Contains(":FOR[")
                                   && !name.ToUpper().Contains(":AFTER[")
                                   && !name.ToUpper().Contains(":FINAL")
                                 ) ^ name.ToUpper().EndsWith(Stage.ToUpper()))
                                    {
                                        print("[ModuleManager] node " + mod.url + " - " + check + " not found!");
                                    }
                                }

                            }
                        }

                        if (unresolvedDependencies)
                        {

                        }
                        else if (
                                 ( Stage == ":FIRST" 
                                   && !name.ToUpper().Contains(":BEFORE[") 
                                   && !name.ToUpper().Contains(":FOR[") 
                                   && !name.ToUpper().Contains(":AFTER[") 
                                   && !name.ToUpper().Contains(":FINAL") 
                                 ) ^ name.ToUpper().EndsWith(Stage.ToUpper()) ) 
                        {
                            char[] sep = new char[] { '[', ']' };                            
                            string cond = "";
                            if (name.Contains(Stage))
                                name = name.Substring(0, name.LastIndexOf(Stage));

                            if (name.Contains(":HAS["))
                            {
                                int start = name.IndexOf(":HAS[");
                                cond = name.Substring(start + 5, name.LastIndexOf(']') - start - 5);
                                name = name.Substring(0, start);
                            }

                            string[] splits = name.Split(sep, 3);
                            string pattern = splits.Length>1?splits[1]:null;
                            string type = splits[0].Substring(1);

                            if (!IsBraquetBalanced(mod.name))
                            {
                                print("[ModuleManager] Skipping a patch with unbalanced square brackets or a space (replace them with a '?') :\n" + mod.name + "\n");
                                errorCount++;
                                continue;
                            }

                            foreach (UrlDir.UrlConfig url in GameDatabase.Instance.root.AllConfigs) {
                                if (url.type == type
                                    && WildcardMatch(url.name, pattern)
                                    && CheckCondition(url.config, cond)
                                    && !IsPathInList(mod.url, excludePaths)
                                    ) {
                                        if (mod.type[0] == '@') {
                                            print("[ModuleManager] Applying node " + mod.url + " to " + url.url);
                                            patchCount++;
                                            url.config = ConfigManager.ModifyNode(url.config, mod.config);
                                        }
                                        else { // type = $
                                            // Here we would duplicate an Node if we had the mean to do it
                                            //ConfigNode newNode = ConfigManager.ModifyNode(url.config, mod.config);
                                            //UrlDir.UrlConfig newurl = new UrlDir.UrlConfig(mod.parent, newNode);
                                            //print("[ModuleManager] Copying Node " + newurl.url + " " + newurl.name);
                                        }                                    
                                }
                            }
                        }
                    } catch (Exception e) {
                        print("[ModuleManager] Exception while processing node : " + mod.url + "\n" + e.ToString());
                    }
                }
            }
        }

        public bool IsPathInList(string modPath, List<String> pathList)
        {
            return pathList.Any(modPath.StartsWith);
        }
        // Split condiction while not getting lost in embeded brackets
        public static List<string> SplitCondition(string cond)
        {
            cond = RemoveWS(cond) + ",";
            List<string> conds = new List<string>();
            int start = 0;
            int level = 0;
            for (int end = 0; end < cond.Length; end++) {
                if (cond[end] == ',' && level == 0) {
                    conds.Add(cond.Substring(start, end - start));
                    start = end + 1;
                } else if (cond[end] == '[')
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

            if (condsList.Count == 1) {
                conds = condsList[0];

                

                string remainCond = "";
                if (conds.Contains("HAS[")) {
                    int start = conds.IndexOf("HAS[") + 4;
                    remainCond = conds.Substring(start, condsList[0].LastIndexOf(']') - start);
                    conds = conds.Substring(0, start - 5);
                }

                char[] sep = new char[] { '[', ']' };
                string[] splits = conds.Split(sep, 3);
                string type = splits[0].Substring(1);
                string name = splits.Length > 1 ? splits[1] : null;

                switch (conds[0]) {
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

        public static void log(String s)
        {
            print("[ModuleManager] " + s);
        }
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
