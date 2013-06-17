using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

using UnityEngine;

using KSP;
using KSP.IO;

/* Ideal use cases
 * Part.Load(ConfigNode)
 * {
 *   this will set the Fields, 
 *   then load all attachNodes,
 *   then load all Resources,
 *   then load all Modules.
 * 
 *   It will work whether ConfigNode is a straight PART {} node, or a @PART[name] node.
 * }
 * 
 * AvailablePart.Load(ConfigNode)
 * {
 *   this will call Part.Load(ConfigNode)
 *   then reset resourceInfo
 *   then reset moduleInfo 
 * }
 * 
 * List<ConfigNode> GameDatabase
 * this will be every possible top-level ConfigNode pulled from every .cfg file in the GameData folder,
 * concatenated as subnodes of one single massive ConfigNode
 * This ConfigNode will also have values for every game asset
 * such as mesh =
 * and texture =
 * and sound =
 * and so on.
 * 
 * */
namespace ModuleManager
{
	public class ModuleManager : MonoBehaviour
	{
		public static bool Awaken(PartModule module)
		{
			// thanks to Mu and Kine for help with this bit of Dark Magic. 
			// KINEMORTOBESTMORTOLOLOLOL
			if (module == null)
				return false;
			object[] paramList = new object[] { };
			MethodInfo awakeMethod = typeof(PartModule).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
			
			if (awakeMethod == null)
				return false;
			
			awakeMethod.Invoke(module, paramList);
			return true;
		}
		
		public static void ModifyPart(Part part, ConfigNode node)
		{
			part.Fields.Load(node);
			
			//Step 2A: clear the old Resources
			part.Resources.list.Clear ();
			//Step 2B: load the new Resources
			foreach(ConfigNode rNode in node.GetNodes ("RESOURCE"))
				part.AddResource (rNode);
			
			//Step 3A: clear the old Modules
			while (part.Modules.Count > 0)
				part.RemoveModule (part.Modules [0]);
			//Step 3B: load the new Modules
			foreach(ConfigNode mNode in node.GetNodes ("MODULE")) {
				
				PartModule module = part.AddModule (mNode.GetValue ("name"));
				if(module) {
					// really? REALLY? It appears the only way to make this work, is to molest KSP's privates.
					if(Awaken (module)) { // uses reflection to find and call the PartModule.Awake() private method
						module.Load(mNode);					
					} else {
						print ("Awaken failed for new module.");
					}
					if(module.part == null) {
						print ("new module has null part.");
					} else {
						#if DEBUG
						print ("Created module for " + module.part.name);
						#endif
					}
				}
			}
		}
		
		public static void ModifyPart(AvailablePart partData, ConfigNode node)
		{
			
			//Step 1: load the Fields
			partData.partPrefab.Fields.Load(node);
			
			//Step 2A: clear the old Resources
			partData.partPrefab.Resources.list.Clear ();
			partData.resourceInfo = "";
			//Step 2B: load the new Resources
			foreach(ConfigNode rNode in node.GetNodes ("RESOURCE")) {
				PartResource resource = partData.partPrefab.AddResource (rNode);
				if(partData.resourceInfo.Length > 0)
					partData.resourceInfo += "\n";
				partData.resourceInfo += resource.GetInfo ();
			}
			if (partData.resourceInfo.Length > 0)
				partData.resourceInfo += "\nDry Mass: " + partData.partPrefab.mass.ToString ("F3");
			//Step 3A: clear the old Modules
			while (partData.partPrefab.Modules.Count > 0)
				partData.partPrefab.RemoveModule (partData.partPrefab.Modules [0]);
			partData.moduleInfo = "";
			//Step 3B: load the new Modules
			foreach(ConfigNode mNode in node.GetNodes ("MODULE")) {
				
				PartModule module = partData.partPrefab.AddModule (mNode.GetValue ("name"));
				if(module) {
					// really? REALLY? It appears the only way to make this work, is to molest KSP's privates.
					if(Awaken (module)) { // uses reflection to find and call the PartModule.Awake() private method
						module.Load(mNode);					
					} else {
						print ("Awaken failed for new module.");
					}
					if(module.part == null) {
						print ("new module has null part.");
					} else {
						#if DEBUG
						print ("Created module for " + module.part.name);
						#endif
					}
				}
				if(partData.moduleInfo.Length > 0)
					partData.moduleInfo += "\n";
				partData.moduleInfo += module.GetInfo ();
			}
			
		}
	}
	[KSPAddon(KSPAddon.Startup.EveryScene, true)]
	public class ConfigManager : MonoBehaviour
	{

		public static List<AvailablePart> partDatabase = PartLoader.LoadedPartsList;

		//FindConfigNodeIn finds and returns a ConfigNode in src of type nodeType. If nodeName is not null,
		//it will only find a node of type nodeType with the value name=nodeName. If nodeTag is not null,
		//it will only find a node of type nodeType with the value name=nodeName and tag=nodeTag.

