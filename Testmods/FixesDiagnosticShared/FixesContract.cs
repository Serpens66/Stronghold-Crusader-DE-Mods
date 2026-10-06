using BepInEx.Bootstrap;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
namespace FixesDiagnostics
{
    internal sealed class FixesContract
    {
        private readonly Assembly assembly;
        private readonly MethodInfo tryGet;
        internal readonly bool ExactVersion;
        internal FixesContract()
        {
            if (!Chainloader.PluginInfos.TryGetValue("fixes", out var info)) throw new InvalidOperationException("Fixes is absent.");
            assembly = info.Instance.GetType().Assembly;
            ExactVersion = info.Metadata.Version.Major == 1 && info.Metadata.Version.Minor == 24 && info.Metadata.Version.Build == 0;
            Type entry = assembly.GetType("Fixes.Config.CustomLordPreferencesEntry", true);
            Type preferences = assembly.GetType("Fixes.Config.Preferences", true);
            tryGet = preferences.GetMethod("TryGet", BindingFlags.NonPublic | BindingFlags.Static, null,
                new[] { typeof(int), entry.MakeByRefType() }, null);
            if (tryGet == null || tryGet.ReturnType != typeof(bool)) throw new InvalidOperationException("Fixes preference contract changed.");
        }
        internal Dictionary<string, object> Preferences(int playerId)
        {
            object[] arguments = { playerId, null };
            if (!(bool)tryGet.Invoke(null, arguments)) return new Dictionary<string, object>();
            return arguments[1].GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.GetIndexParameters().Length == 0 && p.GetGetMethod() != null)
                .ToDictionary(p => p.Name, p => p.GetValue(arguments[1]));
        }
        internal object State(string fieldName, int playerId)
        {
            Type type = assembly.GetType("Fixes.Detours.FixesAIDetours", true);
            FieldInfo field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException("Fixes state field changed: " + fieldName);
            object array = field.GetValue(null);
            if (array == null) return null;
            PropertyInfo indexer = array.GetType().GetProperty("Item", new[] { typeof(int) });
            if (indexer == null || indexer.GetGetMethod() == null) throw new InvalidOperationException("Fixes state indexer changed: " + fieldName);
            return indexer.GetValue(array, new object[] { playerId });
        }
    }
}
