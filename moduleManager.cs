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
    [KSPAddonFixed(KSPAddon.Startup.Instantly, false, typeof(ConfigManager))]
	public class ConfigManager : MonoBehaviour
	{
		//FindConfigNodeIn finds and returns a ConfigNode in src of type nodeType. 
		//If nodeName is not null, it will only find a node of type nodeType with the value name=nodeName. 
		//If nodeTag is not null, it will only find a node of type nodeType with the value name=nodeName and tag=nodeTag.

		public static ConfigNode FindConfigNodeIn(ConfigNode src, string nodeType, 
		                                          string nodeName = null, string nodeTag = null)
		{
#if debug
			if (nodeTag == null)
				print ("Searching node for " + nodeType + "[" + nodeName + "]");
			else
				print ("Searching node for " + nodeType + "[" + nodeName + "," + nodeTag + "]");
#endif
			foreach (ConfigNode n in src.GetNodes (nodeType)) {
				if(n.HasValue ("name") && n.GetValue ("name").Equals (nodeName) && 
				   (nodeTag == null || 
				   (n.HasValue ("tag") && n.GetValue("tag").Equals(nodeTag))) ) {
#if debug
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
			original.CopyTo (newNode);

			foreach(ConfigNode.Value val in mod.values) {
				if(val.name[0] == '@') {
					// Modifying a value: Format is @key = value or @key,index = value 
					string valName = val.name.Substring (1);
					int index = 0;
					if(valName.Contains (",")) {
						int.TryParse(valName.Split (',')[1], out index);
						valName = valName.Split (',')[0];
					}
					newNode.SetValue (valName, val.value, index);
				} else if (val.name[0] == '!') {
					// Parsing: Format is @key = value or @key,index = value 
					string valName = val.name.Substring (1);
					int index = 0;
					if(valName.Contains (",")) {
						int.TryParse(valName.Split (',')[1], out index);
						valName = valName.Split (',')[0];
					} // index is useless right now, but some day it might not be.
					newNode.RemoveValue (valName);
				} else {
					newNode.AddValue (val.name, val.value);
				}
			}

			foreach (ConfigNode subMod in mod.nodes) {
				if(subMod.name[0] == '@') {
					// Modifying a node: Format is @NODETYPE {...}, @NODETYPE[Name] {...} or @NODETYPE[Name,Tag] {...}
					ConfigNode subNode = null;

					if(subMod.name.Contains ("["))
					{ // format @NODETYPE[Name] {...} or @NODETYPE[Name, Tag] {...}
						string nodeType = subMod.name.Substring (1).Split ('[')[0].Trim ();
						string nodeName = subMod.name.Split ('[')[1].Replace ("]","").Trim();
						string nodeTag = null;
						if(nodeName.Contains (",")) { //format @NODETYPE[Name, Tag] {...}
							nodeTag = nodeName.Split (',')[1];
							nodeName = nodeName.Split (',')[0];
						}
						subNode = FindConfigNodeIn(newNode, nodeType, nodeName, nodeTag);
					} else { // format @NODETYPE {...}
						string nodeType = subMod.name.Substring (1);
						subNode = newNode.GetNode (nodeType);
					}
					// find the original subnode to modify, modify it, remove the original and add the modified.
					if(subNode == null) {
                        print("[ModuleManager] Could not find node to modify: " + subMod.name);
					} else {
						ConfigNode newSubNode = ModifyNode (subNode, subMod);
						newNode.nodes.Remove (subNode);
						newNode.nodes.Add (newSubNode);
					}

				} else if(subMod.name[0] == '!') {
					// Removing a node: Format is !NODETYPE {}, !NODETYPE[Name] {} or !NODETYPE[Name,Tag] {}

					ConfigNode subNode;
					
					if(subMod.name.Contains ("["))
					{ // format !NODETYPE[Name] {} or !NODETYPE[Name, Tag] {}
						string nodeType = subMod.name.Substring (1).Split ('[')[0].Trim ();
						string nodeName = subMod.name.Split ('[')[1].Replace ("]","").Trim();
						string nodeTag = null;
						if(nodeName.Contains (",")) { //format !NODETYPE[Name, Tag] {}
							nodeTag = nodeName.Split (',')[1];
							nodeName = nodeName.Split (',')[0];
						}
						subNode = FindConfigNodeIn(newNode, nodeType, nodeName, nodeTag);
					} else { // format !NODETYPE {}
						string nodeType = subMod.name.Substring (1);
						subNode = newNode.GetNode (nodeType);
					}
					if(subNode != null)
						newNode.nodes.Remove (subNode);

				} else {
					// this is a full node, not a mod, so just add it as a new subnode.
					newNode.AddNode (subMod);
				}
			}
			return newNode;
		}

		public static List<UrlDir.UrlConfig> AllConfigsStartingWith(string match) {
			List<UrlDir.UrlConfig> nodes = new List<UrlDir.UrlConfig>();
			foreach (UrlDir.UrlConfig url in GameDatabase.Instance.root.AllConfigs) {
				if(url.type.StartsWith (match))
					url.config.name = url.type;
					nodes.Add (url);
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

            string fullPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            // The subpatch of Gamedata where this dll is installed
            // We will only process cfg under this directory
            string relPath = Uri.UnescapeDataString(new Uri(Path.GetFullPath(KSPUtil.ApplicationRootPath)).MakeRelativeUri(new Uri(fullPath)).ToString() + "/").Remove(0, "GameData/".Length);

            if (relPath == "/")
            {
                PopupDialog.SpawnPopupDialog("Module Manager Path", "This Module Manager will only work if installed in a subfolder of GameData.\nAbording patch loading", "OK", false, HighLogic.Skin);
                loaded = true;
                return;
            }

            var assemblies = AssemblyLoader.loadedAssemblies.Where(a => a.assembly.GetName().Name == Assembly.GetExecutingAssembly().GetName().Name).Where(a => a.assembly.GetName().Version.CompareTo(new System.Version(1, 4, 0)) == -1);
            if (assemblies.Any())
            {
                var badPaths = assemblies.Select(a => a.path).Select(p => Uri.UnescapeDataString(new Uri(Path.GetFullPath(KSPUtil.ApplicationRootPath)).MakeRelativeUri(new Uri(p)).ToString().Replace('/', Path.DirectorySeparatorChar)));
                PopupDialog.SpawnPopupDialog("Old versions of Module Manager", "You have old versions of Module Manager (older than 1.4).\nYou will need to remove them for Module Manager to work normaly\n\nIncorrect version(s):\n" + String.Join("\n", badPaths.ToArray()), "OK", false, HighLogic.Skin);
                loaded = true;
                return;
            }

            print("[ModuleManager] loading cfg patches in " + relPath);

            // TODO : Not being lazy and do 1 loop instead of 3 ...
            foreach (UrlDir.UrlConfig mod in GameDatabase.Instance.root.AllConfigs)
            {
                if (mod.type[0] == '@')
                {
                    char[] sep = new char[] { '[', ']' };
                    string[] splits = mod.name.Split(sep, 3);
                    string pattern = splits[1];

                    // it's a modification node and it's not one Modulemanager will process
                    if (pattern.Contains("*") || pattern.Contains("?") || (splits.Length > 2 && splits[2].Contains(":HAS") && !splits[2].Contains(":Final")))
                    {
                        String cond = "";
                        if (splits.Length > 2 && splits[2].Length > 5)
                        {
                            int start = splits[2].IndexOf("HAS[") + 4;
                            cond = splits[2].Substring(start, splits[2].LastIndexOf(']') - start);
                        }
                        foreach (UrlDir.UrlConfig url in GameDatabase.Instance.root.AllConfigs)
                        {
                            if (url.name[0] != '@' && WildcardMatch(url.name, pattern) && CheckCondition(url.config, cond) && mod.url.StartsWith(relPath))
                            {
                                print("[ModuleManager] Applying node " + mod.url + " to " + url.url);
                                url.config = ConfigManager.ModifyNode(url.config, mod.config);
                            }
                        }
                    }
                }
            }

            foreach (UrlDir.UrlConfig url in GameDatabase.Instance.root.AllConfigs)
            {
                if (url.type[0] != '@')
                {
                    string modName = "@" + url.type + "[" + url.name + "]";
                    foreach (UrlDir.UrlConfig mod in GameDatabase.Instance.GetConfigs(modName))
                    {
                        if (mod.url.StartsWith(relPath))
                        {
                            print("[ModuleManager] Applying node " + mod.url + " to " + url.url);
                            url.config = ModifyNode(url.config, mod.config);
                        }
                    }

                    modName = "@" + url.type + "[" + url.name + "]:Final";
                    foreach (UrlDir.UrlConfig mod in GameDatabase.Instance.GetConfigs(modName))
                    {
                        if (mod.url.StartsWith(relPath))
                        {
                            print("[ModuleManager] Applying node " + mod.url + " to " + url.url);
                            url.config = ModifyNode(url.config, mod.config);
                        }
                    }
                }
            }

            foreach (UrlDir.UrlConfig mod in GameDatabase.Instance.root.AllConfigs)
            {
                if (mod.type[0] == '@')
                {
                    char[] sep = new char[] { '[', ']' };
                    string[] splits = mod.name.Split(sep, 3);
                    string pattern = splits[1];

                    // it's a modification node and it's not one Modulemanager will process
                    if (pattern.Contains("*") || pattern.Contains("?") || (splits.Length > 2 && splits[2].Contains(":HAS") && splits[2].Contains(":Final")))
                    {
                        String cond = "";
                        if (splits.Length > 2 && splits[2].Length > 5)
                        {
                            int start = splits[2].IndexOf("HAS[") + 4;
                            cond = splits[2].Substring(start, splits[2].LastIndexOf(']') - start);
                        }
                        foreach (UrlDir.UrlConfig url in GameDatabase.Instance.root.AllConfigs)
                        {
                            if (url.name[0] != '@' && WildcardMatch(url.name, pattern) && CheckCondition(url.config, cond) && mod.url.StartsWith(relPath))
                            {
                                print("[ModuleManager] Applying node " + mod.url + " to " + url.url);
                                url.config = ConfigManager.ModifyNode(url.config, mod.config);
                            }
                        }
                    }
                }
            }


            loaded = true;
        }

        // Split condiction while not getting lost in embeded brackets
        public List<string> SplitCondition(string cond)
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

        public bool CheckCondition(ConfigNode node, string conds)
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

        private bool WildcardMatch(String s, String wildcard)
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

