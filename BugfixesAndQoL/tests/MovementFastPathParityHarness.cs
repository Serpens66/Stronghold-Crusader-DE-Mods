using System;
using System.Collections.Generic;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace BugfixesAndQoL
{
    /// <summary>
    /// Executable reference contract for the removed managed callbacks. The
    /// reference side mirrors their decisions; the table side mirrors the
    /// native fixed-table kernel emitted by SynchronizedMovementCadencePatch.
    /// </summary>
    internal static class MovementFastPathParityHarness
    {
        private const ushort InitializationState = 109;
        private const ushort AliveStateValue = (ushort)AliveState.IsAlive;
        private const ushort TransformationState = 4;
        private const ushort DeadState = 3;
        private static readonly ushort SpearmanType =
            (ushort)eChimps.CHIMP_TYPE_SPEARMAN;

        private static readonly eChimps[] SupportedRallyTypes =
        {
            eChimps.CHIMP_TYPE_ENGINEER,
            eChimps.CHIMP_TYPE_TUNNELER,
            eChimps.CHIMP_TYPE_LADDERMAN,
            eChimps.CHIMP_TYPE_MONK,
            eChimps.CHIMP_TYPE_ARCHER,
            eChimps.CHIMP_TYPE_XBOWMAN,
            eChimps.CHIMP_TYPE_SPEARMAN,
            eChimps.CHIMP_TYPE_PIKEMAN,
            eChimps.CHIMP_TYPE_MACEMAN,
            eChimps.CHIMP_TYPE_SWORDSMAN,
            eChimps.CHIMP_TYPE_KNIGHT,
            eChimps.CHIMP_TYPE_ARAB_BOW,
            eChimps.CHIMP_TYPE_ARAB_SLAVE,
            eChimps.CHIMP_TYPE_ARAB_SLINGER,
            eChimps.CHIMP_TYPE_ARAB_ASSASIN,
            eChimps.CHIMP_TYPE_ARAB_HORSEMAN,
            eChimps.CHIMP_TYPE_ARAB_SWORDSMAN,
            eChimps.CHIMP_TYPE_ARAB_GRENADIER,
            eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER,
            eChimps.CHIMP_TYPE_BEDOUIN_HEALER,
            eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH,
            eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER,
            eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER,
            eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL,
            eChimps.CHIMP_TYPE_BEDOUIN_SAPPER,
            eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER
        };

        internal static List<string> Run()
        {
            var failures = new List<string>();
            var profiles = CreateProfiles();

            RunGroupMatrix(failures);
            RunCadenceMatrix(failures, profiles);
            RunRallyMatrix(failures, profiles);
            RunRallyTransformationSequence(failures, profiles);
            RunSpeedMatrix(failures);
            RunActivationFlagMatrix(failures, profiles);
            return failures;
        }

        private static void RunGroupMatrix(List<string> failures)
        {
            GroupMember[][] cases =
            {
                new[] { Member(1, 8, true, 2), Member(1, 8, true, 2) },
                new[] { Member(1, 8, true, 2), Member(2, 12, true, 3) },
                new[] { Member(1, 12, true, 3), Member(2, 12, true, 1) },
                new[] { Member(1, 12, true, 3), Member(2, 9, false, 0) },
                new[] { Member(1, 0, true, 0), Member(2, 0, true, 0) },
                new[] { Member(1, 8, true, 2), Member(2, 12, true, 3, false) }
            };

            for (int index = 0; index < cases.Length; index++)
            {
                GroupResult reference = AnalyzeGroupReference(cases[index]);
                GroupResult table = AnalyzeGroupSinglePass(cases[index]);
                Compare(reference, table, failures, "group-" + index);
            }
        }

        private static void RunCadenceMatrix(
            List<string> failures,
            Dictionary<ushort, Profile> profiles)
        {
            foreach (KeyValuePair<ushort, Profile> pair in profiles)
            {
                uint[] states = { 1, 0x81, 0xDEAD };
                foreach (uint state in states)
                {
                    foreach (bool running in new[] { false, true })
                    {
                        Unit referenceUnit = UnitFor(pair.Key, state);
                        Unit tableUnit = referenceUnit.Clone();
                        ApplySynchronizationReference(
                            referenceUnit,
                            profiles,
                            running,
                            7);
                        ApplySynchronizationTable(
                            tableUnit,
                            profiles,
                            running,
                            7);
                        Compare(referenceUnit, tableUnit, failures,
                            $"cadence-type-{pair.Key}-state-{state}-running-{running}");
                    }
                }
            }

            Unit unsupportedReference = UnitFor(250, 0xDEAD);
            Unit unsupportedTable = unsupportedReference.Clone();
            ApplySynchronizationReference(unsupportedReference, profiles, true, 9);
            ApplySynchronizationTable(unsupportedTable, profiles, true, 9);
            Compare(unsupportedReference, unsupportedTable, failures,
                "cadence-unsupported-profile");

            Unit deadReference = UnitFor(1, 1);
            deadReference.AliveState = DeadState;
            Unit deadTable = deadReference.Clone();
            ApplySynchronizationReference(deadReference, profiles, true, 9);
            ApplySynchronizationTable(deadTable, profiles, true, 9);
            Compare(deadReference, deadTable, failures, "cadence-dead-unit");
        }

        private static void RunRallyMatrix(
            List<string> failures,
            Dictionary<ushort, Profile> profiles)
        {
            var cases = new List<RallyCase>
            {
                Rally("active", UnitFor(1, 1), TrackingFor(1), true),
                Rally("arab-bow-vanilla-running-pair",
                    UnitFor((ushort)eChimps.CHIMP_TYPE_ARAB_BOW, 1),
                    TrackingFor((ushort)eChimps.CHIMP_TYPE_ARAB_BOW), true),
                Rally("no-path", UnitFor(1, 1, false), TrackingFor(1, moving: true), true),
                Rally("initializing", UnitFor(99, 1, false, InitializationState, 0), TrackingFor(1), true),
                Rally("transforming", UnitFor(99, 1, false, 0, 1), TrackingFor(1), true),
                Rally("wrong-type", UnitFor(99, 1), TrackingFor(1), true),
                Rally("wrong-owner", UnitFor(1, 1, owner: 2), TrackingFor(1), true),
                Rally("dead-unit", Dead(UnitFor(1, 1)), TrackingFor(1), true),
                Rally("reused-slot", UnitFor(1, 1, globalId: 88), TrackingFor(1), true),
                Rally("capture-generation", UnitFor(1, 1), TrackingFor(1, globalId: 0), true),
                Rally("interrupted-same-target", UnitFor(1, 1), TrackingFor(1, observed: true, moving: false), true),
                Rally("interrupted-new-target", UnitFor(1, 1, targetX: 44), TrackingFor(1, observed: true, moving: false), true),
                Rally("deleted-or-map-cleared", UnitFor(1, 1), TrackingFor(1, active: false), true),
                Rally("unsupported", UnitFor(250, 7), TrackingFor(250), true),
                Rally("spearman-upgrade-off", UnitFor(SpearmanType, 1), TrackingFor(SpearmanType), false),
                Rally("spearman-upgrade-on", UnitFor(SpearmanType, 1), TrackingFor(SpearmanType), true)
            };

            foreach (RallyCase item in cases)
            {
                Unit referenceUnit = item.Unit.Clone();
                Unit tableUnit = item.Unit.Clone();
                Tracking referenceTracking = item.Tracking.Clone();
                Tracking tableTracking = item.Tracking.Clone();
                bool referenceHandled = ApplyRallyReference(
                    referenceUnit, referenceTracking, profiles, item.ImprovedSpearmen);
                bool tableHandled = ApplyRallyTable(
                    tableUnit, tableTracking, profiles, item.ImprovedSpearmen);
                if (!referenceHandled)
                    ApplySynchronizationReference(referenceUnit, profiles, true, 99);
                if (!tableHandled)
                    ApplySynchronizationTable(tableUnit, profiles, true, 99);
                if (referenceHandled != tableHandled)
                    failures.Add(item.Name + ": handled result differs");
                Compare(referenceUnit, tableUnit, failures, item.Name + "-unit");
                Compare(referenceTracking, tableTracking, failures, item.Name + "-tracking");
                if (referenceHandled && referenceUnit.SpeedBonus == 99)
                    failures.Add(item.Name + ": synchronization overrode rally priority");
                if (item.Name == "arab-bow-vanilla-running-pair" &&
                    (tableUnit.Animation != 0x81 || tableUnit.SpeedBonus != 1))
                {
                    failures.Add(item.Name +
                        ": expected Animation 0x81 and SpeedBonus 1");
                }
            }
        }

        private static void RunSpeedMatrix(List<string> failures)
        {
            foreach (bool active in new[] { false, true })
            foreach (bool alive in new[] { false, true })
            foreach (bool path in new[] { false, true })
            {
                Unit reference = UnitFor(1, 1, path);
                reference.AliveState = alive ? AliveStateValue : DeadState;
                reference.CurrentSpeed = 17;
                reference.CurrentSpeed2 = 31;
                Unit table = reference.Clone();
                Tracking referenceTracking = TrackingFor(1);
                Tracking tableTracking = referenceTracking.Clone();
                referenceTracking.Active = active;
                tableTracking.Active = active;
                ApplySpeedReference(reference, referenceTracking);
                ApplySpeedTable(table, tableTracking);
                Compare(reference, table, failures,
                    $"speed-active-{active}-alive-{alive}-path-{path}");
            }
        }

        private static void RunRallyTransformationSequence(
            List<string> failures,
            Dictionary<ushort, Profile> profiles)
        {
            ushort knightType = (ushort)eChimps.CHIMP_TYPE_KNIGHT;
            Unit referenceUnit = UnitFor(
                1,
                1,
                path: false,
                aiState: InitializationState,
                transformType: knightType);
            Unit tableUnit = referenceUnit.Clone();
            Tracking referenceTracking = TrackingFor(knightType);
            Tracking tableTracking = referenceTracking.Clone();

            for (int tick = 0; tick < 32; tick++)
            {
                ApplyCombinedReference(referenceUnit, referenceTracking,
                    profiles, rallyEnabled: true, synchronizationEnabled: true);
                ApplyCombinedTable(tableUnit, tableTracking,
                    profiles, rallyEnabled: true, synchronizationEnabled: true);
            }

            referenceUnit.AliveState = TransformationState;
            tableUnit.AliveState = TransformationState;
            referenceUnit.AiState = 0;
            tableUnit.AiState = 0;
            ApplyCombinedReference(referenceUnit, referenceTracking,
                profiles, rallyEnabled: true, synchronizationEnabled: true);
            ApplyCombinedTable(tableUnit, tableTracking,
                profiles, rallyEnabled: true, synchronizationEnabled: true);
            Compare(referenceUnit, tableUnit, failures,
                "rally-transform-state-4-unit");
            Compare(referenceTracking, tableTracking, failures,
                "rally-transform-state-4-tracking");
            if (!referenceTracking.Active || !tableTracking.Active)
                failures.Add("rally-transform-state-4 cleared tracking");

            referenceUnit.AliveState = AliveStateValue;
            tableUnit.AliveState = AliveStateValue;
            referenceUnit.Type = knightType;
            tableUnit.Type = knightType;
            referenceUnit.HasPath = true;
            tableUnit.HasPath = true;
            ApplyCombinedReference(referenceUnit, referenceTracking,
                profiles, rallyEnabled: true, synchronizationEnabled: true);
            ApplyCombinedTable(tableUnit, tableTracking,
                profiles, rallyEnabled: true, synchronizationEnabled: true);
            Compare(referenceUnit, tableUnit, failures,
                "rally-transform-complete-unit");
            Compare(referenceTracking, tableTracking, failures,
                "rally-transform-complete-tracking");
            if (!tableTracking.Active || tableUnit.Animation != 0x81 ||
                tableUnit.SpeedBonus != 2)
            {
                failures.Add(
                    "rally-transform-complete did not apply the Knight 0x81/2 profile before synchronization");
            }
        }

        private static void RunActivationFlagMatrix(
            List<string> failures,
            Dictionary<ushort, Profile> profiles)
        {
            foreach (bool rallyEnabled in new[] { false, true })
            foreach (bool synchronizationEnabled in new[] { false, true })
            {
                Unit referenceUnit = UnitFor(1, 1);
                Unit tableUnit = referenceUnit.Clone();
                Tracking referenceTracking = TrackingFor(1);
                Tracking tableTracking = referenceTracking.Clone();
                ApplyCombinedReference(referenceUnit, referenceTracking, profiles,
                    rallyEnabled, synchronizationEnabled);
                ApplyCombinedTable(tableUnit, tableTracking, profiles,
                    rallyEnabled, synchronizationEnabled);
                string name = $"flags-rally-{rallyEnabled}-sync-{synchronizationEnabled}";
                Compare(referenceUnit, tableUnit, failures, name + "-unit");
                Compare(referenceTracking, tableTracking, failures, name + "-tracking");
                if (rallyEnabled && referenceUnit.SpeedBonus == 99)
                    failures.Add(name + ": synchronization overrode active rally");
                if (!rallyEnabled && !synchronizationEnabled &&
                    (referenceUnit.Animation != 1 || referenceUnit.SpeedBonus != 55))
                    failures.Add(name + ": disabled native flags changed Vanilla state");
            }
        }

        private static void ApplyCombinedReference(
            Unit unit,
            Tracking tracking,
            Dictionary<ushort, Profile> profiles,
            bool rallyEnabled,
            bool synchronizationEnabled)
        {
            bool handled = rallyEnabled &&
                ApplyRallyReference(unit, tracking, profiles, improvedSpearmen: true);
            if (!handled && synchronizationEnabled)
                ApplySynchronizationReference(unit, profiles, running: true, bonus: 99);
        }

        private static void ApplyCombinedTable(
            Unit unit,
            Tracking tracking,
            Dictionary<ushort, Profile> profiles,
            bool rallyEnabled,
            bool synchronizationEnabled)
        {
            bool handled = rallyEnabled &&
                ApplyRallyTable(unit, tracking, profiles, improvedSpearmen: true);
            if (!handled && synchronizationEnabled)
                ApplySynchronizationTable(unit, profiles, running: true, bonus: 99);
        }

        private static bool ApplyRallyReference(
            Unit unit,
            Tracking tracking,
            Dictionary<ushort, Profile> profiles,
            bool improvedSpearmen)
        {
            if (!tracking.Active)
                return false;
            if (unit.AliveState != AliveStateValue)
                return false;
            if (tracking.GlobalId != 0 && unit.GlobalId != tracking.GlobalId)
            {
                tracking.Active = false;
                return false;
            }
            if (unit.Owner != tracking.Owner)
            {
                tracking.Active = false;
                return false;
            }
            if (unit.Type != tracking.ExpectedType)
            {
                if (unit.AiState == InitializationState ||
                    unit.TransformType == tracking.ExpectedType)
                    return true;
                tracking.Active = false;
                return false;
            }
            if (!unit.HasPath)
            {
                tracking.Moving = false;
                return true;
            }
            if (!tracking.Observed)
            {
                tracking.Observed = true;
                tracking.GlobalId = unit.GlobalId;
            }
            else if (!tracking.Moving &&
                     (unit.TargetX != tracking.TargetX || unit.TargetY != tracking.TargetY))
            {
                tracking.Active = false;
                return false;
            }
            tracking.Moving = true;
            tracking.TargetX = unit.TargetX;
            tracking.TargetY = unit.TargetY;
            ApplyRallyProfile(unit, profiles, improvedSpearmen);
            return true;
        }

        private static bool ApplyRallyTable(
            Unit unit,
            Tracking tracking,
            Dictionary<ushort, Profile> profiles,
            bool improvedSpearmen)
        {
            if (!tracking.Active)
                return false;
            if (unit.AliveState != AliveStateValue)
                return false;
            if ((tracking.GlobalId != 0 && unit.GlobalId != tracking.GlobalId) ||
                unit.Owner != tracking.Owner)
            {
                tracking.Active = false;
                return false;
            }
            if (unit.Type != tracking.ExpectedType)
            {
                if (unit.AiState == InitializationState ||
                    unit.TransformType == tracking.ExpectedType)
                    return true;
                tracking.Active = false;
                return false;
            }
            if (!unit.HasPath)
            {
                tracking.Moving = false;
                return true;
            }
            if (!tracking.Observed)
            {
                tracking.Observed = true;
                if (tracking.GlobalId == 0)
                    tracking.GlobalId = unit.GlobalId;
            }
            else if (!tracking.Moving &&
                     (unit.TargetX != tracking.TargetX || unit.TargetY != tracking.TargetY))
            {
                tracking.Active = false;
                return false;
            }
            tracking.Moving = true;
            tracking.TargetX = unit.TargetX;
            tracking.TargetY = unit.TargetY;
            ApplyRallyProfile(unit, profiles, improvedSpearmen);
            return true;
        }

        private static void ApplyRallyProfile(
            Unit unit,
            Dictionary<ushort, Profile> profiles,
            bool improvedSpearmen)
        {
            if (unit.Type == SpearmanType && !improvedSpearmen)
                return;
            if (!profiles.TryGetValue(unit.Type, out Profile profile))
                return;
            if (profile.Running.TryGetValue(unit.Animation, out uint running))
            {
                unit.Animation = running;
                unit.SpeedBonus = profile.Bonus;
            }
            else if (profile.AllowFallback && profile.SoleRunningState != 0)
            {
                unit.Animation = profile.SoleRunningState;
                unit.SpeedBonus = profile.Bonus;
            }
        }

        private static void ApplySynchronizationReference(
            Unit unit,
            Dictionary<ushort, Profile> profiles,
            bool running,
            ushort bonus)
        {
            if (unit.AliveState != AliveStateValue)
                return;
            unit.SpeedBonus = running ? bonus : (ushort)0;
            if (!profiles.TryGetValue(unit.Type, out Profile profile))
                return;
            Dictionary<uint, uint> mappings = running ? profile.Running : profile.Walking;
            if (mappings.TryGetValue(unit.Animation, out uint state))
                unit.Animation = state;
        }

        private static void ApplySynchronizationTable(
            Unit unit,
            Dictionary<ushort, Profile> profiles,
            bool running,
            ushort bonus)
        {
            if (unit.AliveState != AliveStateValue)
                return;
            unit.SpeedBonus = running ? bonus : (ushort)0;
            if (!profiles.TryGetValue(unit.Type, out Profile profile))
                return;
            Dictionary<uint, uint> mappings = running ? profile.Running : profile.Walking;
            foreach (KeyValuePair<uint, uint> mapping in mappings)
            {
                if (mapping.Key != unit.Animation)
                    continue;
                unit.Animation = mapping.Value;
                break;
            }
        }

        private static void ApplySpeedReference(Unit unit, Tracking tracking)
        {
            if (tracking.Active && unit.AliveState == AliveStateValue && unit.HasPath &&
                (tracking.GlobalId == 0 || unit.GlobalId == tracking.GlobalId) &&
                unit.Owner == tracking.Owner && unit.Type == tracking.ExpectedType)
                unit.CurrentSpeed2 = unit.CurrentSpeed;
        }

        private static void ApplySpeedTable(Unit unit, Tracking tracking)
        {
            if (!tracking.Active || unit.AliveState != AliveStateValue)
                return;
            if (tracking.GlobalId != 0 && unit.GlobalId != tracking.GlobalId)
                return;
            if (unit.Owner != tracking.Owner || unit.Type != tracking.ExpectedType || !unit.HasPath)
                return;
            unit.CurrentSpeed2 = unit.CurrentSpeed;
        }

        private static GroupResult AnalyzeGroupReference(GroupMember[] members)
        {
            var types = new HashSet<ushort>();
            ushort slowest = 0;
            ushort bonus = 0;
            bool supports = true;
            bool first = true;
            foreach (GroupMember member in members)
            {
                if (!member.Alive)
                    continue;
                types.Add(member.Type);
                if (first || member.MaximumSpeed > slowest)
                {
                    slowest = member.MaximumSpeed;
                    bonus = member.Bonus;
                    first = false;
                }
                else if (member.MaximumSpeed == slowest && member.Bonus < bonus)
                    bonus = member.Bonus;
                supports &= member.SupportsCadence;
            }
            return new GroupResult(types.Count >= 2, slowest,
                supports && bonus > 0, supports ? bonus : (ushort)0);
        }

        private static GroupResult AnalyzeGroupSinglePass(GroupMember[] members)
        {
            var typeCache = new Dictionary<ushort, bool>();
            ushort maximumDelay = 0;
            ushort sharedBonus = 0;
            int active = 0;
            bool shared = true;
            foreach (GroupMember member in members)
            {
                if (!member.Alive)
                    continue;
                bool first = active++ == 0;
                typeCache[member.Type] = true;
                if (first || member.MaximumSpeed > maximumDelay)
                {
                    maximumDelay = member.MaximumSpeed;
                    sharedBonus = member.Bonus;
                }
                else if (member.MaximumSpeed == maximumDelay && member.Bonus < sharedBonus)
                    sharedBonus = member.Bonus;
                if (!member.SupportsCadence)
                    shared = false;
            }
            return new GroupResult(typeCache.Count >= 2, maximumDelay,
                shared && sharedBonus > 0, shared ? sharedBonus : (ushort)0);
        }

        private static Dictionary<ushort, Profile> CreateProfiles()
        {
            var profiles = new Dictionary<ushort, Profile>();
            foreach (eChimps unitType in SupportedRallyTypes)
            {
                ushort type = (ushort)unitType;
                bool isArabBow = unitType == eChimps.CHIMP_TYPE_ARAB_BOW;
                bool isKnight = unitType == eChimps.CHIMP_TYPE_KNIGHT;
                bool allowFallback = isArabBow ||
                    unitType == eChimps.CHIMP_TYPE_BEDOUIN_HEALER;
                profiles[type] = new Profile(
                    new Dictionary<uint, uint> { [1] = 0x81, [0x81] = 0x81 },
                    new Dictionary<uint, uint> { [0x81] = 1, [1] = 1 },
                    isArabBow ? (ushort)1 : isKnight ? (ushort)2 : (ushort)(type + 1),
                    allowFallback,
                    soleRunningState: isArabBow
                        ? 0x81u
                        : unitType == eChimps.CHIMP_TYPE_BEDOUIN_HEALER
                            ? 0x5C1u
                            : 0u);
            }
            return profiles;
        }

        private static Unit UnitFor(
            ushort type,
            uint animation,
            bool path = true,
            ushort aiState = 0,
            ushort transformType = 0,
            int owner = 1,
            uint globalId = 77,
            ushort targetX = 33,
            ushort targetY = 34)
        {
            return new Unit
            {
                AliveState = AliveStateValue,
                Type = type,
                Animation = animation,
                HasPath = path,
                AiState = aiState,
                TransformType = transformType,
                Owner = owner,
                GlobalId = globalId,
                TargetX = targetX,
                TargetY = targetY,
                SpeedBonus = 55
            };
        }

        private static Tracking TrackingFor(
            ushort type,
            bool observed = false,
            bool moving = false,
            uint globalId = 77,
            bool active = true)
        {
            return new Tracking
            {
                Active = active,
                GlobalId = globalId,
                Owner = 1,
                ExpectedType = type,
                Observed = observed,
                Moving = moving,
                TargetX = 33,
                TargetY = 34
            };
        }

        private static RallyCase Rally(
            string name,
            Unit unit,
            Tracking tracking,
            bool improvedSpearmen) =>
            new RallyCase(name, unit, tracking, improvedSpearmen);

        private static Unit Dead(Unit unit)
        {
            unit.AliveState = DeadState;
            return unit;
        }

        private static GroupMember Member(
            ushort type,
            ushort maximumSpeed,
            bool supports,
            ushort bonus,
            bool alive = true) =>
            new GroupMember(type, maximumSpeed, supports, bonus, alive);

        private static void Compare(
            Unit expected,
            Unit actual,
            List<string> failures,
            string name)
        {
            if (expected.AliveState != actual.AliveState || expected.GlobalId != actual.GlobalId ||
                expected.Owner != actual.Owner || expected.Type != actual.Type ||
                expected.Animation != actual.Animation || expected.SpeedBonus != actual.SpeedBonus ||
                expected.CurrentSpeed != actual.CurrentSpeed ||
                expected.CurrentSpeed2 != actual.CurrentSpeed2)
                failures.Add(name + ": unit fields differ");
        }

        private static void Compare(
            Tracking expected,
            Tracking actual,
            List<string> failures,
            string name)
        {
            if (expected.Active != actual.Active || expected.GlobalId != actual.GlobalId ||
                expected.Owner != actual.Owner || expected.ExpectedType != actual.ExpectedType ||
                expected.TargetX != actual.TargetX || expected.TargetY != actual.TargetY ||
                expected.Observed != actual.Observed || expected.Moving != actual.Moving)
                failures.Add(name + ": tracking fields differ");
        }

        private static void Compare(
            GroupResult expected,
            GroupResult actual,
            List<string> failures,
            string name)
        {
            if (expected.Applied != actual.Applied || expected.Speed != actual.Speed ||
                expected.Running != actual.Running || expected.Bonus != actual.Bonus)
                failures.Add(name + ": group decision differs");
        }

        private sealed class Unit
        {
            public ushort AliveState;
            public uint GlobalId;
            public int Owner;
            public ushort Type;
            public ushort TransformType;
            public ushort AiState;
            public bool HasPath;
            public ushort TargetX;
            public ushort TargetY;
            public uint Animation;
            public ushort SpeedBonus;
            public ushort CurrentSpeed;
            public ushort CurrentSpeed2;

            public Unit Clone() => (Unit)MemberwiseClone();
        }

        private sealed class Tracking
        {
            public bool Active;
            public uint GlobalId;
            public int Owner;
            public ushort ExpectedType;
            public ushort TargetX;
            public ushort TargetY;
            public bool Observed;
            public bool Moving;

            public Tracking Clone() => (Tracking)MemberwiseClone();
        }

        private sealed class Profile
        {
            public Profile(
                Dictionary<uint, uint> running,
                Dictionary<uint, uint> walking,
                ushort bonus,
                bool allowFallback,
                uint soleRunningState)
            {
                Running = running;
                Walking = walking;
                Bonus = bonus;
                AllowFallback = allowFallback;
                SoleRunningState = soleRunningState;
            }

            public Dictionary<uint, uint> Running { get; }
            public Dictionary<uint, uint> Walking { get; }
            public ushort Bonus { get; }
            public bool AllowFallback { get; }
            public uint SoleRunningState { get; }
        }

        private readonly struct GroupMember
        {
            public GroupMember(ushort type, ushort speed, bool supports, ushort bonus, bool alive)
            {
                Type = type;
                MaximumSpeed = speed;
                SupportsCadence = supports;
                Bonus = bonus;
                Alive = alive;
            }

            public ushort Type { get; }
            public ushort MaximumSpeed { get; }
            public bool SupportsCadence { get; }
            public ushort Bonus { get; }
            public bool Alive { get; }
        }

        private readonly struct GroupResult
        {
            public GroupResult(bool applied, ushort speed, bool running, ushort bonus)
            {
                Applied = applied;
                Speed = speed;
                Running = running;
                Bonus = bonus;
            }

            public bool Applied { get; }
            public ushort Speed { get; }
            public bool Running { get; }
            public ushort Bonus { get; }
        }

        private sealed class RallyCase
        {
            public RallyCase(string name, Unit unit, Tracking tracking, bool improvedSpearmen)
            {
                Name = name;
                Unit = unit;
                Tracking = tracking;
                ImprovedSpearmen = improvedSpearmen;
            }

            public string Name { get; }
            public Unit Unit { get; }
            public Tracking Tracking { get; }
            public bool ImprovedSpearmen { get; }
        }
    }
}
