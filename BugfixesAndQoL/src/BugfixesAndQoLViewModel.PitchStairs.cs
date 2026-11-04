using SHCDESE.API;
using SHCDESE.API.Components.ModManager;
using SHCDESE.API.Components.Network;

namespace BugfixesAndQoL
{
    public sealed partial class BugfixesAndQoLViewModel
    {
        private bool enablePitchStairsFix = true;
        private volatile bool pitchStairsMissionLocked;

        public string EnablePitchStairsFixText => SerpLocalization.Get("BugfixesAndQoL.EnablePitchStairsFix");
        public string EnablePitchStairsFixHelpText => SerpLocalization.Get("BugfixesAndQoL.EnablePitchStairsFixHelp");

        [SyncHostOnly]
        public bool EnablePitchStairsFix
        {
            get => enablePitchStairsFix;
            set
            {
                // Save/Trail restoration and authenticated host updates still reach the model.
                // The native rule is independently frozen before this map starts loading.
                if (pitchStairsMissionLocked && !IsApplyingMissionSettingsSnapshot &&
                    GameXAMLManagerAPI.Instance.CurrentLobbyModSettingsChangeOrigin != LobbyModSettingsChangeOrigin.IncomingNetwork)
                    return;
                SetSetting(ref enablePitchStairsFix, value, nameof(EnablePitchStairsFix));
            }
        }

        public bool CanConfigurePitchStairs => CanEditHostSettings && !pitchStairsMissionLocked;

        internal void SetPitchStairsMissionLocked(bool locked)
        {
            if (pitchStairsMissionLocked == locked) return;
            pitchStairsMissionLocked = locked;
            // Mission notifications may arrive on the simulation thread.
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(RaisePitchStairsAuthority);
        }

        private void RaisePitchStairsAuthority() => OnPropertyChanged(nameof(CanConfigurePitchStairs));
    }
}
