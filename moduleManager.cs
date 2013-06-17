using System.Collections.Generic;
using UnityEngine;
using KSP;

namespace ModuleManager
{
	[KSPAddon(KSPAddon.Startup.Instantly, true)]
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


		//ModifyNode applies the ConfigNode mod as a 'patch' to ConfigNode original, then returns the patched ConfigNode.
		// it uses FindConfigNodeIn(src, nodeType, nodeName, nodeTag) to recurse.

		public static ConfigNode ModifyNode(ConfigNode original, ConfigNode mod)
		{
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
						print ("Could not find node to modify: " + subMod.name);
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

		bool loaded = false;
		public void OnGUI()
		{
			if (loaded)
				return;
			//by the time we reach OnGUI(), all the configNodes have been loaded.
			print ("ModuleManager loading cfg patches...");
			foreach (UrlDir.UrlConfig url in GameDatabase.Instance.root.AllConfigs) {
				if (url.type [0] != '@') {
					string modName = "@" + url.type + "[" + url.name + "]";
					foreach (UrlDir.UrlConfig mod in GameDatabase.Instance.GetConfigs(modName)) {
						print ("Applying node " + mod.url);
						url.config = ModifyNode (url.config, mod.config);
					}

					modName = "@" + url.type + "[" + url.name + "]:Final";
					foreach (UrlDir.UrlConfig mod in GameDatabase.Instance.GetConfigs(modName)) {
						print ("Applying node " + mod.url);
						url.config = ModifyNode (url.config, mod.config);
					}
				}
			}
			loaded = true;
		}
	}
}

