using System;
using NodeStack = ModuleManager.Collections.ImmutableStack<ConfigNode>;
using ModuleManager.Extensions;
using ModuleManager.Logging;
using ModuleManager.Progress;

namespace ModuleManager.Patches
{
    public class CopyPatch : IPatch
    {
        public UrlDir.UrlConfig UrlConfig { get; }
        public INodeMatcher NodeMatcher { get; }

        public CopyPatch(UrlDir.UrlConfig urlConfig, INodeMatcher nodeMatcher)
        {
            UrlConfig = urlConfig ?? throw new ArgumentNullException(nameof(urlConfig));
            NodeMatcher = nodeMatcher ?? throw new ArgumentNullException(nameof(nodeMatcher));
        }

        public void Apply(UrlDir.UrlFile file, IPatchProgress progress, IBasicLogger logger)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            if (progress == null) throw new ArgumentNullException(nameof(progress));
            if (logger == null) throw new ArgumentNullException(nameof(logger));

            PatchContext context = new PatchContext(UrlConfig, file.root, logger, progress);

            // Avoid checking the new configs we are creating
            int count = file.configs.Count;
            for (int i = 0; i < count; i++)
            {
                UrlDir.UrlConfig url = file.configs[i];
                try
                {
                    if (!NodeMatcher.IsMatch(url.config)) continue;

                    ConfigNode clone = MMPatchLoader.ModifyNode(new NodeStack(url.config), UrlConfig.config, context);
                    if (url.config.HasValue("name") && url.config.GetValue("name") == clone.GetValue("name"))
                    {
                        progress.Error(UrlConfig, $"Error - when applying copy {UrlConfig.SafeUrl()} to {url.SafeUrl()} - the copy needs to have a different name than the parent (use @name = xxx)");
                    }
                    else
                    {
                        progress.ApplyingCopy(url, UrlConfig);
                        file.AddConfig(clone);
                    }
                }
                catch (Exception ex)
                {
                    progress.Exception(UrlConfig, $"Exception while applying copy {UrlConfig.SafeUrl()} to {url.SafeUrl()}", ex);
                }
            }
        }
    }
}
