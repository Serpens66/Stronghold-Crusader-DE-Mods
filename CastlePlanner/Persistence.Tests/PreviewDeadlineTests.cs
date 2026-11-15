using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;

internal static class PreviewDeadlineTests
{
    internal static void Run(Assembly assembly, Action<bool, string> check)
    {
        Type runtimeType = assembly.GetType("CastlePlanner.FreeCastlePreviewRuntime", true);
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        object runtime = FormatterServices.GetUninitializedObject(runtimeType);
        FieldInfo state = runtimeType.GetField("state", fields);
        object distributing = Enum.Parse(state.FieldType, "Distributing");
        state.SetValue(runtime, distributing);

        // Call the production finalizer after distribution started. Its guard must
        // return before reaching Steam/Unity, preserve the frozen manifest, and
        // retain acknowledgements that arrive in different rendered frames.
        foreach (string name in new[] { "roster", "decisions", "noneDecisions", "committedSelections" })
        {
            FieldInfo field = runtimeType.GetField(name, fields);
            field.SetValue(runtime, Activator.CreateInstance(field.FieldType));
        }
        FieldInfo manifest = runtimeType.GetField("committedSelections", fields);
        object originalManifest = manifest.GetValue(runtime);
        Type selectionType = assembly.GetType("CastlePlanner.FreeCastleSelection", true);
        Type modeType = assembly.GetType("CastlePlanner.FreeCastleSelectionMode", true);
        var roster = (HashSet<int>)runtimeType.GetField("roster", fields).GetValue(runtime);
        var decisions = (IDictionary)runtimeType.GetField("decisions", fields).GetValue(runtime);
        for (int player = 1; player <= 3; player++)
        {
            object selection = Activator.CreateInstance(selectionType);
            selectionType.GetField("Mode").SetValue(selection, Enum.Parse(modeType, "RotateKeepOnly"));
            selectionType.GetField("PlayerId").SetValue(selection, player);
            selectionType.GetField("Rotation").SetValue(selection, 2);
            roster.Add(player);
            decisions.Add(player, selection);
            ((IList)originalManifest).Add(selection);
        }
        var acknowledgements = new HashSet<ulong> { 1001 };
        runtimeType.GetField("manifestAcks", fields).SetValue(runtime, acknowledgements);
        MethodInfo finalize = runtimeType.GetMethod("TryFinalizeAsHost", fields);

        foreach (ulong arrivingPeer in new ulong[] { 1002, 1003 })
        {
            acknowledgements.Add(arrivingPeer);
            Exception reentryError = null;
            try { finalize.Invoke(runtime, null); }
            catch (TargetInvocationException error)
            {
                reentryError = error.InnerException ?? error;
            }
            check(Equals(state.GetValue(runtime), distributing),
                "Repeated selection finalization must not replace the distribution state.");
            check(ReferenceEquals(manifest.GetValue(runtime), originalManifest),
                "Repeated selection finalization must retain the committed manifest.");
            check(acknowledgements.Contains(1001) && acknowledgements.Contains(arrivingPeer),
                "Repeated selection finalization must retain acknowledgements already received.");
            check(reentryError == null,
                "Completed-selection finalization must return before revisiting Steam/Unity or settings dependencies.");
        }
        check(acknowledgements.SetEquals(new ulong[] { 1001, 1002, 1003 }),
            "Acknowledgements from separate finalization attempts must accumulate until commit.");
    }
}
