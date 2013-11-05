using System.Collections.Generic;
using UnityEngine;
using KSP;
using System;
using System.Text.RegularExpressions;

using System.Linq;
using System.Reflection;
using System.IO;


namespace ModuleManager
{
    // Once MUST be true for the election process to work when 2+ dll of the same version are loaded
    [KSPAddonFixed(KSPAddon.Startup.Instantly, true, typeof(ConfigManager))]
    public class ConfigManager : MonoBehaviour
    {
        //FindConfigNodeIn finds and returns a ConfigNode in src of type nodeType. 
        //If nodeName is not null, it will only find a node of type nodeType with the value name=nodeName. 
        //If nodeTag is not null, it will only find a node of type nodeType with the value name=nodeName and tag=nodeTag.

        public static ConfigNode FindConfigNodeIn(ConfigNode src, string nodeType,
                                                  string nodeName = null, string nodeTag = null)
        {
#if DEBUG
			if (nodeTag == null)
				print ("Searching node for " + nodeType + "[" + nodeName + "]");
			else
				print ("Searching node for " + nodeType + "[" + nodeName + "," + nodeTag + "]");
#endif
            foreach (ConfigNode n in src.GetNodes(nodeType))
            {
                if (n.HasValue("name") && WildcardMatch(n.GetValue("name"), nodeName) &&
                   (nodeTag == null ||
                   (n.HasValue("tag") && WildcardMatch(n.GetValue("tag"), nodeTag))))
                {
#if DEBUG
                    print ("found node!");
#endif
                    return n;
                }
            }
            return null;
        }

        // Added that to precent crash in KSP 'CopyTo' when a subnode has an empty name like
        // Like a pair of curly bracket without a name before them
        public static bool isSane(ConfigNode node)
        {
            if (node.name == "")
                return false;
            else foreach (ConfigNode subnode in node.nodes)
                    if (!isSane(subnode))
                        return false;
            return true;
        }


        //ModifyNode applies the ConfigNode mod as a 'patch' to ConfigNode original, then returns the patched ConfigNode.
        // it uses FindConfigNodeIn(src, nodeType, nodeName, nodeTag) to recurse.

        public static ConfigNode ModifyNode(ConfigNode original, ConfigNode mod)
        {
            if (!isSane(original) || !isSane(mod))
            {
                print("[ModuleManager] You feel a disturbance in the force");
                print("[ModuleManager] A node has an empty name. Skipping it");
                return original;
            }

            ConfigNode newNode = new ConfigNode(original.name);
            original.CopyTo(newNode);

            foreach (ConfigNode.Value val in mod.values)
            {
                if (val.name[0] == '@')
                {
                    // Modifying a value: Format is @key = value or @key,index = value 
                    string valName = val.name.Substring(1);
                    int index = 0;
                    if (valName.Contains(","))
                    {
                        int.TryParse(valName.Split(',')[1], out index);
                        valName = valName.Split(',')[0];
                    }
                    newNode.SetValue(valName, val.value, index);
                }
                else if (val.name[0] == '!')
                {
                    // Parsing: Format is @key = value or @key,index = value 
                    string valName = val.name.Substring(1);
                    int index = 0;
                    if (valName.Contains(","))
                    {
                        int.TryParse(valName.Split(',')[1], out index);
                        valName = valName.Split(',')[0];
                    } // index is useless right now, but some day it might not be.
                    newNode.RemoveValue(valName);
                }
                else
                {
                    newNode.AddValue(val.name, val.value);
                }
            }

            foreach (ConfigNode subMod in mod.nodes)
            {
                if (subMod.name[0] == '@')
                {
                    // Modifying a node: Format is @NODETYPE {...}, @NODETYPE[Name] {...} or @NODETYPE[Name,Tag] {...}
                    ConfigNode subNode = null;

                    if (subMod.name.Contains("["))
                    { // format @NODETYPE[Name] {...} or @NODETYPE[Name, Tag] {...}
                        string nodeType = subMod.name.Substring(1).Split('[')[0].Trim();
                        string nodeName = subMod.name.Split('[')[1].Replace("]", "").Trim();
                        string nodeTag = null;
                        if (nodeName.Contains(","))
                        { //format @NODETYPE[Name, Tag] {...}
                            nodeTag = nodeName.Split(',')[1];
                            nodeName = nodeName.Split(',')[0];
                        }
                        subNode = FindConfigNodeIn(newNode, nodeType, nodeName, nodeTag);
                    }
                    else
                    { // format @NODETYPE {...}
                        string nodeType = subMod.name.Substring(1);
                        subNode = newNode.GetNode(nodeType);
                    }
                    // find the original subnode to modify, modify it, remove the original and add the modified.
                    if (subNode == null)
                    {
                        print("[ModuleManager] Could not find node to modify: " + subMod.name);
                    }
                    else
                    {
                        ConfigNode newSubNode = ModifyNode(subNode, subMod);
                        newNode.nodes.Remove(subNode);
                        newNode.nodes.Add(newSubNode);
                    }
                }
                else if (subMod.name[0] == '!')
                {
                    // Removing a node: Format is !NODETYPE {}, !NODETYPE[Name] {} or !NODETYPE[Name,Tag] {}

                    ConfigNode subNode;

                    if (subMod.name.Contains("["))
                    { // format !NODETYPE[Name] {} or !NODETYPE[Name, Tag] {}
                        string nodeType = subMod.name.Substring(1).Split('[')[0].Trim();
                        string nodeName = subMod.name.Split('[')[1].Replace("]", "").Trim();
                        string nodeTag = null;
                        if (nodeName.Contains(","))
                        { //format !NODETYPE[Name, Tag] {}
                            nodeTag = nodeName.Split(',')[1];
                            nodeName = nodeName.Split(',')[0];
                        }
                        subNode = FindConfigNodeIn(newNode, nodeType, nodeName, nodeTag);
                    }
                    else
                    { // format !NODETYPE {}
                        string nodeType = subMod.name.Substring(1);
                        subNode = newNode.GetNode(nodeType);
                    }
                    if (subNode != null)
                        newNode.nodes.Remove(subNode);

                }
                else
                {
                    // this is a full node, not a mod, so just add it as a new subnode.
                    newNode.AddNode(subMod);
                }
            }
            return newNode;
        }

