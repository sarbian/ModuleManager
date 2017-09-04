using System;
using System.Collections.Generic;

namespace ModuleManager
{
    public interface IPatchProgress
    {
        int AppliedPatchCount { get; }
        int ErrorCount { get; }
        int ExceptionCount { get; }
        int NeedsUnsatisfiedCount { get; }
        int PatchedNodeCount { get; set; }
        float ProgressFraction { get; }
        int TotalPatchCount { get; }
        Dictionary<String, int> ErrorFiles { get; }

        void Error(UrlDir.UrlConfig url, string message);
        void Exception(string message, Exception exception);
        void Exception(UrlDir.UrlConfig url, string message, Exception exception);
        void NeedsUnsatisfiedRoot(string url, string name);
        void NeedsUnsatisfiedNode(string url, string path);
        void NeedsUnsatisfiedValue(string url, string path, string valName);
        void ApplyingCopy(string url, string patchUrl);
        void ApplyingDelete(string url, string patchUrl);
        void ApplyingUpdate(string url, string patchUrl);
        void PatchAdded();
        void PatchApplied();
    }
}
