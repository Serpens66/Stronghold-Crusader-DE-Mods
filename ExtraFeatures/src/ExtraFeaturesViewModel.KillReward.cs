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

        // Read compatibility only. Canonical exports and new changes always retire these flags.
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
            if ((killRewardMissionLocked && !IsApplyingSettingsSnapshot) || !CanMutateSetting(name)) return;
            if (maximum && value != -1 && value <= 0) return;
            int normalized = maximum ? value : Math.Max(-1, Math.Min(100, value));
            if (field == normalized) return;
            field = normalized;
            // A snapshot is normalized only after all setters, independently of property order.
            if (!IsApplyingSettingsSnapshot)
            {
                legacyKillRewardHuman = false; legacyKillRewardAI = false;
                if (CommitKillRewardGroup()) return;
            }
            // Capture the complete group, including the other migrated zero and retired aliases.
            // Received host values use the existing origin/authority scope and never reach own storage.
            foreach (string property in KillRewardProperties) OnPropertyChanged(property);
            RaiseKillRewardText();
            SettingChanged?.Invoke(name);
        }

        private void ReceiveLegacyKillReward(bool value, bool ai)
        {
            string name = ai ? nameof(NoKillRewardAI) : nameof(NoKillRewardHuman);
            if ((killRewardMissionLocked && !IsApplyingSettingsSnapshot) || !CanMutateSetting(name)) return;
            if (ai) legacyKillRewardAI = value; else legacyKillRewardHuman = value;
            if (!IsApplyingSettingsSnapshot)
            {
                ApplyLegacyKillReward();
                if (CommitKillRewardGroup()) return;
                RaiseKillRewardProperties();
                SettingChanged?.Invoke(name);
            }
        }

        protected override void OnSettingsSnapshotApplied()
        {
            base.OnSettingsSnapshotApplied();
            killRewardPresetsReady = true;
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

        private bool CommitKillRewardGroup()
        {
            if (!killRewardPresetsReady ||
                GameXAMLManagerAPI.Instance.CurrentLobbyModSettingsChangeOrigin != LobbyModSettingsChangeOrigin.Local)
                return false;
            // One existing controller transaction/atomic file replacement, not eight
            // independent property writes with a crash window for a stale legacy flag.
            var snapshot = new Dictionary<string, byte[]> {
                [nameof(KillRewardGoldHuman)] = MessagePackSerializer.Serialize(killRewardGoldHuman),
                [nameof(KillRewardGoldAI)] = MessagePackSerializer.Serialize(killRewardGoldAI),
                [nameof(KillRewardGoodsHuman)] = MessagePackSerializer.Serialize(killRewardGoodsHuman),
                [nameof(KillRewardGoodsAI)] = MessagePackSerializer.Serialize(killRewardGoodsAI),
                [nameof(KillRewardGoldMaximumHuman)] = MessagePackSerializer.Serialize(killRewardGoldMaximumHuman),
                [nameof(KillRewardGoldMaximumAI)] = MessagePackSerializer.Serialize(killRewardGoldMaximumAI),
                [nameof(NoKillRewardHuman)] = MessagePackSerializer.Serialize(false),
                [nameof(NoKillRewardAI)] = MessagePackSerializer.Serialize(false)
            };
            base.System_ApplyWorkingSnapshot(snapshot);
            return true;
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
            if (Thread.CurrentThread.ManagedThreadId == uiThread) OnPropertyChanged(nameof(CanConfigureKillReward));
            else if (uiContext != null) uiContext.Post(_ => OnPropertyChanged(nameof(CanConfigureKillReward)), null);
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

        private static Dictionary<string, byte[]> CanonicalKillRewardSnapshot(Dictionary<string, byte[]> snapshot)
        {
            foreach (string role in new[] { "Human", "AI" })
            {
                string legacy = "NoKillReward" + role;
                bool suppress = false;
                if (snapshot.TryGetValue(legacy, out byte[] bytes) && bytes != null)
                {
                    // Match the preset controller's fallback for a corrupt retired value.
                    // Keep this detached projection pure; never repair a remote/Trail source.
                    try { suppress = MessagePackSerializer.Deserialize<bool>(bytes); }
                    catch (MessagePackSerializationException) { }
                }
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