        public static List<UrlDir.UrlConfig> AllConfigsStartingWith(string match)
        {
            List<UrlDir.UrlConfig> nodes = new List<UrlDir.UrlConfig>();
            foreach (UrlDir.UrlConfig url in GameDatabase.Instance.root.AllConfigs)
            {
                if (url.type.StartsWith(match))
                    url.config.name = url.type;
                nodes.Add(url);
            }
            return nodes;
        }

        bool loaded = true;

        bool waitingReload = true;

        public void OnGUI()
        {
            if (PartLoader.Instance.Recompile == waitingReload)
            {
                waitingReload = !waitingReload;
                if (!waitingReload)
                    loaded = false;
            }
            if (loaded)
                return;

            // Check for old version and MMSarbianExt
            var oldMM = AssemblyLoader.loadedAssemblies.Where(a => a.assembly.GetName().Name == Assembly.GetExecutingAssembly().GetName().Name).Where(a => a.assembly.GetName().Version.CompareTo(new System.Version(1, 5)) == -1);
            var oldAssemblies = oldMM.Concat(AssemblyLoader.loadedAssemblies.Where(a => a.assembly.GetName().Name == "MMSarbianExt"));
            if (oldAssemblies.Any())
            {
                var badPaths = oldAssemblies.Select(a => a.path).Select(p => Uri.UnescapeDataString(new Uri(Path.GetFullPath(KSPUtil.ApplicationRootPath)).MakeRelativeUri(new Uri(p)).ToString().Replace('/', Path.DirectorySeparatorChar)));
                PopupDialog.SpawnPopupDialog("Old versions of Module Manager", "You have old versions of Module Manager (older than 1.5) or MMSarbianExt.\nYou will need to remove them for Module Manager to work\nExit KSP and delete those files :\n" + String.Join("\n", badPaths.ToArray()), "OK", false, HighLogic.Skin);
                loaded = true;
                return;
            }

            Assembly currentAssembly = Assembly.GetExecutingAssembly();
            var eligible = AssemblyLoader.loadedAssemblies.Where(a => a.assembly.GetName().Name == currentAssembly.GetName().Name);

            //print("[ModuleManager] starting \n" + String.Join("\n", eligible.Select(a => a.path).ToArray()));

            // Elect the newest loaded version of MM to process all patch files.
            // If there is a newer version loaded then don't do anything
            if (eligible.Any(a => a.assembly.GetName().Version.CompareTo(currentAssembly.GetName().Version) == 1))
            {
                loaded = true;
                print("[ModuleManager] version " + currentAssembly.GetName().Version + " at " + currentAssembly.Location + " lost the election");
                return;
            }
            else
            {
                string candidates = "";
                foreach (AssemblyLoader.LoadedAssembly a in eligible)
                    candidates += "Version " + a.assembly.GetName().Version + " " + a.path + " " + "\n";
                print("[ModuleManager] version " + currentAssembly.GetName().Version + " at " + currentAssembly.Location + " won the election against\n" + candidates);
            }             


            /* No longer usefull but kept as reference in case I need it

            List<String> processedPath = new List<string>();            

            // Generate the list of subdirectory of Gamedata where we process patch file
            foreach (AssemblyLoader.LoadedAssembly a in eligible)
            {
                string fullPath = Path.GetDirectoryName(a.path);
                string relPath = Uri.UnescapeDataString(new Uri(Path.GetFullPath(KSPUtil.ApplicationRootPath)).MakeRelativeUri(new Uri(fullPath)).ToString() + "/").Remove(0, "GameData/".Length);
                if (relPath == "/")
                {
                    PopupDialog.SpawnPopupDialog("Module Manager Path", "This Module Manager will only work if installed in a subfolder of GameData.\nAbording patch loading", "OK", false, HighLogic.Skin);
                    loaded = true;
                    return;
                }
                processedPath.Add(relPath);
            }            

            print("[ModuleManager] will procces patch in" + String.Join("\n",processedPath.ToArray()));

            */

            // Build a list of subdirectory that won't be processed
            List<String> excludePaths = new List<string>();

            foreach (UrlDir.UrlConfig mod in GameDatabase.Instance.root.AllConfigs)
            {
                if (mod.name == "@MODULEMANAGER[LOCAL]")
                {
                    string fullpath =  mod.url.Substring(0, mod.url.IndexOf('@'));
                    string excludepath = fullpath.Substring(0,fullpath.LastIndexOf('/'));
                    excludePaths.Add(excludepath);
                    print("excludepath: " + excludepath);                    
                }
            }
            if (excludePaths.Any())
                print("[ModuleManager] will not procces patch in those subdirectories:\n" + String.Join("\n",excludePaths.ToArray()));

            applyPatch(excludePaths, false);

            // :Final node
            applyPatch(excludePaths, true);
            
            loaded = true;
        }

