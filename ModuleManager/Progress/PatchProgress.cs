using System;
using ModuleManager.Extensions;
using ModuleManager.Logging;
using NodeStack = ModuleManager.Collections.ImmutableStack<ConfigNode>;

namespace ModuleManager.Progress
{
    public class PatchProgress : IPatchProgress
    {
        public ProgressCounter Counter { get; private set; }

        private IBasicLogger logger;

        public float ProgressFraction
        {
            get
            {
                if (Counter.totalPatches > 0)
                    return (Counter.appliedPatches + Counter.needsUnsatisfied) / (float)Counter.totalPatches;
                return 0;
            }
        }

        public PatchProgress(IBasicLogger logger)
        {
            this.logger = logger;
            Counter = new ProgressCounter();
        }

        public PatchProgress(IPatchProgress progress, IBasicLogger logger)
        {
            this.logger = logger;
            Counter = progress.Counter;
        }

        public void PatchAdded()
        {
            Counter.totalPatches.Increment();
        }

        public void ApplyingUpdate(UrlDir.UrlConfig original, UrlDir.UrlConfig patch)
        {
            logger.Info($"Applying update {patch.SafeUrl()} to {original.SafeUrl()}");
            Counter.patchedNodes.Increment();
        }

        public void ApplyingCopy(UrlDir.UrlConfig original, UrlDir.UrlConfig patch)
        {
            logger.Info($"Applying copy {patch.SafeUrl()} to {original.SafeUrl()}");
            Counter.patchedNodes.Increment();
        }

        public void ApplyingDelete(UrlDir.UrlConfig original, UrlDir.UrlConfig patch)
        {
            logger.Info($"Applying delete {patch.SafeUrl()} to {original.SafeUrl()}");
            Counter.patchedNodes.Increment();
        }

        public void PatchApplied()
        {
            Counter.appliedPatches.Increment();
        }

        public void NeedsUnsatisfiedRoot(UrlDir.UrlConfig url)
        {
            logger.Info($"Deleting root node in file {url.parent.url} node: {url.type} as it can't satisfy its NEEDS");
            Counter.needsUnsatisfied.Increment();
        }

        public void NeedsUnsatisfiedNode(UrlDir.UrlConfig url, NodeStack path)
        {
            logger.Info($"Deleting node in file {url.parent.url} subnode: {path.GetPath()} as it can't satisfy its NEEDS");
        }

        public void NeedsUnsatisfiedValue(UrlDir.UrlConfig url, NodeStack path, string valName)
        {
            logger.Info($"Deleting value in file {url.parent.url} subnode: {path.GetPath()} value: {valName} as it can't satisfy its NEEDS");
        }

        public void NeedsUnsatisfiedBefore(UrlDir.UrlConfig url)
        {
            logger.Info($"Deleting root node in file {url.parent.url} node: {url.type} as it can't satisfy its BEFORE");
            Counter.needsUnsatisfied.Increment();
        }

        public void NeedsUnsatisfiedFor(UrlDir.UrlConfig url)
        {
            logger.Warning($"Deleting root node in file {url.parent.url} node: {url.type} as it can't satisfy its FOR (this shouldn't happen)");
            Counter.needsUnsatisfied.Increment();
        }

        public void NeedsUnsatisfiedAfter(UrlDir.UrlConfig url)
        {
            logger.Info($"Deleting root node in file {url.parent.url} node: {url.type} as it can't satisfy its AFTER");
            Counter.needsUnsatisfied.Increment();
        }

        public void Error(UrlDir.UrlConfig url, string message)
        {
            Counter.errors.Increment();
            logger.Error(message);
            RecordErrorFile(url);
        }

        public void Exception(string message, Exception exception)
        {
            Counter.exceptions.Increment();
            logger.Exception(message, exception);
        }

        public void Exception(UrlDir.UrlConfig url, string message, Exception exception)
        {
            Exception(message, exception);
            RecordErrorFile(url);
        }

        private void RecordErrorFile(UrlDir.UrlConfig url)
        {
            string key = url.parent.url + "." + url.parent.fileExtension;
            if (key[0] == '/')
                key = key.Substring(1);

            if (Counter.errorFiles.ContainsKey(key))
                Counter.errorFiles[key] += 1;
            else
                Counter.errorFiles[key] = 1;
        }
    }
}
