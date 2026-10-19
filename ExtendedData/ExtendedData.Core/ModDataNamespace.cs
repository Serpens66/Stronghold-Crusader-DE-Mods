using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;

namespace ExtendedData
{
    public enum ExtendedDataModDataReadStatus
    {
        Success,
        FileNotFound,
        NamespaceNotFound,
        InvalidDocument,
        ReadError,
        InvalidRequest,
        HostDataUnavailable,
    }

    public sealed class ExtendedDataModDataReadResult
    {
        internal ExtendedDataModDataReadResult(
            ExtendedDataModDataReadStatus status,
            string modGuid,
            string source,
            IReadOnlyDictionary<string, object> data,
            string json,
            string diagnostic)
        {
            Status = status;
            ModGuid = modGuid ?? string.Empty;
            Source = source ?? string.Empty;
            Data = data;
            Json = json;
            Diagnostic = diagnostic ?? string.Empty;
        }

        public ExtendedDataModDataReadStatus Status { get; }
        public bool Success => Status == ExtendedDataModDataReadStatus.Success;
        public string ModGuid { get; }
        public string Source { get; }
        public IReadOnlyDictionary<string, object> Data { get; }
        public string Json { get; }
        public string Diagnostic { get; }

        internal static ExtendedDataModDataReadResult FileNotFound(string modGuid, string source) =>
            Failure(ExtendedDataModDataReadStatus.FileNotFound, modGuid, source, "The mod-data file does not exist.");

        internal static ExtendedDataModDataReadResult ReadError(string modGuid, string source, string diagnostic) =>
            Failure(ExtendedDataModDataReadStatus.ReadError, modGuid, source, diagnostic);

        internal static ExtendedDataModDataReadResult InvalidDocument(string modGuid, string source, string diagnostic) =>
            Failure(ExtendedDataModDataReadStatus.InvalidDocument, modGuid, source, diagnostic);

        internal static ExtendedDataModDataReadResult InvalidRequest(string modGuid, string source, string diagnostic) =>
            Failure(ExtendedDataModDataReadStatus.InvalidRequest, modGuid, source, diagnostic);

        internal static ExtendedDataModDataReadResult HostDataUnavailable(string modGuid, string source, string diagnostic) =>
            Failure(ExtendedDataModDataReadStatus.HostDataUnavailable, modGuid, source, diagnostic);

        private static ExtendedDataModDataReadResult Failure(
            ExtendedDataModDataReadStatus status,
            string modGuid,
            string source,
            string diagnostic) =>
            new ExtendedDataModDataReadResult(status, modGuid, source, null, null, diagnostic);
    }

    // Preserve ExtendedData.Core's published type identities; validation lives in APIShared.Content.
    internal static class ModDataNamespaceReader
    {
        public static ExtendedDataModDataReadResult Read(string json, string modGuid, string source)
        {
            var result = APIShared.ModData.ModDataNamespaceReader.Read(json, modGuid, source);
            return new ExtendedDataModDataReadResult(
                (ExtendedDataModDataReadStatus)(int)result.Status, result.ModGuid, result.Source,
                result.Data, result.Json, result.Diagnostic);
        }
    }
}