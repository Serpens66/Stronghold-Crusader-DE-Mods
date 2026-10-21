using ExtendedData;

internal static class CoopDraftTests
{
    internal static void Run()
    {
        var state = new CoopSettingsDraftState();
        int amount = 23, applications = 0, freezes = 0, reopens = 0;
        bool editable = false;
        void Apply() { applications++; amount = 17; editable = state.HasDraft; }
        void Freeze() { freezes++; editable = false; }
        void Reopen() { reopens++; editable = true; }
        state.Prepare("package:a:1", false, false, false, Apply, Freeze, Reopen);
        state.Prepare("package:a:1", true, true, false, Apply, Freeze, Reopen);
        Check(amount == 17 && applications == 2, "Customize did not begin from the selected mission.");
        amount = 99;
        foreach (bool ready in new[] { true, false, true, false })
            state.Prepare("package:a:1", true, false, false, Apply, Freeze, Reopen);
        Check(amount == 99 && applications == 2 && freezes == 0, "Ready/unready replaced or froze the draft.");
        state.Prepare("package:a:1", true, false, true, Apply, Freeze, Reopen);
        state.Prepare("package:a:1", true, false, true, Apply, Freeze, Reopen);
        Check(amount == 99 && freezes == 1 && !editable, "Repeated start callbacks changed the materialized draft.");
        state.Recover(Reopen);
        Check(editable && reopens == 1 && amount == 99, "Rejected start lost the draft.");
        amount = 101;
        state.Prepare("package:a:1", true, false, true, Apply, Freeze, Reopen);
        Check(amount == 101 && freezes == 2, "Retry did not freeze the edited draft.");
        state.Prepare("package:a:2", false, false, false, Apply, Freeze, Reopen);
        Check(amount == 17 && !state.HasDraft && applications == 3, "Mission switch retained another mission's draft.");
        state.Prepare("package:b:2", false, false, false, Apply, Freeze, Reopen);
        Check(applications == 4, "Package identity did not distinguish the source.");
        state.Reset();
        state.Prepare("package:b:2", true, true, false, Apply, Freeze, Reopen);
        bool failed = false;
        try { state.Prepare("package:b:2", true, false, true, Apply, () => throw new InvalidOperationException("freeze failed"), Reopen); }
        catch (InvalidOperationException) { failed = true; }
        Check(failed, "Freeze failure was swallowed.");
        state.Prepare("package:b:2", true, false, true, Apply, Freeze, Reopen);
        Check(freezes == 3, "Failed freeze made a retry a no-op.");
    }

    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); }
}
