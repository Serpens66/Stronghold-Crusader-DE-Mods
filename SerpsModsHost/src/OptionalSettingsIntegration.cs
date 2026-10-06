using System;

namespace SerpsModsHost
{
    internal enum OptionalSettingsState { Absent, Incompatible, Waiting, Active, Failed }

    // Used by the real adapter and isolated tests; UI failure never changes provider admission.
    internal sealed class OptionalSettingsIntegration<T> where T : class
    {
        internal OptionalSettingsState State { get; private set; }
        internal T Participant { get; private set; }
        internal bool ViewAttached { get; private set; }
        internal bool ViewFailed { get; private set; }
        internal void Discover(bool installed, Type api, Func<T> prepareAndActivate, Action<Exception> report)
        {
            if (State == OptionalSettingsState.Active || State == OptionalSettingsState.Failed || State == OptionalSettingsState.Incompatible) return;
            if (!installed) { State = OptionalSettingsState.Absent; return; }
            if (api == null) { State = OptionalSettingsState.Incompatible; return; }
            try
            {
                var candidate = prepareAndActivate();
                if (candidate == null) { State = OptionalSettingsState.Waiting; return; }
                Participant = candidate;
                State = OptionalSettingsState.Active;
            }
            catch (Exception ex) { State = OptionalSettingsState.Failed; report(ex); }
        }
        internal void Attach(Func<T, bool> attach, Action<Exception> report)
        {
            if (State != OptionalSettingsState.Active || ViewAttached || ViewFailed) return;
            try { ViewAttached = attach(Participant); }
            catch (Exception ex) { ViewFailed = true; report(ex); }
        }
    }
}
