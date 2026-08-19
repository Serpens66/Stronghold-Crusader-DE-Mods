using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CustomCustomTrail.Core
{
    public sealed class TrailModCompatibilityResult
    {
        public TrailModCompatibilityResult(PropertyInfo[] properties, string incompatibilityReason)
        {
            Properties = properties ?? Array.Empty<PropertyInfo>();
            IncompatibilityReason = incompatibilityReason;
        }

        public PropertyInfo[] Properties { get; }
        public string IncompatibilityReason { get; }
        public bool IsCompatible => string.IsNullOrEmpty(IncompatibilityReason);
    }

    public static class TrailModCompatibilityContract
    {
        public static TrailModCompatibilityResult Evaluate(
            object viewModel,
            Action<PropertyInfo, object> serializationProbe,
            Func<Type, byte[], object> deserializationProbe)
        {
            if (viewModel == null)
                return Incompatible("missing ViewModel");

            Type type = viewModel.GetType();
            PropertyInfo[] properties = GetTrailProperties(type);
            if (properties.Length == 0)
                return Incompatible("no persistent SyncHostOnly settings");
            IGrouping<string, PropertyInfo> duplicateProperty = properties
                .GroupBy(property => property.Name, StringComparer.Ordinal)
                .FirstOrDefault(group => group.Skip(1).Any());
            if (duplicateProperty != null)
                return Incompatible("duplicate SyncHostOnly property " + duplicateProperty.Key);

            PropertyInfo enableMod = properties.FirstOrDefault(property => property.Name == "EnableMod");
            if (enableMod != null && enableMod.PropertyType != typeof(bool))
                return Incompatible("EnableMod must be Boolean");

            MethodInfo createSnapshot = GetPublicInstanceMethod(
                type,
                "System_CreateDisabledMissionPresetSnapshot",
                Type.EmptyTypes);
            if (createSnapshot == null || createSnapshot.ReturnType != typeof(Dictionary<string, byte[]>))
            {
                return Incompatible("missing mission snapshot creation");
            }
            MethodInfo enterPreset = GetPublicInstanceMethod(
                type,
                "System_EnterMissionPreset",
                new[] { typeof(Dictionary<string, byte[]>), typeof(string), typeof(bool) });
            if (enterPreset == null || enterPreset.ReturnType != typeof(void))
            {
                return Incompatible("missing mission preset entry");
            }
            MethodInfo exitPreset = GetPublicInstanceMethod(type, "System_ExitMissionPreset", Type.EmptyTypes);
            if (exitPreset == null || exitPreset.ReturnType != typeof(void))
                return Incompatible("missing mission preset exit");

            PropertyInfo active = type.GetProperty("IsMissionPresetActive", BindingFlags.Instance | BindingFlags.Public);
            if (active == null || !active.CanRead || active.PropertyType != typeof(bool))
                return Incompatible("missing mission preset state");

            foreach (PropertyInfo property in properties)
            {
                object value;
                try
                {
                    value = property.GetValue(viewModel);
                }
                catch (Exception exception)
                {
                    return Incompatible("could not read " + property.Name + ": " + Unwrap(exception).Message);
                }

                if (value == null)
                    return Incompatible(property.Name + " is null");

                try
                {
                    serializationProbe?.Invoke(property, value);
                }
                catch (Exception exception)
                {
                    return Incompatible(property.Name + " is not serializable: " + Unwrap(exception).Message);
                }
            }

            Dictionary<string, byte[]> snapshot;
            try
            {
                snapshot = (Dictionary<string, byte[]>)createSnapshot.Invoke(viewModel, null);
            }
            catch (Exception exception)
            {
                return Incompatible("could not create mission snapshot: " + Unwrap(exception).Message);
            }
            if (snapshot == null)
                return Incompatible("mission snapshot is null");

            foreach (PropertyInfo property in properties)
            {
                if (!snapshot.TryGetValue(property.Name, out byte[] bytes) || bytes == null)
                    return Incompatible("mission snapshot is missing " + property.Name);
                try
                {
                    object snapshotValue = deserializationProbe?.Invoke(property.PropertyType, bytes);
                    if (property == enableMod && snapshotValue is bool enabled && enabled)
                        return Incompatible("mission snapshot does not disable EnableMod");
                }
                catch (Exception exception)
                {
                    return Incompatible("mission snapshot contains invalid " + property.Name + ": " + Unwrap(exception).Message);
                }
            }

            return new TrailModCompatibilityResult(properties, null);
        }

        public static PropertyInfo[] GetTrailProperties(Type viewModelType)
        {
            if (viewModelType == null)
                return Array.Empty<PropertyInfo>();

            return viewModelType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.CanRead && property.CanWrite &&
                    HasAttribute(property, "SyncHostOnlyAttribute") &&
                    !HasAttribute(property, "DoNotPersistAttribute"))
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .ToArray();
        }

        public static string[] NormalizeDisabledModIds(IEnumerable<string> ids, string excludedModId)
        {
            return (ids ?? Enumerable.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id) &&
                    !string.Equals(id, excludedModId, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
        }

        private static bool HasAttribute(PropertyInfo property, string attributeTypeName) =>
            property.GetCustomAttributes(false)
                .Any(attribute => attribute.GetType().Name == attributeTypeName);

        private static MethodInfo GetPublicInstanceMethod(Type type, string name, Type[] parameterTypes) =>
            type.GetMethod(
                name,
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                types: parameterTypes,
                modifiers: null);

        private static TrailModCompatibilityResult Incompatible(string reason) =>
            new TrailModCompatibilityResult(Array.Empty<PropertyInfo>(), reason);

        private static Exception Unwrap(Exception exception) =>
            exception is TargetInvocationException invocation && invocation.InnerException != null
                ? invocation.InnerException
                : exception;
    }
}
