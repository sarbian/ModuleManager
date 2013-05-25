using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

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
	[KSPAddon(KSPAddon.Startup.MainMenu, true)]
	public class ModuleManager : MonoBehaviour
	{

		public static List<AvailablePart> partDatabase = PartLoader.LoadedPartsList;
		private static string appPath = KSPUtil.ApplicationRootPath.Replace("\\", "/") + "GameData/";


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

		
		public static UrlDir.UrlConfig ModifyConfig(ConfigNode modNode)
		{ 
			// this is a modifier node. Format: @NODETYPE[NodeName] {...}
			if (modNode.name == null || modNode.name.Trim ().Equals ("") || modNode.name [0] != '@' || !modNode.name.Contains ("["))
				return null;
			else {
				string nodeType = modNode.name.Substring (1).Split ('[') [0].Trim ();
				string nodeName = modNode.name.Split ('[') [1].Replace ("]", "").Trim ();
				
				print ("moduleManager modifying " + nodeType + "[" + nodeName + "]");
				
				UrlDir.UrlConfig orig = FindConfig (nodeType, nodeName);
				if (orig == null) {
					print ("Could not find Config for " + nodeType + "[" + nodeName + "]");
					return null;
				}
				print ("Old ConfigNode:");
				print (orig.config.ToString ());
				orig.config = ModifyNode (orig.config, modNode);
				print ("New ConfigNode:");
				print (orig.config.ToString ());
				return orig;
			}
		}

		private static void LoadCFG(string newCFG)
		{
			ConfigNode mods = ConfigNode.Load(appPath + newCFG);
			if (mods == null) {
				print ("ModuleManager: file " + newCFG + " not found.");
				return;

			}
			print ("moduleManager loaded " + mods.CountNodes + " nodes from " + appPath + newCFG);
			
			// step 1: get all available part definitions
			
			
			print ("moduleManager: beginning search of partList...");
			
			foreach (ConfigNode pmod in mods.nodes) {
				if(pmod.name == null || pmod.name.Trim ().Equals ("")) {
					// an empty node? I suppose it's possible
				} else if(pmod.name[0] == '@') {
					UrlDir.UrlConfig orig = ModifyConfig (pmod);
					if(orig == null)
						print (pmod.name.Substring (1) + " not found");
					else {
						string nodeType = orig.config.name, nodeName = orig.name;
						if(nodeType.Equals ("PART")) {
							// we just modified a part node, so let's change the AvailablePart
							AvailablePart partData = partDatabase.Find (p => p.name.Equals (nodeName.Replace ("_", ".")));
							if(partData == null)
								print ("PART[" + nodeName + "] not found!");
							else {
								ModifyPart(partData, orig.config);
							}
						} else if(nodeType.Equals ("RESOURCE_DEFINITION")) {
							// we just modified a resource definition node, so let's change the ResourceDefinition
							PartResourceDefinition resource = PartResourceLibrary.Instance.resourceDefinitions[nodeName];
							if(resource == null)
								print ("RESOURCE_DEFINITION[" + nodeName + "] not found!");
							else
								resource.Load (orig.config);
						}
					}
				}

			}
			foreach (string includeFile in mods.GetValues ("include")) {
				print ("recursing into " + includeFile);
				LoadCFG (includeFile);
			}

			print ("moduleManager: finished search of partList.");
		}

		
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
				partData.moduleInfo += module.GetInfo ();
			}

		}

		public static void ApplyMods(string nodeType)
		{
			foreach (UrlDir.UrlConfig url in GameDatabase.Instance.GetConfigs (nodeType)) {

				string nodeName = url.name;

				string modName = "@" + nodeType + "[" + nodeName + "]";
				print ("Searching gameDatabase for " + modName);

				foreach (ConfigNode mod in GameDatabase.Instance.GetConfigNodes(modName)) {
					print ("Applying node " + modName);
					url.config = ModifyNode (url.config, mod);
				}

				modName = "@" + nodeType + "[" + nodeName + "]:Final";
				print ("Searching gameDatabase for " + modName);
				
				foreach (ConfigNode mod in GameDatabase.Instance.GetConfigNodes(modName)) {
					print ("Applying node " + modName);
					url.config = ModifyNode (url.config, mod);
				}

				if (nodeType.Equals ("PART")) {
					// we just modified a part node, so let's change the AvailablePart
					AvailablePart partData = partDatabase.Find (p => p.name.Equals (nodeName.Replace ("_", ".")));
					if (partData == null)
						print ("PART[" + nodeName + "] not found!");
					else {
						ModifyPart (partData, url.config);
					}
				} else if (nodeType.Equals ("RESOURCE_DEFINITION")) {
					// we just modified a resource definition node, so let's change the ResourceDefinition
					PartResourceDefinition resource = PartResourceLibrary.Instance.resourceDefinitions [nodeName];
					if (resource == null)
						print ("RESOURCE_DEFINITION[" + nodeName + "] not found!");
					else
						resource.Load (url.config);
				}

			}
		}

		public void Awake()
		{
			ApplyMods ("RESOURCE_DEFINITION");
			ApplyMods ("PART");
		}	
	}
}

