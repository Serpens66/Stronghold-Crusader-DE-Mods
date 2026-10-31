using APIShared;
using APIShared.GameModes;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace AIBuildDiagnoseTest.Tests
{
    internal static class RuntimeFixture
    {
        // No native subscriptions or singleton initialization: initialize the production
        // state containers and drive the actual lifecycle/record methods directly.
        internal static T Create<T>() where T : class
        {
            var result = (T)FormatterServices.GetUninitializedObject(typeof(T));
            foreach (var field in typeof(T).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (field.FieldType.IsArray) field.SetValue(result, Array.CreateInstance(field.FieldType.GetElementType(), 9));
                else if (field.FieldType == typeof(object)) field.SetValue(result, new object());
                else if (field.FieldType.Namespace == "System.Collections.Generic")
                    field.SetValue(result, Activator.CreateInstance(field.FieldType));
            }
            return result;
        }
        internal static object Get(object owner, string name) =>
            owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        internal static void Set(object owner, string name, object value) =>
            owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
        internal static MissionLifecycleNotification Notification(long id, MissionInitializationPhase phase = MissionInitializationPhase.ManagedReady)
        {
            var constructor = typeof(MissionContext).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .First(c => c.GetParameters().Length >= 6 && c.GetParameters()[0].ParameterType == typeof(long));
            var arguments = constructor.GetParameters().Select(p => p.HasDefaultValue ? p.DefaultValue :
                p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null).ToArray();
            arguments[0] = id; arguments[1] = MissionStartKind.LoadedSave;
            arguments[2] = default(GameModeSnapshot); arguments[3] = "arbitrary-new-case.sav";
            var context = (MissionContext)constructor.Invoke(arguments);
            return (MissionLifecycleNotification)Activator.CreateInstance(typeof(MissionLifecycleNotification),
                BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { context, MissionLifecycleKind.Initialization, phase, MissionEndReason.None, false, false }, null);
        }
    }
}
