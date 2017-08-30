using System;
using System.Collections.Generic;
using ModuleManager.Logging;

namespace ModuleManager
{
    public class PatchProgress : IPatchProgress
    {
        public int TotalPatchCount { get; private set; } = 0;

        public int AppliedPatchCount { get; private set; } = 0;

        public int PatchedNodeCount { get; set; } = 0;

        public int ErrorCount { get; private set; } = 0;

        public int ExceptionCount { get; private set; } = 0;

        public int NeedsUnsatisfiedCount { get; private set; } = 0;

        public Dictionary<String, int> ErrorFiles { get; } = new Dictionary<string, int>();

        private IBasicLogger logger;

        public float ProgressFraction
        {
            get
            {
                if (TotalPatchCount > 0)
                    return (AppliedPatchCount + NeedsUnsatisfiedCount) / (float)TotalPatchCount;
                return 0;
            }
        }

        public PatchProgress(IBasicLogger logger)
        {
            this.logger = logger;
        }

        public void PatchAdded()
        {
            TotalPatchCount += 1;
        }

        public void NodePatched(string url, string patchUrl)
        {
            logger.Info($"Applying update {patchUrl} to {url}");
            PatchedNodeCount += 1;
        }

        public void NodeCopied(string url, string patchUrl)
        {
            logger.Info($"Applying copy {patchUrl} to {url}");
            PatchedNodeCount += 1;
        }

        public void NodeDeleted(string url, string patchUrl)
        {
            logger.Info($"Applying delete {patchUrl} to {url}");
            PatchedNodeCount += 1;
        }

        public void PatchApplied()
        {
            AppliedPatchCount += 1;
        }

        public void NeedsUnsatisfiedNode(string url, string path)
        {
            logger.Info($"Deleting Node in file {url} subnode: {path} as it can't satisfy its NEEDS");
            NeedsUnsatisfiedCount += 1;
        }

        public void NeedsUnsatisfiedValue(string url, string path, string valName)
        {
            logger.Info($"Deleting value in file {url} subnode: {path} value: {valName} as it can't satisfy its NEEDS");
            NeedsUnsatisfiedCount += 1;
        }

        public void Error(UrlDir.UrlConfig url, string message)
        {
            ErrorCount += 1;
            logger.Error(message);
            RecordErrorFile(url);
        }

        public void Exception(string message, Exception exception)
        {
            ExceptionCount += 1;
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

            if (ErrorFiles.ContainsKey(key))
                ErrorFiles[key] += 1;
            else
                ErrorFiles[key] = 1;
        }
    }
}
