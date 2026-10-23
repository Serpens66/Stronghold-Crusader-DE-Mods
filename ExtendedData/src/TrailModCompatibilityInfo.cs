using System;
using System.Reflection;
namespace ExtendedData
{
    internal sealed class TrailModCompatibilityInfo
    {
        public TrailModCompatibilityInfo(
            string modId,
            string displayName,
            PropertyInfo[] properties,
            string incompatibilityReason, object endpoint = null)
        {
            ModId = modId; Endpoint = endpoint;
            DisplayName = displayName;
            Properties = properties ?? Array.Empty<PropertyInfo>();
            IncompatibilityReason = incompatibilityReason;
        }

        public object Endpoint { get; }
        public string ModId { get; }
        public string DisplayName { get; }
        public PropertyInfo[] Properties { get; }
        public string IncompatibilityReason { get; }
        public bool IsCompatible => string.IsNullOrEmpty(IncompatibilityReason);
    }

}
