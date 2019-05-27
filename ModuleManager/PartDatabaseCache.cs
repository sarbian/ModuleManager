using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ModuleManager.Extensions;
using ModuleManager.Logging;
using ModuleManager.Utils;

using static ModuleManager.FilePathRepository;

namespace ModuleManager
{
    public class PartDatabaseCache
    {
        private readonly IPartShaGenerator partShaGenerator;
        private readonly IBasicLogger logger;

        public PartDatabaseCache(IPartShaGenerator partShaGenerator, IBasicLogger logger)
        {
            this.partShaGenerator = partShaGenerator ?? throw new ArgumentNullException(nameof(partShaGenerator));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void DoTheThing(IEnumerable<IProtoUrlConfig> urlConfigs)
        {
            UrlDir gameData = GameDatabase.Instance.GetGameData();
            ConfigNode newPartDatabaseSha = new ConfigNode();

            foreach (IProtoUrlConfig urlConfig in urlConfigs)
            {
                if (urlConfig.NodeType != "PART") continue;
                string partSha = partShaGenerator.ComputePartSha(urlConfig);
                if (partSha == null)
                {
                    logger.Error($"Could not compute SHA for part '{urlConfig.FullUrl}'");
                    continue;
                }
                newPartDatabaseSha.AddValue($"{urlConfig.UrlFile.url}/{urlConfig.Node.GetValue("name")}", partSha);
            }

            ConfigNode partDatabaseSha = null;
            if (File.Exists(partDatabaseShaPath))
                partDatabaseSha = ConfigNode.Load(partDatabaseShaPath, true);

            newPartDatabaseSha.Save(partDatabaseShaPath);

            if (!File.Exists(partDatabasePath))
            {
                logger.Info("PartDatabase.cfg does not exist");
                return;
            }
            else if (partDatabaseSha == null)
            {
                logger.Info("Part databse SHA does not exist, removing PartDatabase.cfg");
                File.Delete(partDatabasePath);
                return;
            }

            ConfigNode partDatabase = ConfigNode.Load(partDatabasePath, true);

            foreach (ConfigNode partDatabaseNode in partDatabase.nodes)
            {
                if (partDatabaseNode.name != "PART") continue;

                if (!(partDatabaseNode.GetValue("url") is string partUrl))
                {
                    logger.Error("Part database node is missing url");
                    partDatabase.RemoveNode(partDatabaseNode);
                    continue;
                }

                int slashIndex = partUrl.LastIndexOf('/');

                if (slashIndex == -1 || slashIndex == 0 || slashIndex == partUrl.Length - 1)
                {
                    logger.Error($"Malformed part url in part database: '{partUrl}'");
                    partDatabase.RemoveNode(partDatabaseNode);
                    continue;
                }

                string cachedPartSha = partDatabaseSha.GetValue(partUrl);
                string partSha = newPartDatabaseSha.GetValue(partUrl);

                if (cachedPartSha == null)
                {
                    logger.Info($"No cached SHA found for part '{partUrl}'");
                    partDatabase.RemoveNode(partDatabaseNode);
                    continue;
                }
                else if (partSha != cachedPartSha)
                {
                    logger.Info($"Part SHAs differ on part '{partUrl}' - cached: '{cachedPartSha}', actual: '{partSha}'");
                    partDatabase.RemoveNode(partDatabaseNode);
                    continue;
                }
            }

            partDatabase.Save(partDatabasePath);
        }
    }
}
