using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ModuleManager.Extensions;
using ModuleManager.Logging;
using ModuleManager.Progress;
using NodeStack = ModuleManager.Collections.ImmutableStack<ConfigNode>;

namespace ModuleManager
{
    public interface INeedsChecker
    {
        bool CheckNeeds(string mod);
        bool CheckNeedsExpression(string needsString);
        void CheckNeedsRecursive(ConfigNode node, UrlDir.UrlConfig urlConfig);
    }

    public class NeedsChecker : INeedsChecker
    {
        private readonly IEnumerable<string> mods;
        private readonly UrlDir gameData;
        private readonly IPatchProgress progress;
        [SuppressMessage("CodeQuality", "IDE0052", Justification = "Reserved for future use")]
        private readonly IBasicLogger logger;

        public NeedsChecker(IEnumerable<string> mods, UrlDir gameData, IPatchProgress progress, IBasicLogger logger)
        {
            this.mods = mods ?? throw new ArgumentNullException(nameof(mods));
            this.gameData = gameData ?? throw new ArgumentNullException(nameof(gameData));
            this.progress = progress ?? throw new ArgumentNullException(nameof(progress));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool CheckNeeds(string mod)
        {
            if (mod == null) throw new ArgumentNullException(nameof(mod));
            if (mod == string.Empty) throw new ArgumentException("can't be empty", nameof(mod));
            return mods.Contains(mod, StringComparer.InvariantCultureIgnoreCase);
        }

        public bool CheckNeedsExpression(string needsExpression)
        {
            if (needsExpression == null) throw new ArgumentNullException(nameof(needsExpression));
            if (needsExpression == string.Empty) throw new ArgumentException("can't be empty", nameof(needsExpression));

            foreach (string andDependencies in needsExpression.Split(',', '&'))
            {
                bool orMatch = false;
                foreach (string orDependency in andDependencies.Split('|'))
                {
                    if (orDependency.Length == 0)
                        continue;

                    bool not = orDependency[0] == '!';
                    string toFind = not ? orDependency.Substring(1) : orDependency;

                    bool found = CheckNeedsWithDirectories(toFind);

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

        public void CheckNeedsRecursive(ConfigNode node, UrlDir.UrlConfig urlConfig)
        {
            if (node == null) throw new ArgumentNullException(nameof(node));
            if (urlConfig == null) throw new ArgumentNullException(nameof(urlConfig));
            CheckNeedsRecursive(new NodeStack(node), urlConfig);
        }

        private bool CheckNeedsWithDirectories(string mod)
        {
            if (CheckNeeds(mod)) return true;
            if (mod.Contains('/'))
            {
                string[] splits = mod.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                bool result = true;
                UrlDir current = gameData;
                for (int i = 0; i < splits.Length; i++)
                {
                    current = current.children.FirstOrDefault(dir => dir.name == splits[i]);
                    if (current == null)
                    {
                        result = false;
                        break;
                    }
                }
                return result;
            }
            return false;
        }

        private bool CheckNeedsName(ref string name)
        {
            if (name == null)
                return true;

            int idxStart = name.IndexOf(":NEEDS[", StringComparison.OrdinalIgnoreCase);
            if (idxStart < 0)
                return true;
            int idxEnd = name.IndexOf(']', idxStart + 7);
            string needsString = name.Substring(idxStart + 7, idxEnd - idxStart - 7);

            name = name.Substring(0, idxStart) + name.Substring(idxEnd + 1);

            return CheckNeedsExpression(needsString);
        }

        private void CheckNeedsRecursive(NodeStack nodeStack, UrlDir.UrlConfig urlConfig)
        {
            ConfigNode original = nodeStack.value;
            for (int i = 0; i < original.values.Count; ++i)
            {
                ConfigNode.Value val = original.values[i];
                string valname = val.name;
                try
                {
                    if (CheckNeedsName(ref valname))
                    {
                        val.name = valname;
                    }
                    else
                    {
                        original.values.Remove(val);
                        i--;
                        progress.NeedsUnsatisfiedValue(urlConfig, nodeStack.GetPath() + '/' + val.name);
                    }
                }
                catch (ArgumentOutOfRangeException e)
                {
                    progress.Exception("ArgumentOutOfRangeException in CheckNeeds for value \"" + val.name + "\"", e);
                    throw;
                }
                catch (Exception e)
                {
                    progress.Exception("General Exception in CheckNeeds for value \"" + val.name + "\"", e);
                    throw;
                }
            }

            for (int i = 0; i < original.nodes.Count; ++i)
            {
                ConfigNode node = original.nodes[i];
                string nodeName = node.name;

                if (nodeName == null)
                {
                    progress.Error(urlConfig, "Error - Node in file " + urlConfig.SafeUrl() + " subnode: " + nodeStack.GetPath() + " has config.name == null");
                }

                try
                {
                    if (CheckNeedsName(ref nodeName))
                    {
                        node.name = nodeName;
                        CheckNeedsRecursive(nodeStack.Push(node), urlConfig);
                    }
                    else
                    {
                        original.nodes.Remove(node);
                        i--;
                        progress.NeedsUnsatisfiedNode(urlConfig, nodeStack.GetPath() + '/' + node.name);
                    }
                }
                catch (ArgumentOutOfRangeException e)
                {
                    progress.Exception("ArgumentOutOfRangeException in CheckNeeds for node \"" + node.name + "\"", e);
                    throw;
                }
                catch (Exception e)
                {
                    progress.Exception("General Exception " + e.GetType().Name + " for node \"" + node.name + "\"", e);
                    throw;
                }
            }
        }
    }
}
