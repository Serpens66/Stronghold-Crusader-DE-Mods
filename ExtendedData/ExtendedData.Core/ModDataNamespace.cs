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

    internal static class ModDataNamespaceReader
    {
        public static ExtendedDataModDataReadResult Read(string json, string modGuid, string source)
        {
            if (string.IsNullOrWhiteSpace(modGuid))
            {
                return ExtendedDataModDataReadResult.InvalidRequest(
                    modGuid,
                    source,
                    "A non-empty mod GUID is required.");
            }

            Dictionary<string, object> root;
            try
            {
                root = Shared.DependencyFreeJson.Parse(json) as Dictionary<string, object>;
            }
            catch (Exception exception) when (exception is InvalidDataException || exception is FormatException)
            {
                return ExtendedDataModDataReadResult.InvalidDocument(
                    modGuid,
                    source,
                    "The mod-data file is not valid JSON: " + exception.Message);
            }

            if (root == null)
            {
                return ExtendedDataModDataReadResult.InvalidDocument(
                    modGuid,
                    source,
                    "The mod-data document root must be a JSON object.");
            }

            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            KeyValuePair<string, object>? requested = null;
            foreach (KeyValuePair<string, object> entry in root)
            {
                if (string.IsNullOrWhiteSpace(entry.Key))
                {
                    return ExtendedDataModDataReadResult.InvalidDocument(
                        modGuid,
                        source,
                        "Mod GUID keys must not be empty.");
                }
                if (!keys.Add(entry.Key))
                {
                    return ExtendedDataModDataReadResult.InvalidDocument(
                        modGuid,
                        source,
                        "The document contains mod GUID keys that differ only by letter casing.");
                }
                if (!(entry.Value is Dictionary<string, object>))
                {
                    return ExtendedDataModDataReadResult.InvalidDocument(
                        modGuid,
                        source,
                        "Every mod GUID namespace must be a JSON object.");
                }
                if (string.Equals(entry.Key, modGuid, StringComparison.OrdinalIgnoreCase))
                    requested = entry;
            }

            if (!requested.HasValue)
            {
                return new ExtendedDataModDataReadResult(
                    ExtendedDataModDataReadStatus.NamespaceNotFound,
                    modGuid,
                    source,
                    null,
                    null,
                    "The requested mod GUID namespace is not present.");
            }

            var namespaceObject = (Dictionary<string, object>)requested.Value.Value;
            string namespaceJson = Shared.DependencyFreeJson.Serialize(namespaceObject);
            return new ExtendedDataModDataReadResult(
                ExtendedDataModDataReadStatus.Success,
                requested.Value.Key,
                source,
                FreezeObject(namespaceObject),
                namespaceJson,
                string.Empty);
        }

        private static IReadOnlyDictionary<string, object> FreezeObject(Dictionary<string, object> source)
        {
            var copy = new Dictionary<string, object>(source.Count, StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> entry in source)
                copy.Add(entry.Key, FreezeValue(entry.Value));
            return new ReadOnlyDictionary<string, object>(copy);
        }

        private static object FreezeValue(object value)
        {
            if (value is Dictionary<string, object> objectValue)
                return FreezeObject(objectValue);
            if (value is List<object> arrayValue)
            {
                var copy = new List<object>(arrayValue.Count);
                foreach (object item in arrayValue)
                    copy.Add(FreezeValue(item));
                return new ReadOnlyCollection<object>(copy);
            }
            return value;
        }
    }
}