		public static ConfigNode FindConfigNodeIn(ConfigNode src, string nodeType, string nodeName, string nodeTag)
		{
			if (nodeTag == null)
				print ("Searching node for " + nodeType + "[" + nodeName + "]");
			else
				print ("Searching node for " + nodeType + "[" + nodeName + "," + nodeTag + "]");

			foreach (ConfigNode n in src.GetNodes (nodeType)) {
				if(n.HasValue ("name") && n.GetValue ("name").Equals (nodeName) && 
				   (nodeTag == null || 
				   (n.HasValue ("tag") && n.GetValue("tag").Equals(nodeTag))) ) {
					print ("found node!");
					return n;
				}
			}
			return null;
		}

		public static UrlDir.UrlConfig FindConfig(string nodeType, string nodeName) 
		{
			print ("Searching " + (GameDatabase.Instance.GetConfigs (nodeType).Length+1).ToString () + nodeType + " nodes for 'name = " + nodeName + "'");
			foreach (UrlDir.UrlConfig n in GameDatabase.Instance.GetConfigs(nodeType)) {
				if(n.name.Equals (nodeName)) {
					return n;
				} else if(n.config.HasValue ("name") && n.config.GetValue ("name").Equals (nodeName)) {
					return n;
				}
			}
			return null;
		}


		//ModifyNode applies the ConfigNode mod as a 'patch' to ConfigNode original,
		//then returns the patched ConfigNode.

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

		public static void ApplyMods(string nodeType)
		{
			foreach (UrlDir.UrlConfig url in GameDatabase.Instance.GetConfigs (nodeType)) {

				if(!url.config.HasValue ("@modified")) // don't apply a cfg mod twice
				{

					string nodeName = url.name;

					if (nodeType.Equals ("PART")) {
						// for some reason the loader rips out all this data from the node.
						// so we have to put it back.
						AvailablePart partData = partDatabase.Find (p => p.name.Equals (nodeName.Replace ("_", ".")));
						if(partData == null)
							print ("PART[" + nodeName + "] not found!");
						else {
							print ("Restoring PART[" + nodeName + "] metadata");
							url.config.AddValue ("name", nodeName);
							url.config.AddValue ("description", partData.description);
							url.config.AddValue ("category", partData.category);
							url.config.AddValue ("title", partData.title);
							url.config.AddValue ("module", partData.partPrefab.GetType().ToString ());
						}
					}

					bool modded = false;
					string modName = "@" + nodeType + "[" + nodeName + "]";
					print ("Searching gameDatabase for " + modName);

					foreach (ConfigNode mod in GameDatabase.Instance.GetConfigNodes(modName)) {
						print ("Applying node " + modName);
						url.config = ModifyNode (url.config, mod);
					}

					modName = "@" + nodeType + "[" + nodeName + "]:Final";
					print ("Searching gameDatabase for " + modName);
					
					foreach (ConfigNode mod in GameDatabase.Instance.GetConfigNodes(modName)) {
						modded = true;
						print ("Applying node " + modName);
						url.config = ModifyNode (url.config, mod);
					}


					if(modded) {
						url.config.AddValue ("@modified", "true");
						print ("final node: " + url.config.ToString ());
					} else {
						url.config.AddValue ("@modified", "false");
					}
				}
			}
		}

		public static List<ConfigNode> AllConfigsStartingWith(string match) {
			List<ConfigNode> nodes = new List<ConfigNode>();
			foreach (UrlDir.UrlConfig url in GameDatabase.Instance.root.AllConfigs) {
				if(url.type.StartsWith (match))
					url.config.name = url.type;
					nodes.Add (url.config);
			}
			return nodes;
		}

		public bool loaded {
			get {
				foreach (UrlDir.UrlConfig url in GameDatabase.Instance.root.AllConfigs) {
					return(url.config.HasValue ("@modified"));
				}
				return true;
			}

		}


		public void OnGUI()
		{
			if (loaded || !GameDatabase.Instance.IsReady ())
				return;

			List<string> types = new List<string> ();
			foreach (UrlDir.UrlConfig url in GameDatabase.Instance.root.AllConfigs) {
				if (url.type [0] != '@' && !types.Contains (url.type)) {
					types.Add (url.type);
					print ("Applying cfg patches to type " + url.type);
					ApplyMods (url.type);
				}
			}
			
			// reload Resources
			PartResourceLibrary.Instance.resourceDefinitions.Clear ();
			PartResourceLibrary.Instance.LoadDefinitions ();
			
			// reload Parts
			if (PartLoader.LoadedPartsList.Count > 0) {
				PartLoader.Instance.Recompile = true;
				PartLoader.Instance.StartLoad ();

			}
		}
	}
}

