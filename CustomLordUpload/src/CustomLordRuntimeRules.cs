using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace CustomLordUpload
{
    internal sealed class CustomLordRuntimeRules
    {
        private static readonly string[] KnownIdentityMarkers =
        {
            // COMPATIBILITY: Add a marker only after the complete preflight profile was reviewed against that build.
            "171d68e", // v1.42.0
            "ac291f2", // v1.43.2
            "a7775a6", // custom-lord-details review revision
            "f593de4", // published uploader branch base
            "2560dd2"  // local uploader branch review revision
        };

        private CustomLordRuntimeRules(
            string extenderIdentity,
            bool knownIdentity,
            bool usesVersionedAssetModResolution,
            Dictionary<string, int> messageTypes,
            HashSet<string> lordInfoFields,
            Func<string, IEnumerable<string>>? publicValidator)
        {
            ExtenderIdentity = extenderIdentity;
            IsKnownIdentity = knownIdentity;
            UsesVersionedAssetModResolution = usesVersionedAssetModResolution;
            MessageTypes = messageTypes;
            LordInfoFields = lordInfoFields;
            PublicValidator = publicValidator;
        }

        internal string ExtenderIdentity { get; }
        internal bool IsKnownIdentity { get; }
        internal bool UsesVersionedAssetModResolution { get; }
        internal IReadOnlyDictionary<string, int> MessageTypes { get; }
        internal IReadOnlyCollection<string> LordInfoFields { get; }
        internal Func<string, IEnumerable<string>>? PublicValidator { get; }

        internal static CustomLordRuntimeRules Discover(Assembly extenderAssembly)
        {
            if (extenderAssembly == null)
                throw new ArgumentNullException(nameof(extenderAssembly));

            string identity = GetIdentity(extenderAssembly);
            // COMPATIBILITY: Recheck these public full names after Script Extender API namespace changes.
            return Discover(
                extenderAssembly,
                identity,
                "SHCDESE.API.Components.AI.LordInfo",
                "SHCDESE.Interop.Enums.AILordMessageType");
        }

        internal static CustomLordRuntimeRules CreateCompatibilityProfile(string identity, bool knownIdentity)
        {
            return new CustomLordRuntimeRules(
                identity,
                knownIdentity,
                UsesVersionedAssetModResolutionFor(identity),
                CustomLordCompatibilityProfile.CreateFallbackMessageTypes(),
                CustomLordCompatibilityProfile.CreateFallbackLordInfoFields(),
                null);
        }

        internal static CustomLordRuntimeRules Discover(
            Assembly extenderAssembly,
            string identity,
            string lordInfoTypeName,
            string messageTypeName)
        {
            Dictionary<string, int> messageTypes = ReadMessageTypes(
                extenderAssembly.GetType(messageTypeName, throwOnError: false));
            HashSet<string> fields = ReadLordInfoFields(
                extenderAssembly.GetType(lordInfoTypeName, throwOnError: false));

            bool known = KnownIdentityMarkers.Any(marker =>
                (identity ?? string.Empty).IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0);
            return new CustomLordRuntimeRules(
                string.IsNullOrWhiteSpace(identity) ? "unknown" : identity,
                known,
                UsesVersionedAssetModResolutionFor(identity),
                messageTypes.Count == 0
                    ? CustomLordCompatibilityProfile.CreateFallbackMessageTypes()
                    : messageTypes,
                fields.Count == 0
                    ? CustomLordCompatibilityProfile.CreateFallbackLordInfoFields()
                    : fields,
                FindPublicValidator(extenderAssembly));
        }

        private static bool UsesVersionedAssetModResolutionFor(string identity)
        {
            // COMPATIBILITY: v1.42.0 registers asset mods in discovery order and does not compare
            // info.json versions. The reviewed v1.43.2 and Branch-A builds do compare them.
            return (identity ?? string.Empty).IndexOf("171d68e", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static Dictionary<string, int> ReadMessageTypes(Type? type)
        {
            Dictionary<string, int> result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (type == null || !type.IsEnum || !type.IsPublic)
                return result;

            foreach (string name in Enum.GetNames(type))
                result[name] = Convert.ToInt32(Enum.Parse(type, name));
            return result;
        }

        private static HashSet<string> ReadLordInfoFields(Type? type)
        {
            HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (type == null || !type.IsPublic)
                return result;

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (property.CanRead || property.CanWrite)
                    result.Add(property.Name);
            }
            return result;
        }

        private static Func<string, IEnumerable<string>>? FindPublicValidator(Assembly assembly)
        {
            // COMPATIBILITY: Only this exact public, static, read-only-style contract is invoked.
            // Review its signature and documented side effects before accepting a future API change.
            foreach (Type type in GetExportedTypesSafely(assembly))
            {
                MethodInfo? method = type.GetMethod(
                    "ValidateCustomLordPackage",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string) },
                    null);
                if (method == null || !typeof(IEnumerable<string>).IsAssignableFrom(method.ReturnType))
                    continue;

                return path => (IEnumerable<string>?)method.Invoke(null, new object[] { path })
                    ?? Array.Empty<string>();
            }
            return null;
        }

        private static IEnumerable<Type> GetExportedTypesSafely(Assembly assembly)
        {
            try { return assembly.GetExportedTypes(); }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null).Cast<Type>();
            }
        }

        private static string GetIdentity(Assembly assembly)
        {
            try
            {
                string location = assembly.Location;
                if (!string.IsNullOrWhiteSpace(location))
                {
                    FileVersionInfo version = FileVersionInfo.GetVersionInfo(location);
                    if (!string.IsNullOrWhiteSpace(version.ProductVersion))
                        return version.ProductVersion;
                    if (!string.IsNullOrWhiteSpace(version.FileVersion))
                        return version.FileVersion;
                }
            }
            catch
            {
            }
            return assembly.FullName ?? "unknown";
        }
    }
}
