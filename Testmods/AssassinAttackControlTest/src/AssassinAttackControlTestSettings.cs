using SHCDESE.API.Components.Network;
using SHCDESE.ViewModels;
namespace AssassinAttackControlTest
{
    public sealed class AssassinAttackControlTestSettings : LobbyModSettingsBaseViewModel
    {
        private bool enabled=true, protect=true, alternatives=true;
        [SyncHostOnly] public bool EnableMod { get=>enabled; set=>SetSynced(ref enabled,value); }
        [SyncHostOnly] public bool ProtectCapturedGates { get=>protect; set=>SetSynced(ref protect,value); }
        [SyncHostOnly] public bool PreferCastleAccess { get=>alternatives; set=>SetSynced(ref alternatives,value); }
    }
}
