using SHCDESE.API;
using SHCDESE.API.Components.ModManager;
using SHCDESE.API.Components.Network;

namespace BugfixesAndQoL
{
    public sealed partial class BugfixesAndQoLViewModel
    {
        private bool enablePeaceTimeCivilianProximityReduction = true;
        private volatile bool peaceTimeCivilianMissionLocked;

        public string EnablePeaceTimeCivilianProximityReductionText =>
            SerpLocalization.Get("BugfixesAndQoL.EnablePeaceTimeCivilianProximityReduction");
        public string EnablePeaceTimeCivilianProximityReductionHelpText =>
            SerpLocalization.Get("BugfixesAndQoL.EnablePeaceTimeCivilianProximityReductionHelp");

        [SyncHostOnly]
        public bool EnablePeaceTimeCivilianProximityReduction
        {
            get => enablePeaceTimeCivilianProximityReduction;
            set
            {
                // Authoritative load/host snapshots remain applicable; local next-game edits wait for mission end.
                if (peaceTimeCivilianMissionLocked && !IsApplyingMissionSettingsSnapshot &&
                    !IsApplyingPeaceTimeAuthoritativeUpdate()) return;
                SetSetting(ref enablePeaceTimeCivilianProximityReduction, value,
                    nameof(EnablePeaceTimeCivilianProximityReduction));
            }
        }

        private static bool IsApplyingPeaceTimeAuthoritativeUpdate()
        {
            // The public origin is only the mission-lock exemption. SetSetting still
            // applies the shared/Extender ownership gate before accepting any value.
            LobbyModSettingsChangeOrigin origin = GameXAMLManagerAPI.Instance.CurrentLobbyModSettingsChangeOrigin;
            return origin == LobbyModSettingsChangeOrigin.IncomingNetwork;
        }

        public bool CanConfigurePeaceTimeCivilianProximity =>
            CanEditHostSettings && !peaceTimeCivilianMissionLocked;

        internal void SetPeaceTimeCivilianMissionLocked(bool locked)
        {
            if (peaceTimeCivilianMissionLocked == locked) return;
            peaceTimeCivilianMissionLocked = locked;
            // Mission callbacks can run on the simulation thread. Noesis notifications belong on Unity's thread.
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(RaisePeaceTimeCivilianAuthority);
        }

        private void RaisePeaceTimeCivilianAuthority() =>
            OnPropertyChanged(nameof(CanConfigurePeaceTimeCivilianProximity));
    }
}
