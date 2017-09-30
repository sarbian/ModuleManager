using System;
using System.Collections.Generic;
using System.Linq;
using ModuleManager.Extensions;
using ModuleManager.Logging;
using NodeStack = ModuleManager.Collections.ImmutableStack<ConfigNode>;

namespace ModuleManager
{
    public static class NeedsChecker
    {
        public static void CheckNeeds(UrlDir gameDatabaseRoot, IEnumerable<string> mods, IPatchProgress progress, IBasicLogger logger)
        {
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

                    if (currentMod.type.IndexOf(":NEEDS[", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        mod.parent.configs.Remove(currentMod);
                        string type = currentMod.type;

                        if (!CheckNeeds(ref type, mods))
                        {
                            progress.NeedsUnsatisfiedRoot(currentMod);
                            continue;
                        }

                        ConfigNode copy = new ConfigNode(type);
                        copy.ShallowCopyFrom(currentMod.config);
                        currentMod = new UrlDir.UrlConfig(currentMod.parent, copy);
                        mod.parent.configs.Add(currentMod);
                    }

                    // Recursively check the contents
                    PatchContext context = new PatchContext(mod, gameDatabaseRoot, logger, progress);
                    CheckNeeds(new NodeStack(mod.config), context, mods);
                }
                catch (Exception ex)
                {
                    try
                    {
                        progress.Exception(currentMod, "Exception while checking needs on root node :\n" + currentMod.PrettyPrint(), ex);
                    }
                    catch (Exception ex2)
                    {
                        progress.Exception("Exception while attempting to log an exception", ex2);
                    }
                }
            }
        }

        private static void CheckNeeds(NodeStack stack, PatchContext context, IEnumerable<string> mods)
        {
            bool needsCopy = false;
            ConfigNode original = stack.value;
            ConfigNode copy = new ConfigNode(original.name);
            for (int i = 0; i < original.values.Count; ++i)
            {
                ConfigNode.Value val = original.values[i];
                string valname = val.name;
                try
                {
                    if (CheckNeeds(ref valname, mods))
                    {
                        copy.AddValue(valname, val.value);
                    }
                    else
                    {
                        needsCopy = true;
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
                    if (CheckNeeds(ref nodeName, mods))
                    {
                        node.name = nodeName;
                        CheckNeeds(stack.Push(node), context, mods);
                        copy.AddNode(node);
                    }
                    else
                    {
                        needsCopy = true;
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

            if (needsCopy)
                original.ShallowCopyFrom(copy);
        }

        /// <summary>
        /// Returns true if needs are satisfied.
        /// </summary>
        private static bool CheckNeeds(ref string name, IEnumerable<string> mods)
        {
            if (name == null)
                return true;

            int idxStart = name.IndexOf(":NEEDS[", StringComparison.OrdinalIgnoreCase);
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
                    bool found = mods.Contains(toFind, StringComparer.OrdinalIgnoreCase);

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
