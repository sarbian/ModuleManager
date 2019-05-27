using System;
using System.Linq;
using System.Text;
using ModuleManager.Extensions;
using ModuleManager.Logging;
using ModuleManager.Utils;

namespace ModuleManager
{
    public interface IPartShaGenerator
    {
        string ComputePartSha(IProtoUrlConfig partUrlConfig);
    }

    public class PartShaGenerator : IPartShaGenerator
    {
        private readonly IFileShaGenerator fileShaGenerator;
        private readonly IUrlFileToReadableObjectConverter urlFileToReadableObjectConverter;
        private readonly UrlDir gameData;
        private readonly IBasicLogger logger;

        public PartShaGenerator(IFileShaGenerator fileShaGenerator, IUrlFileToReadableObjectConverter urlFileToReadableObjectConverter, UrlDir gameData, IBasicLogger logger)
        {
            this.fileShaGenerator = fileShaGenerator ?? throw new ArgumentNullException(nameof(fileShaGenerator));
            this.urlFileToReadableObjectConverter = urlFileToReadableObjectConverter ?? throw new ArgumentNullException(nameof(urlFileToReadableObjectConverter));
            this.gameData = gameData ?? throw new ArgumentNullException(nameof(gameData));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string ComputePartSha(IProtoUrlConfig partUrlConfig)
        {
            System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create();
            byte[] configBytes = Encoding.UTF8.GetBytes(partUrlConfig.Node.ToString());
            sha.TransformBlock(configBytes, 0, configBytes.Length, configBytes, 0);

            if (partUrlConfig.Node.GetValue("model") is string modelName)
            {
                UrlDir.UrlFile modelFile = partUrlConfig.UrlFile.parent.files.FirstOrDefault(file => file.fileType == UrlDir.FileType.Model);

                if (modelFile == null)
                {
                    logger.Error($"Unable to find model for part '{partUrlConfig.FullUrl}'");
                    return null;
                }
                else
                {
                    IReadableObject modelReadableObject = urlFileToReadableObjectConverter.ConvertToReadableObject(modelFile);
                    fileShaGenerator.TransformBlock(sha, modelReadableObject);
                }
            }

            foreach (ConfigNode subNode in partUrlConfig.Node.nodes)
            {
                if (subNode.name != "MODEL") continue;
                if (!(subNode.GetValue("model") is string modelUrl))
                {
                    logger.Error($"Part has MODEL node without model value: {partUrlConfig.FullUrl}");
                    return null;
                }
                if (!(gameData.FindFile(modelUrl, UrlDir.FileType.Model) is UrlDir.UrlFile modelFile))
                {
                    logger.Error($"Unable to find model file for part '{partUrlConfig.FullUrl}' model url '{modelUrl}'");
                    return null;
                }
                else
                {
                    IReadableObject modelReadableObject = urlFileToReadableObjectConverter.ConvertToReadableObject(modelFile);
                    fileShaGenerator.TransformBlock(sha, modelReadableObject);
                }
            }

            byte[] godsFinalMessageToHisCreation = Encoding.UTF8.GetBytes("We apologize for the inconvenience.");
            sha.TransformFinalBlock(godsFinalMessageToHisCreation, 0, godsFinalMessageToHisCreation.Length);

            string partSha = BitConverter.ToString(sha.Hash);
            sha.Clear();

            return partSha;
        }
    }
}