        // Apply patch to all relevent nodes
        public void applyPatch(List<String> excludePaths, bool final)
        {
            foreach (UrlDir.UrlConfig mod in GameDatabase.Instance.root.AllConfigs)
            {
                if (mod.type[0] == '@')
                {
                    try
                    {
                        char[] sep = new char[] { '[', ']' };
                        string[] splits = mod.name.Split(sep, 3);
                        string pattern = splits[1];
                        string type = splits[0].Substring(1);

                        if (final ^ mod.name.EndsWith(":Final"))
                        {
                            String cond = "";
                            if (splits.Length > 2 && splits[2].Length > 5)
                            {
                                int start = splits[2].IndexOf("HAS[") + 4;
                                cond = splits[2].Substring(start, splits[2].LastIndexOf(']') - start);
                            }
                            foreach (UrlDir.UrlConfig url in GameDatabase.Instance.root.AllConfigs)
                            {
                                if (url.type == type && WildcardMatch(url.name, pattern) && CheckCondition(url.config, cond) && !isPathInList(mod.url, excludePaths))
                                {
                                    print("[ModuleManager] Applying node " + mod.url + " to " + url.url);
                                    url.config = ConfigManager.ModifyNode(url.config, mod.config);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        print("[ModuleManager] Exception while processing node : " + mod.url + "\n" + e.ToString());
                    }
                }
            }
        }

        public bool isPathInList(string modPath, List<String> pathList)
        {
            return pathList.Any(p => modPath.StartsWith(p));
        }


        // Split condiction while not getting lost in embeded brackets
        public static List<string> SplitCondition(string cond)
        {
            cond = cond + ",";
            List<string> conds = new List<string>();
            int start = 0;
            int level = 0;
            for (int end = 0; end < cond.Length; end++)
            {
                if (cond[end] == ',' && level == 0)
                {
                    conds.Add(cond.Substring(start, end - start).Trim());
                    start = end + 1;
                }
                else if (cond[end] == '[') level++;
                else if (cond[end] == ']') level--;
            }
            return conds;
        }

        public static bool CheckCondition(ConfigNode node, string conds)
        {
            if (conds.Length > 0)
            {
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

                    string type = conds.Substring(1).Split('[')[0].Trim();
                    string name = conds.Split('[')[1].Replace("]", "").Trim();

                    if (conds[0] == '@' || conds[0] == '!')  // @MODULE[ModuleAlternator] or !MODULE[ModuleAlternator]
                    {
                        bool not = (conds[0] == '!');
                        ConfigNode subNode = ConfigManager.FindConfigNodeIn(node, type, name);
                        if (subNode != null)
                            return not ^ CheckCondition(subNode, remainCond);
                        else
                            return not ^ false;
                    }
                    else if (conds[0] == '#') // #module[Winglet]
                    {
                        if (node.HasValue(type) && node.GetValue(type).Equals(name))
                            return CheckCondition(node, remainCond);
                        else
                            return false;
                    }
                    else if (conds[0] == '~') // ~breakingForce[]  breakingForce is not present
                    {
                        if (!(node.HasValue(type)))

                            return CheckCondition(node, remainCond);
                        else
                            return false;
                    }
                    else
                        return false; // Syntax error
                }
                else  // Multiple condition
                {
                    foreach (string cond in condsList)
                    {
                        if (!CheckCondition(node, cond))
                            return false;
                    }
                    return true;
                }
            }
            else
                return true;
        }

        public static bool WildcardMatch(String s, String wildcard)
        {
            String pattern = "^" + Regex.Escape(wildcard).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
            Regex regex;
            regex = new Regex(pattern);

            return (regex.IsMatch(s));
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

