using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CrusaderDE;

namespace UiAcceptance
{
    internal static partial class GameSteps
    {
        // The runner starts with the user's personal mod settings (for example CastlePlanner's castle spawn). This
        // presses every loaded mod's own "reset to default" command (ResetToDefaultCommand on its settings object,
        // found on the plugin instance or behind a static/instance "Settings" member), so a scenario starts from the
        // mods' defaults and then sets what it needs. Front end only; the run script restores the personal files.
        private static IEnumerable<Wait> ModSettingsDefaults(Step step)
        {
            Runner.Require(FatControler.currentScene == Enums.SceneIDS.FrontEnd, "settings change only outside missions");
            var skip = new HashSet<string>(step.Args ?? new string[0]);
            var reset = new List<string>();
            var without = new List<string>();
            foreach (var info in BepInEx.Bootstrap.Chainloader.PluginInfos.Values.OrderBy(i => i.Metadata.GUID, StringComparer.Ordinal))
            {
                // A destroyed plugin component compares equal to null in Unity but its settings object lives on.
                if (ReferenceEquals(info.Instance, null) || skip.Contains(info.Metadata.GUID)) continue;
                object settings = FindResettable(info.Instance);
                if (settings == null) { without.Add(info.Metadata.GUID); continue; }
                object command = settings.GetType().GetProperty("ResetToDefaultCommand").GetValue(settings, null);
                MethodInfo execute = command?.GetType().GetMethod("Execute", new[] { typeof(object) });
                Runner.Require(execute != null, info.Metadata.GUID + " has no executable ResetToDefaultCommand");
                execute.Invoke(command, new object[] { null });
                reset.Add(info.Metadata.GUID);
            }
            AcceptLog.Info("SETTINGS_DEFAULTS reset=[" + string.Join(",", reset) + "] without=[" + string.Join(",", without) + "]");
            Runner.Require(reset.Count > 0, "no mod settings were reset");
            yield break;
        }

        private const BindingFlags AnyMember = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private static object FindResettable(object plugin)
        {
            if (HasReset(plugin)) return plugin;
            foreach (object value in Members(plugin.GetType(), plugin))
            {
                if (HasReset(value)) return value;
                if (value == null) continue;
                foreach (object inner in Members(value.GetType(), value).Where(v => v != null))
                    if (HasReset(inner)) return inner;
            }
            return null;
        }

        // Values of members named Settings or processRoot (the conventions of the workspace mods).
        private static IEnumerable<object> Members(Type type, object instance)
        {
            foreach (string name in new[] { "Settings", "processRoot" })
            {
                PropertyInfo property = type.GetProperty(name, AnyMember);
                if (property != null && property.GetIndexParameters().Length == 0)
                    yield return property.GetGetMethod(true).IsStatic ? property.GetValue(null, null) : property.GetValue(instance, null);
                FieldInfo field = type.GetField(name, AnyMember);
                if (field != null) yield return field.IsStatic ? field.GetValue(null) : field.GetValue(instance);
            }
        }

        private static bool HasReset(object value) => value != null && value.GetType().GetProperty("ResetToDefaultCommand") != null;
    }
}
