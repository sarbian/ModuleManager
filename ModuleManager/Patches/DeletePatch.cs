using System;
using ModuleManager.Extensions;
using ModuleManager.Logging;
using ModuleManager.Progress;

namespace ModuleManager.Patches
{
    public class DeletePatch : IPatch
    {
        public UrlDir.UrlConfig UrlConfig { get; }
        public INodeMatcher NodeMatcher { get; }

        public DeletePatch(UrlDir.UrlConfig urlConfig, INodeMatcher nodeMatcher)
        {
            UrlConfig = urlConfig ?? throw new ArgumentNullException(nameof(urlConfig));
            NodeMatcher = nodeMatcher ?? throw new ArgumentNullException(nameof(nodeMatcher));
        }

        public void Apply(UrlDir.UrlFile file, IPatchProgress progress, IBasicLogger logger)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            int i = 0;
            while (i < file.configs.Count)
            {
                UrlDir.UrlConfig url = file.configs[i];
                try
                {
                    if (NodeMatcher.IsMatch(url.config))
                    {
                        progress.ApplyingDelete(url, UrlConfig);
                        file.configs.RemoveAt(i);
                    }
                    else
                    {
                        i++;
                    }
                }
                catch (Exception ex)
                {
                    progress.Exception(UrlConfig, $"Exception while applying delete {UrlConfig.SafeUrl()} to {url.SafeUrl()}", ex);
                }
            }
        }
    }
}
