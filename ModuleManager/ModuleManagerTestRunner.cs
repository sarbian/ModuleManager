using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ModuleManager.Extensions;
using ModuleManager.Logging;

namespace ModuleManager
{
    public class InGameTestRunner
    {
        private readonly IBasicLogger logger;

        public InGameTestRunner(IBasicLogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void RunTestCases(UrlDir gameDatabaseRoot)
        {
            if (gameDatabaseRoot == null) throw new ArgumentNullException(nameof(gameDatabaseRoot));
            logger.Info("Running tests...");

            foreach (UrlDir.UrlConfig expect in gameDatabaseRoot.GetConfigs("MMTEST_EXPECT"))
            {
                // So for each of the expects, we expect all the configs before that node to match exactly.
                UrlDir.UrlFile parent = expect.parent;
                if (parent.configs.Count != expect.config.CountNodes + 1)
                {
                    logger.Error("Test " + parent.name + " failed as expected number of nodes differs expected: " +
                        expect.config.CountNodes + " found: " + (parent.configs.Count - 1));
                    for (int i = 0; i < parent.configs.Count; ++i)
                        logger.Info(parent.configs[i].config.ToString());
                    continue;
                }
                for (int i = 0; i < expect.config.CountNodes; ++i)
                {
                    ConfigNode gotNode = parent.configs[i].config;
                    ConfigNode expectNode = expect.config.nodes[i];
                    if (!CompareRecursive(expectNode, gotNode))
                    {
                        logger.Error("Test " + parent.name + "[" + i +
                            "] failed as expected output and actual output differ.\nexpected:\n" + expectNode +
                            "\nActually got:\n" + gotNode);
                    }
                }

                // Purge the tests
                parent.configs.Clear();
            }
            logger.Info("tests complete.");
        }

        private static bool CompareRecursive(ConfigNode expectNode, ConfigNode gotNode)
        {
            if (expectNode.values.Count != gotNode.values.Count || expectNode.nodes.Count != gotNode.nodes.Count)
                return false;
            for (int i = 0; i < expectNode.values.Count; ++i)
            {
                ConfigNode.Value eVal = expectNode.values[i];
                ConfigNode.Value gVal = gotNode.values[i];
                if (eVal.name != gVal.name || eVal.value != gVal.value)
                    return false;
            }
            for (int i = 0; i < expectNode.nodes.Count; ++i)
            {
                ConfigNode eNode = expectNode.nodes[i];
                ConfigNode gNode = gotNode.nodes[i];
                if (!CompareRecursive(eNode, gNode))
                    return false;
            }
            return true;
        }
    }
}
