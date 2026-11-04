using APIShared.ModSettings;
using MessagePack;
using SHCDESE.API.Components.Network;
using SHCDESE.API;
using SHCDESE.API.Components.ModManager;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace ExtraFeatures
{
    public sealed partial class ExtraFeaturesViewModel : IModSettingsWorkingCopyEndpoint
    {
        private int killRewardGoldHuman = -1, killRewardGoldAI = -1;
        private int killRewardGoodsHuman = -1, killRewardGoodsAI = -1;
        private int killRewardGoldMaximumHuman = -1, killRewardGoldMaximumAI = -1;
        private bool legacyKillRewardHuman, legacyKillRewardAI;
        private bool killRewardPresetsReady;
        private bool normalizingKillReward;
        private volatile bool killRewardMissionLocked;
        public bool CanConfigureKillReward => CanEditHostSettings && !killRewardMissionLocked;
        public string KillRewardTitle => SerpLocalization.Get("ExtraFeatures.KillReward.Title");
        public string KillRewardGoldTitle => SerpLocalization.Get("ExtraFeatures.KillReward.Gold.Title");
        public string KillRewardGoodsTitle => SerpLocalization.Get("ExtraFeatures.KillReward.Goods.Title");
        public string KillRewardGoldMaximumTitle => SerpLocalization.Get("ExtraFeatures.KillReward.Maximum.Title");
        public string KillRewardGoldHelp => SerpLocalization.Get("ExtraFeatures.KillReward.Gold.Help");
        public string KillRewardGoodsHelp => SerpLocalization.Get("ExtraFeatures.KillReward.Goods.Help");
        public string KillRewardGoldMaximumHelp => SerpLocalization.Get("ExtraFeatures.KillReward.Maximum.Help");

        [SyncHostOnly] public int KillRewardGoldHuman { get => killRewardGoldHuman; set => SetKillReward(ref killRewardGoldHuman, value, nameof(KillRewardGoldHuman)); }
        [SyncHostOnly] public int KillRewardGoldAI { get => killRewardGoldAI; set => SetKillReward(ref killRewardGoldAI, value, nameof(KillRewardGoldAI)); }
        [SyncHostOnly] public int KillRewardGoodsHuman { get => killRewardGoodsHuman; set => SetKillReward(ref killRewardGoodsHuman, value, nameof(KillRewardGoodsHuman)); }
        [SyncHostOnly] public int KillRewardGoodsAI { get => killRewardGoodsAI; set => SetKillReward(ref killRewardGoodsAI, value, nameof(KillRewardGoodsAI)); }
        [SyncHostOnly] public int KillRewardGoldMaximumHuman { get => killRewardGoldMaximumHuman; set => SetKillReward(ref killRewardGoldMaximumHuman, value, nameof(KillRewardGoldMaximumHuman), true); }
        [SyncHostOnly] public int KillRewardGoldMaximumAI { get => killRewardGoldMaximumAI; set => SetKillReward(ref killRewardGoldMaximumAI, value, nameof(KillRewardGoldMaximumAI), true); }

        // Read compatibility only. Loading and canonical exports retire these flags.
        [SyncHostOnly] public bool NoKillRewardHuman { get => legacyKillRewardHuman; set => ReceiveLegacyKillReward(value, false); }
        [SyncHostOnly] public bool NoKillRewardAI { get => legacyKillRewardAI; set => ReceiveLegacyKillReward(value, true); }

        public string KillRewardGoldHumanText => RewardPercentText(KillRewardGoldHuman);
        public string KillRewardGoldAIText => RewardPercentText(KillRewardGoldAI);
        public string KillRewardGoodsHumanText => RewardPercentText(KillRewardGoodsHuman);
        public string KillRewardGoodsAIText => RewardPercentText(KillRewardGoodsAI);
        public string KillRewardGoldMaximumHumanText
        {
            get => KillRewardGoldMaximumHuman.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)) KillRewardGoldMaximumHuman = number; OnPropertyChanged(nameof(KillRewardGoldMaximumHumanText)); }
        }
        public string KillRewardGoldMaximumAIText
        {
            get => KillRewardGoldMaximumAI.ToString(CultureInfo.InvariantCulture);
            set { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)) KillRewardGoldMaximumAI = number; OnPropertyChanged(nameof(KillRewardGoldMaximumAIText)); }
        }
        private static string RewardPercentText(int value) => value < 0 ? "Vanilla" : value.ToString(CultureInfo.InvariantCulture) + " %";
        private static readonly string[] KillRewardProperties = {
            nameof(KillRewardGoldHuman), nameof(KillRewardGoldAI), nameof(KillRewardGoodsHuman), nameof(KillRewardGoodsAI),
            nameof(KillRewardGoldMaximumHuman), nameof(KillRewardGoldMaximumAI), nameof(NoKillRewardHuman), nameof(NoKillRewardAI)
        };

        private void SetKillReward(ref int field, int value, string name, bool maximum = false)
        {
            if (killRewardMissionLocked && !IsApplyingSettingsSnapshot) return;
            if (maximum && value != -1 && value <= 0) return;
            if (field == (maximum ? value : Math.Max(-1, Math.Min(100, value)))) return;
            SetIntSetting(ref field, value, -1, maximum ? int.MaxValue : 100, name, name + "Text");
        }

        private void ReceiveLegacyKillReward(bool value, bool ai)
        {
            string name = ai ? nameof(NoKillRewardAI) : nameof(NoKillRewardHuman);
            if ((killRewardMissionLocked && !IsApplyingSettingsSnapshot) || !CanMutateSetting(name)) return;
            if (ai) legacyKillRewardAI = value; else legacyKillRewardHuman = value;
            if (!IsApplyingSettingsSnapshot)
            {
                ApplyLegacyKillReward();
                RaiseKillRewardProperties();
                SettingChanged?.Invoke(name);
            }
        }

        protected override void OnSettingsSnapshotApplied()
        {
            base.OnSettingsSnapshotApplied();
            ApplyLegacyKillReward();
            RaiseKillRewardProperties();
            SettingChanged?.Invoke(nameof(KillRewardGoldHuman));
        }

        private void ApplyLegacyKillReward()
        {
            if (legacyKillRewardHuman) { killRewardGoldHuman = 0; killRewardGoodsHuman = 0; }
            if (legacyKillRewardAI) { killRewardGoldAI = 0; killRewardGoodsAI = 0; }
            legacyKillRewardHuman = legacyKillRewardAI = false;
        }

        // Cold entry points only: store the complete migrated group once, before any edit.
        // Read the selected serialized working copy, never live remote getters. An editable
        // mission stays transient; a read-only source or client cannot enter this path.
        internal void CompleteKillRewardRegistration()
        {
            killRewardPresetsReady = true;
            NormalizeOwnedKillReward();
        }

        private void NormalizeOwnedKillReward()
        {
            if (!killRewardPresetsReady || normalizingKillReward ||
                !CanEditHostSettings || IsApplyingSettingsSnapshot ||
                GameXAMLManagerAPI.Instance.CurrentLobbyModSettingsChangeOrigin != LobbyModSettingsChangeOrigin.Local) return;
            normalizingKillReward = true;
            try
            {
                ApplyStartupEnemyProximity();
                var owned = IsMissionPresetSelected ? base.System_CreateCurrentWorkingSnapshot() : base.System_CreatePlayerMissionPresetSnapshot();
                if (HasLegacyKillReward(owned)) base.System_ApplyWorkingSnapshot(CanonicalKillRewardSnapshot(owned));
            }
            finally { normalizingKillReward = false; }
        }

        protected override void ApplyConfirmedPresetSelection(PublishedModSettingsPreset preset)
        {
            base.ApplyConfirmedPresetSelection(preset);
            var proximity = EnemyProximityPresetPatch(preset);
            if (proximity != null) base.System_ApplyWorkingSnapshot(proximity);
            NormalizeOwnedKillReward();
        }

        private void RaiseKillRewardProperties()
        {
            foreach (string property in KillRewardProperties) OnPropertyChanged(property);
            RaiseKillRewardText();
        }

        private void RaiseKillRewardText()
        {
            OnPropertyChanged(nameof(KillRewardGoldHumanText)); OnPropertyChanged(nameof(KillRewardGoldAIText));
            OnPropertyChanged(nameof(KillRewardGoodsHumanText)); OnPropertyChanged(nameof(KillRewardGoodsAIText));
            OnPropertyChanged(nameof(KillRewardGoldMaximumHumanText)); OnPropertyChanged(nameof(KillRewardGoldMaximumAIText));
        }

        internal void SetKillRewardMissionLocked(bool locked)
        {
            if (killRewardMissionLocked == locked) return;
            killRewardMissionLocked = locked;
            if (Thread.CurrentThread.ManagedThreadId == uiThread) { OnPropertyChanged(nameof(CanConfigureKillReward)); OnPropertyChanged(nameof(CanConfigureEnemyProximity)); }
            else if (uiContext != null) uiContext.Post(_ => { OnPropertyChanged(nameof(CanConfigureKillReward)); OnPropertyChanged(nameof(CanConfigureEnemyProximity)); }, null);
        }

        private void ResetKillRewardDefaults()
        {
            KillRewardGoldHuman = KillRewardGoldAI = KillRewardGoodsHuman = KillRewardGoodsAI = -1;
            KillRewardGoldMaximumHuman = KillRewardGoldMaximumAI = -1;
        }

        // Reimplement the existing endpoint's detached snapshot producers. The preset controller
        // deliberately retains owned serialized values, rather than trusting live remote getters.
        // Normalize those copies as well, so untouched legacy profiles can be exported safely.
        public new Dictionary<string, byte[]> System_CreateCurrentWorkingSnapshot() => CanonicalKillRewardSnapshot(base.System_CreateCurrentWorkingSnapshot());
        public new Dictionary<string, byte[]> System_CreateCurrentMissionPresetSnapshot() => CanonicalKillRewardSnapshot(base.System_CreateCurrentMissionPresetSnapshot());
        public new Dictionary<string, byte[]> System_CreatePlayerMissionPresetSnapshot() => CanonicalKillRewardSnapshot(base.System_CreatePlayerMissionPresetSnapshot());

        public new void System_ApplyWorkingSnapshot(Dictionary<string, byte[]> snapshot) =>
            base.System_ApplyWorkingSnapshot(CanonicalKillRewardSnapshot(snapshot));
        public new void System_ApplyMissionPresetSnapshot(Dictionary<string, byte[]> snapshot, string label) =>
            base.System_ApplyMissionPresetSnapshot(CanonicalKillRewardSnapshot(snapshot), label);
        public new void System_EnterMissionPreset(Dictionary<string, byte[]> snapshot, string label, bool editable) =>
            base.System_EnterMissionPreset(snapshot == null ? null : CanonicalKillRewardSnapshot(snapshot), label, editable);
        public new void System_ExitMissionPreset()
        {
            base.System_ExitMissionPreset();
            NormalizeOwnedKillReward();
        }

        private static bool LegacyKillReward(Dictionary<string, byte[]> snapshot, string name)
        {
            if (snapshot.TryGetValue(name, out byte[] bytes) && bytes != null)
                try { return MessagePackSerializer.Deserialize<bool>(bytes); }
                catch (MessagePackSerializationException) { }
            return false;
        }

        private static bool HasLegacyKillReward(Dictionary<string, byte[]> snapshot) =>
            HasLegacyEnemyProximity(snapshot) || LegacyKillReward(snapshot, nameof(NoKillRewardHuman)) || LegacyKillReward(snapshot, nameof(NoKillRewardAI));

        private static Dictionary<string, byte[]> CanonicalKillRewardSnapshot(Dictionary<string, byte[]> snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            snapshot = CanonicalEnemyProximitySnapshot(snapshot);
            foreach (string role in new[] { "Human", "AI" })
            {
                string legacy = "NoKillReward" + role;
                bool suppress = LegacyKillReward(snapshot, legacy);
                if (suppress)
                {
                    snapshot["KillRewardGold" + role] = MessagePackSerializer.Serialize(0);
                    snapshot["KillRewardGoods" + role] = MessagePackSerializer.Serialize(0);
                }
                snapshot[legacy] = MessagePackSerializer.Serialize(false);
            }
            return snapshot;
        }
    }
}
