using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using KSP;
using KSP.IO;


public class moduleManager : MonoBehaviour
{
	public static GameObject GameObjectInstance;
	private static PluginConfiguration config = PluginConfiguration.CreateForType<moduleManager>();
	private static string loadedCFG = "";
	private static string appPath = KSPUtil.ApplicationRootPath.Replace("\\", "/") + "PluginData/moduleManager/";
	private bool initialized = false;

	private void LoadCFG(string newCFG)
	{
		ConfigNode mods = ConfigNode.Load(appPath + newCFG);
		print ("moduleManager loaded " + mods.CountNodes + " nodes from " + appPath + newCFG);
		
		// step 1: get all available part definitions
		
		
		print ("moduleManager: beginning search of partList...");
		
		foreach (ConfigNode pmod in mods.nodes) {
			AvailablePart partData = PartLoader.getPartInfoByName (pmod.name);
			
			if(partData == null) {
				print ("moduleManager could not find part: " + pmod.name);
			} else {
				print ("moduleManager found part: " + partData.name);
				
				Part part = partData.partPrefab;
				
				if(!part) {
					// I have no idea how this could happen, but may as well check for it.
					print ("Null part from partData " + partData.name);
					
				} else if(part.Modules == null) {
					// I have no idea how this could happen, but may as well check for it.
					print ("Null Modules from part " + part.name);
					
				} else  {
					ModifyPart (partData, pmod);
				}
			}
		}
		foreach (string includeFile in mods.GetValues ("include")) {
			print ("recursing into " + includeFile);
			LoadCFG (includeFile);
		}

		print ("moduleManager: finished search of partList.");
	}

	private void Initialize(string newCFG)
	{

		// initialize our plugin.
		if (loadedCFG.Equals (newCFG) && initialized) // we only want to intialize if we haven't already, since this can conceivably get called each frame.
			return;
		if(config != null)
			config.load ();

		print ("Initializing moduleManager...");

		LoadCFG (newCFG);
		loadedCFG = newCFG;
		initialized = true;
		print ("moduleManager initialized.");
	}
	
	public void Awake()
	{
		DontDestroyOnLoad(this);
	}


	public void OnGUI()
	{
		EditorLogic editor = EditorLogic.fetch;
		if (!HighLogic.LoadedSceneIsEditor || !editor) {
			initialized = false;
			return;
		}
		// we can afford to initialize each frame, because Initialize() checks to see if
		// we're already initialized.
		Initialize ("parts.cfg");
	}

	public static AvailablePart ModifyPart(AvailablePart partData, ConfigNode config)
	{
		print ("applying the following to " + partData.name + ":");
		print (config);
		Part part = partData.partPrefab;

		foreach(ConfigNode node in config.nodes)
		{
			print (node);
			if(node.name.Equals ("REMOVE")) {
				foreach (string value in node.GetValues("RESOURCE")) {
					while(part.Resources.Contains(value))
					{
						print ("REMOVE RESOURCE " + value + " from " + partData.name);
						part.Resources.list.Remove (part.Resources[value]);					
					}
				}
				partData.resourceInfo = "";
				foreach(PartResource r in part.Resources)
					partData.resourceInfo += r.GetInfo() + "\n";

				foreach (string value in node.GetValues ("MODULE")) {
					while(part.Modules.Contains (value))
					{
						print ("REMOVE MODULE " + value + " from " + partData.name);
						part.RemoveModule(part.Modules[value]);
					}
				}
			} else if(node.name.Equals ("REPLACE")) {
				foreach (ConfigNode rNode in node.GetNodes("RESOURCE")) {
					print ("REPLACE RESOURCE " + rNode.GetValue ("name") + " to " + partData.name);
					part.SetResource (rNode);
				}
				
				foreach (ConfigNode rNode in node.GetNodes ("MODULE")) {
					print ("REPLACE MODULE " + rNode.GetValue ("name") + " in " + partData.name + " with:");
					print (rNode);
					part.Modules[rNode.GetValue ("name")].Load(rNode);

				}

			} else if(node.name.Equals ("ADD")) {

				foreach (ConfigNode addNode in node.GetNodes("RESOURCE")) {
					print ("ADD RESOURCE " + addNode.GetValue ("name") + " to " + partData.name);
					part.SetResource (addNode);
				}

				foreach (ConfigNode addNode in node.GetNodes ("MODULE")) {
					print ("ADD MODULE " + addNode.GetValue ("name") + " to " + partData.name + ":");
					print (addNode);
					//FIXME: this fails at PartModule.Load(ConfigNode) with a NullReferenceException
					PartModule module = part.AddModule (addNode.GetValue("name"));

					// really? REALLY? It appears the only way to make this work, is to molest KSP's privates.
					module.GetType ().InvokeMember ("Awake", BindingFlags.InvokeMethod | BindingFlags.NonPublic, null, null, new object[] {});

					module.Load(addNode);
				}
				

			} else if(node.name.Equals ("COPY")) {
				//TODO: Make this code work

				foreach (string newPartName in node.GetValues ("name")) {

					AvailablePart newPart = new AvailablePart(partData.partPath);
					if(newPart.partPrefab == null)
					{
						newPart.partPrefab = new Part();
						newPart.name = newPartName;
						newPart.partPrefab.partName = newPartName;

						print ("COPY " + partData.name + " -> " + newPartName + " failed due to null partPrefab.");
					} else {
						newPart.partPrefab.partName = newPartName;
						newPart.name = newPartName;
						PartLoader.LoadedPartsList.Add (newPart);
						print ("COPY " + partData.name + " -> " + newPartName);

					}
				}
			}
		}
		
		//AvailablePart newPartData = PartLoader.getPartInfoByPartPrefab(part.gameObject);
		//if (newPartData == null) {
		//	print ("getPartInfoByPartPrefab(" + part.name + ") returned null!");
		//	return partData;
		//}
		return partData;
	}
}

public class moduleManagerInit : KSP.Testing.UnitTest
{
	public moduleManagerInit()
	{
		var gameobject = new GameObject("moduleManager", typeof(moduleManager));
		UnityEngine.Object.DontDestroyOnLoad(gameobject);
	}
}
