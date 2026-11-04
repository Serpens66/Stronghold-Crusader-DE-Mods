using APIShared.Health;
using APIShared.Modifiers;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.Components.SaveData;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.Threading;

namespace ExtraFeatures
{
    // APIShared owns HP storage, initialization, safety limits and save persistence.
    // This adapter publishes only an immutable, mission-scoped settings contribution.
    internal sealed unsafe class HealthModifierRuntime
    {
        internal const string SaveDataIdentifier = "ExtraFeatures.LordHealth.v1";
        private readonly ManualLogSource log;
        private readonly ExtraFeaturesViewModel settings;
        private readonly object archiveSync = new object();
        private byte[] archivedState;
        private object archivedStateSource;
        private LordHealthBasis[] legacyBases;
        private HealthModifierHandle handle;
        private bool archiveRegistered, legacyRegistered;
        private long sessionId;
        private HealthSettingsSnapshot snapshot;

        internal HealthModifierRuntime(ManualLogSource log, ExtraFeaturesViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        internal void Initialize()
        {
            if (handle == null)
            {
                if (!HealthModifiers.TryRegister(ExtraFeaturesPlugin.PluginGuid, "health-multipliers", GetContribution,
                    out handle, out string reason))
                    throw new InvalidOperationException("Shared health modifiers unavailable: " + reason);
                handle.SetEnabled(false);
            }
            if (!archiveRegistered)
            {
                if (!ModSaveDataAPI.Instance.RegisterModDataHandler(SaveDataIdentifier, _ => null, BufferArchivedState, ClearArchivedState))
                    throw new InvalidOperationException("Legacy Lord health reader registration failed.");
                archiveRegistered = true;
            }
            if (!legacyRegistered)
            {
                if (!HealthModifiers.TryRegisterLegacyBasisProvider(ExtraFeaturesPlugin.PluginGuid, "lord-health-v1",
                    TakeLegacyBases, out string reason))
                    throw new InvalidOperationException("Legacy Lord health import unavailable: " + reason);
                legacyRegistered = true;
            }
        }

        internal void BeginMap(long newSessionId, bool isLoadedSave)
        {
            if (sessionId == newSessionId) return;
            ResetMapState();
            sessionId = newSessionId;
            byte[] legacyBytes = TakeArchivedState();
            if (!settings.EnableMod || !Shared.GameplayModActivationGate.IsFeatureAllowed(
                ExtraFeaturesPlugin.PluginGuid, Shared.GameplayFeatureId.LordHealthMultipliers)) return;
            Initialize();
            if (isLoadedSave && legacyBytes != null)
            {
                try { legacyBases = LordHealthSaveState.Decode(legacyBytes).Records; }
                catch (Exception error)
                {
                    Shared.DebugLogHelper.LogErrorLimited(log, "ExtraFeatures/health/legacy-import",
                        "Invalid legacy Lord health basis; no legacy values imported: " + error);
                }
            }
            Volatile.Write(ref snapshot, new HealthSettingsSnapshot(settings));
            // Remain active at 100%: the shared service must still record an unscaled basis.
            handle.SetEnabled(true);
        }

        internal void ResetMapState()
        {
            handle?.SetEnabled(false);
            Volatile.Write(ref snapshot, null);
            legacyBases = null;
            sessionId = 0;
        }

        private ValueModifierContribution GetContribution(HealthModifierContext context)
        {
            HealthSettingsSnapshot current = Volatile.Read(ref snapshot);
            if (current == null || context.PlayerId < 1 || context.PlayerId > GamePlayerManagerAPI.MAX_PLAYERS)
                return ValueModifierContribution.None;
            HealthCategory category;
            if (context.Kind == HealthObjectKind.Unit) category = HealthMultiplierPolicy.ClassifyUnit(context.UnitType);
            else if (context.Kind == HealthObjectKind.Building) category = HealthMultiplierPolicy.ClassifyBuilding(context.BuildingType);
            else return ValueModifierContribution.None;
            if (category == HealthCategory.None) return ValueModifierContribution.None;
            int percent = current.GetPercent(category, context.IsAIPlayer);
            // Factor(1) requests restoration of a stored basis after native load normalization.
            // None is reserved for excluded categories, whose native values remain untouched.
            return ValueModifierContribution.Factor(percent / 100m);
        }

        private HealthLegacyBasis[] TakeLegacyBases()
        {
            LordHealthBasis[] records = legacyBases;
            legacyBases = null;
            if (Volatile.Read(ref snapshot) == null || records == null) return null;
            var result = new List<HealthLegacyBasis>(records.Length);
            GamePlayerManagerAPI players = GamePlayerManagerAPI.Instance;
            foreach (LordHealthBasis basis in records)
            {
                int unitId = players.GetLordUnitId(basis.PlayerId);
                if (unitId <= 0 || players.GetLordUnitGlobalId(basis.PlayerId) != (int)basis.GlobalId ||
                    !APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) || unit == null ||
                    unit->r_GlobalId != basis.GlobalId || unit->r_UnitChimp != eChimps.CHIMP_TYPE_LORD ||
                    unit->r_ControllableForPlayerId != basis.PlayerId) continue;
                result.Add(new HealthLegacyBasis(unitId, basis.GlobalId, basis.VanillaMaximum));
            }
            return result.ToArray();
        }

        private void BufferArchivedState(byte[] bytes, LoadContext context)
        {
            object archive = GameMapArchiveManagerAPI.Instance.GetMapArchive();
            lock (archiveSync) { archivedState = bytes; archivedStateSource = archive; }
        }

        private void ClearArchivedState()
        {
            lock (archiveSync) { archivedState = null; archivedStateSource = null; }
        }

        private byte[] TakeArchivedState()
        {
            object archive = GameMapArchiveManagerAPI.Instance.GetMapArchive();
            lock (archiveSync)
            {
                byte[] bytes = archive != null && ReferenceEquals(archive, archivedStateSource) ? archivedState : null;
                archivedState = null; archivedStateSource = null;
                return bytes;
            }
        }
    }
}
