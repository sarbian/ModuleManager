using System;
using System.Collections.Generic;
using System.Linq;
using ModuleManager.Extensions;
using ModuleManager.Logging;
using ModuleManager.Progress;
using NodeStack = ModuleManager.Collections.ImmutableStack<ConfigNode>;

namespace ModuleManager
{
    public static class NeedsChecker
    {
        public static void CheckNeeds(UrlDir gameDatabaseRoot, IEnumerable<string> mods, IPatchProgress progress, IBasicLogger logger)
        {
            UrlDir gameData = gameDatabaseRoot.children.First(dir => dir.type == UrlDir.DirectoryType.GameData && dir.name == "");

            foreach (UrlDir.UrlConfig mod in gameDatabaseRoot.AllConfigs.ToArray())
            {
                UrlDir.UrlConfig currentMod = mod;
                try
                {
                    if (mod.config.name == null)
                    {
                        progress.Error(currentMod, "Error - Node in file " + currentMod.parent.url + " subnode: " + currentMod.type +
                                " has config.name == null");
                    }

                    UrlDir.UrlConfig newMod;

                    if (currentMod.type.IndexOf(":NEEDS[", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        string type = currentMod.type;

                        if (CheckNeeds(ref type, mods, gameData))
                        {

                            ConfigNode copy = new ConfigNode(type);
                            copy.ShallowCopyFrom(currentMod.config);
                            int index = mod.parent.configs.IndexOf(currentMod);
                            newMod = new UrlDir.UrlConfig(currentMod.parent, copy);
                            mod.parent.configs[index] = newMod;
                        }
                        else
                        {
                            progress.NeedsUnsatisfiedRoot(currentMod);
                            mod.parent.configs.Remove(currentMod);
                            continue;
                        }
                    }
                    else
                    {
                        newMod = currentMod;
                    }

                    // Recursively check the contents
                    PatchContext context = new PatchContext(newMod, gameDatabaseRoot, logger, progress);
                    CheckNeeds(new NodeStack(newMod.config), context, mods, gameData);
                }
                catch (Exception ex)
                {
                    try
                    {
                        mod.parent.configs.Remove(currentMod);
                    }
                    catch(Exception ex2)
                    {
                        logger.Exception("Exception while attempting to ensure config removed" ,ex2);
                    }

                    try
                    {
                        progress.Exception(mod, "Exception while checking needs on root node :\n" + mod.PrettyPrint(), ex);
                    }
                    catch (Exception ex2)
                    {
                        progress.Exception("Exception while attempting to log an exception", ex2);
                    }
                }
            }
        }

        private static void CheckNeeds(NodeStack stack, PatchContext context, IEnumerable<string> mods, UrlDir gameData)
        {
            ConfigNode original = stack.value;
            for (int i = 0; i < original.values.Count; ++i)
            {
                ConfigNode.Value val = original.values[i];
                string valname = val.name;
                try
                {
                    if (CheckNeeds(ref valname, mods, gameData))
                    {
                        val.name = valname;
                    }
                    else
                    {
                        original.values.Remove(val);
                        i--;
                        context.progress.NeedsUnsatisfiedValue(context.patchUrl, stack, val.name);
                    }
                }
                catch (ArgumentOutOfRangeException e)
                {
                    context.progress.Exception("ArgumentOutOfRangeException in CheckNeeds for value \"" + val.name + "\"", e);
                    throw;
                }
                catch (Exception e)
                {
                    context.progress.Exception("General Exception in CheckNeeds for value \"" + val.name + "\"", e);
                    throw;
                }
            }

            for (int i = 0; i < original.nodes.Count; ++i)
            {
                ConfigNode node = original.nodes[i];
                string nodeName = node.name;

                if (nodeName == null)
                {
                    context.progress.Error(context.patchUrl, "Error - Node in file " + context.patchUrl.SafeUrl() + " subnode: " + stack.GetPath() +
                            " has config.name == null");
                }

                try
                {
                    if (CheckNeeds(ref nodeName, mods, gameData))
                    {
                        node.name = nodeName;
                        CheckNeeds(stack.Push(node), context, mods, gameData);
                    }
                    else
                    {
                        original.nodes.Remove(node);
                        i--;
                        context.progress.NeedsUnsatisfiedNode(context.patchUrl, stack.Push(node));
                    }
                }
                catch (ArgumentOutOfRangeException e)
                {
                    context.progress.Exception("ArgumentOutOfRangeException in CheckNeeds for node \"" + node.name + "\"", e);
                    throw;
                }
                catch (Exception e)
                {
                    context.progress.Exception("General Exception " + e.GetType().Name + " for node \"" + node.name + "\"", e);
                    throw;
                }
            }
        }

        /// <summary>
        /// Returns true if needs are satisfied.
        /// </summary>
        private static bool CheckNeeds(ref string name, IEnumerable<string> mods, UrlDir gameData)
        {
            if (name == null)
                return true;

            int idxStart = name.IndexOf(":NEEDS[", StringComparison.OrdinalIgnoreCase);
            if (idxStart < 0)
                return true;
            int idxEnd = name.IndexOf(']', idxStart + 7);
            string needsString = name.Substring(idxStart + 7, idxEnd - idxStart - 7);

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

                    bool found = mods.Contains(toFind.ToUpper(), StringComparer.OrdinalIgnoreCase);
                    if (!found && toFind.Contains('/'))
                    {
                        string[] splits = toFind.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                        found = true;
                        UrlDir current = gameData;
                        for (int i = 0; i < splits.Length; i++)
                        {
                            current = current.children.FirstOrDefault(dir => dir.name == splits[i]);
                            if (current == null)
                            {
                                found = false;
                                break;
                            }
                        }
                    }

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
    }
}
