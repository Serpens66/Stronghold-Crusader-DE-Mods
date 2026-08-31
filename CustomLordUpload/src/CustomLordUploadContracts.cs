using System;
using System.Collections.Generic;

namespace CustomLordUpload
{
    public sealed class CustomLordUploadStagingSummary
    {
        internal CustomLordUploadStagingSummary(
            string sourcePath,
            string destinationPath,
            int packageFileCount,
            long packageByteCount,
            int copiedFileCount,
            int existingFileCount)
        {
            SourcePath = sourcePath;
            DestinationPath = destinationPath;
            PackageFileCount = packageFileCount;
            PackageByteCount = packageByteCount;
            CopiedFileCount = copiedFileCount;
            ExistingFileCount = existingFileCount;
        }

        public string SourcePath { get; }
        public string DestinationPath { get; }
        public int PackageFileCount { get; }
        public long PackageByteCount { get; }
        public int CopiedFileCount { get; }
        public int ExistingFileCount { get; }
    }

    internal interface ICustomLordUploadStager
    {
        bool TryResolveSource(string mapTitle, out string sourcePath, out string error);

        bool TryExtendStaging(
            string uploadContentRoot,
            string mapTitle,
            out CustomLordUploadStagingSummary? summary,
            out string error);
    }

    internal interface ICustomLordUploadConfirmation
    {
        void Show(
            IReadOnlyList<CustomLordUploadIssue> issues,
            Action confirm,
            Action cancel);
    }
}
