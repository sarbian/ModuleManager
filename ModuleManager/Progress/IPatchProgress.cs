using System;

namespace ModuleManager.Progress
{
    public interface IPatchProgress
    {
        ProgressCounter Counter { get; }

        float ProgressFraction { get; }

        void Warning(UrlDir.UrlConfig url, string message);
        void Error(UrlDir.UrlConfig url, string message);
        void Exception(string message, Exception exception);
        void Exception(UrlDir.UrlConfig url, string message, Exception exception);
        void NeedsUnsatisfiedRoot(UrlDir.UrlConfig url);
        void NeedsUnsatisfiedNode(UrlDir.UrlConfig url, string path);
        void NeedsUnsatisfiedValue(UrlDir.UrlConfig url, string path);
        void NeedsUnsatisfiedBefore(UrlDir.UrlConfig url);
        void NeedsUnsatisfiedFor(UrlDir.UrlConfig url);
        void NeedsUnsatisfiedAfter(UrlDir.UrlConfig url);
        void ApplyingCopy(UrlDir.UrlConfig original, UrlDir.UrlConfig patch);
        void ApplyingDelete(UrlDir.UrlConfig original, UrlDir.UrlConfig patch);
        void ApplyingUpdate(UrlDir.UrlConfig original, UrlDir.UrlConfig patch);
        void PatchAdded();
        void PatchApplied();
    }
}
