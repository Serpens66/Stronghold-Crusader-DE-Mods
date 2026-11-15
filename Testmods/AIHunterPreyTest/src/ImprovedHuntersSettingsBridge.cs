using System;
using System.Reflection;
using APIShared.ModSettings;
using APIShared.Wildlife;
using BepInEx.Bootstrap;
using SHCDESE.Interop;

namespace AIHunterPreyTest
{
    internal enum ImprovedHuntersSettingsStatus { Unavailable, Absent, Ready }
    internal unsafe delegate bool ImprovedHuntersCorpsePredicate(GameUnit* unit);

    // Settings are captured on Unity. The optional closed corpse predicate reads IH's current
    // simulation-owned reservation/expiry state directly, without reflection or UI access.
    internal sealed class ImprovedHuntersSettingsSnapshot
    {
        private readonly bool enabled, deer, goat, rabbit, camel, chicken;
        private readonly ImprovedHuntersCorpsePredicate corpsePredicate;
        internal ImprovedHuntersSettingsSnapshot(ImprovedHuntersSettingsStatus status, string diagnostic,
            bool enabled = false, bool deer = true, bool goat = true, bool rabbit = true,
            bool camel = true, bool chicken = true, bool collectsCorpses = false,
            ImprovedHuntersCorpsePredicate corpsePredicate = null)
        {
            Status = status;
            Diagnostic = diagnostic ?? "";
            this.enabled = enabled;
            this.deer = deer; this.goat = goat; this.rabbit = rabbit; this.camel = camel; this.chicken = chicken;
            CollectsCorpses = collectsCorpses;
            this.corpsePredicate = corpsePredicate;
        }

        internal ImprovedHuntersSettingsStatus Status { get; }
        internal string Diagnostic { get; }
        internal bool CollectsCorpses { get; }
        internal bool IsKnownAnimal(int type) => IsKnownAnimalType(type);

        // Simulation thread, immediate unit pointer only. Reuse IH's exact free-corpse decision:
        // reservation, species-specific fading states and expired keep-alive records stay its contract.
        // Exceptions propagate so the caller treats the prey scan as unknown instead of exhausted.
        internal unsafe bool IsCollectibleCorpse(GameUnit* unit) =>
            CollectsCorpses && corpsePredicate != null && corpsePredicate(unit);

        // This is the type/activation part of IH's policy, deliberately without per-Hunter range,
        // ranking, temporary reservations, cooldowns or path checks. The caller owns the unit-state test.
        internal bool AllowsType(int type, HuntablePreyEntry entry)
        {
            if (Status == ImprovedHuntersSettingsStatus.Unavailable)
                throw new InvalidOperationException("Improved Hunters settings are unknown: " + Diagnostic);
            if (entry == null || entry.UnitType != type) return false;
            if (Status == ImprovedHuntersSettingsStatus.Absent) return true;
            if (enabled)
            {
                switch ((eChimps)type)
                {
                    case eChimps.CHIMP_TYPE_DEER: return deer;
                    case eChimps.CHIMP_TYPE_GOAT: return goat;
                    case eChimps.CHIMP_TYPE_RABBIT: return rabbit;
                    case eChimps.CHIMP_TYPE_CAMEL: return camel;
                    case eChimps.CHIMP_TYPE_CHICKEN: return chicken;
                    default: return true;
                }
            }

            // A disabled IH refuses only its exact frozen admission, preserving a foreign winner
            // for that same type (HunterInactivePreyPolicy). Vanilla deer/goat remain available.
            return !string.Equals(entry.OwnerGuid, ImprovedHuntersSettingsBridge.PluginGuid, StringComparison.Ordinal) ||
                !string.Equals(entry.RegistrationId, OwnRegistrationId(type), StringComparison.Ordinal);
        }

        internal static bool IsKnownAnimalType(int type) => type == (int)eChimps.CHIMP_TYPE_DEER ||
            type == (int)eChimps.CHIMP_TYPE_GOAT || type == (int)eChimps.CHIMP_TYPE_RABBIT ||
            type == (int)eChimps.CHIMP_TYPE_CAMEL || type == (int)eChimps.CHIMP_TYPE_CHICKEN;

        private static string OwnRegistrationId(int type)
        {
            switch ((eChimps)type)
            {
                case eChimps.CHIMP_TYPE_RABBIT: return nameof(eChimps.CHIMP_TYPE_RABBIT);
                case eChimps.CHIMP_TYPE_CAMEL: return nameof(eChimps.CHIMP_TYPE_CAMEL);
                case eChimps.CHIMP_TYPE_CHICKEN: return nameof(eChimps.CHIMP_TYPE_CHICKEN);
                default: return null;
            }
        }
    }

    // Optional integration: no compile-time reference to ImprovedHunters and no private native queries.
    // Its own assembly's mode gate and runtime readiness are read only at lifecycle snapshot boundaries.
    internal sealed unsafe class ImprovedHuntersSettingsBridge
    {
        internal const string PluginGuid = "ImprovedHunters_Serp";
        private object endpoint;
        private Func<bool> enableMod, collectFreeCorpses, actualModeAllowed;
        private Func<eChimps, bool> isKnownAnimal, isHuntingEnabled;
        private FieldInfo runtimeField, targetPolicyField, targetSelectionModeField, corpsePatchField;
        private PropertyInfo corpsePatchAvailable;
        private MethodInfo corpsePredicateMethod;
        private object corpsePredicateRuntime;
        private ImprovedHuntersCorpsePredicate corpsePredicate;

        // Call after synchronized settings application (BeforeNativeStart), then again at NativeLoaded
        // and OnStart. APIShared's endpoint dictionary is Unity-owned; never capture in a sim callback.
        internal ImprovedHuntersSettingsSnapshot Capture(bool missionAllowsCustomMods)
        {
            try
            {
                if (!ModSettingsApplication.Endpoints.TryGetValue(PluginGuid, out var published))
                    return Chainloader.PluginInfos.ContainsKey(PluginGuid)
                        ? Unknown("The plugin is loaded but has no registered settings endpoint.")
                        : new ImprovedHuntersSettingsSnapshot(ImprovedHuntersSettingsStatus.Absent, "Improved Hunters is absent.");
                if (!ReferenceEquals(endpoint, published)) Bind(published);
                object runtime = runtimeField.GetValue(null);
                if (runtime == null || !(bool)targetPolicyField.GetValue(runtime))
                    return Unknown("Its target policy has not initialized successfully.");

                bool active = missionAllowsCustomMods && actualModeAllowed() && enableMod();
                bool knownDeer = ReadKnown(eChimps.CHIMP_TYPE_DEER);
                bool knownGoat = ReadKnown(eChimps.CHIMP_TYPE_GOAT);
                bool knownRabbit = ReadKnown(eChimps.CHIMP_TYPE_RABBIT);
                bool knownCamel = ReadKnown(eChimps.CHIMP_TYPE_CAMEL);
                bool knownChicken = ReadKnown(eChimps.CHIMP_TYPE_CHICKEN);
                if (!knownDeer || !knownGoat || !knownRabbit || !knownCamel || !knownChicken)
                    return Unknown("Its known-animal contract has changed.");
                object patch = corpsePatchField.GetValue(runtime);
                bool corpses = active && collectFreeCorpses() && (bool)targetSelectionModeField.GetValue(runtime) &&
                    patch != null && (bool)corpsePatchAvailable.GetValue(patch, null);
                if (corpses && !ReferenceEquals(corpsePredicateRuntime, runtime))
                {
                    corpsePredicate = (ImprovedHuntersCorpsePredicate)Delegate.CreateDelegate(
                        typeof(ImprovedHuntersCorpsePredicate), runtime, corpsePredicateMethod);
                    corpsePredicateRuntime = runtime;
                }
                return new ImprovedHuntersSettingsSnapshot(ImprovedHuntersSettingsStatus.Ready, "",
                    active, isHuntingEnabled(eChimps.CHIMP_TYPE_DEER), isHuntingEnabled(eChimps.CHIMP_TYPE_GOAT),
                    isHuntingEnabled(eChimps.CHIMP_TYPE_RABBIT), isHuntingEnabled(eChimps.CHIMP_TYPE_CAMEL),
                    isHuntingEnabled(eChimps.CHIMP_TYPE_CHICKEN), corpses, corpsePredicate);
            }
            catch (Exception error)
            {
                return Unknown(error.GetBaseException().Message);
            }
        }

        private bool ReadKnown(eChimps type) => isKnownAnimal(type);
        private static ImprovedHuntersSettingsSnapshot Unknown(string reason) =>
            new ImprovedHuntersSettingsSnapshot(ImprovedHuntersSettingsStatus.Unavailable, reason);

        private void Bind(object published)
        {
            Type type = published.GetType();
            Assembly assembly = type.Assembly;
            var nextEnable = BindBool(published, type, "EnableMod");
            var nextCorpses = BindBool(published, type, "CollectFreeCorpses");
            var nextKnown = BindAnimal(published, type, "IsKnownAnimal");
            var nextHunting = BindAnimal(published, type, "IsHuntingEnabled");
            Type gate = RequireType(assembly, "Shared.GameplayModActivationGate");
            MethodInfo modeGetter = gate.GetProperty("IsAllowed", BindingFlags.Static | BindingFlags.NonPublic)?.GetGetMethod(true);
            if (modeGetter == null || modeGetter.ReturnType != typeof(bool))
                throw new MissingMemberException("Improved Hunters has no readable effective mode gate.");
            var nextMode = (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), modeGetter);
            var nextRuntime = RequireField(RequireType(assembly, "ImprovedHunters.ImprovedHuntersPlugin"), "persistentRuntime", true);
            Type runtimeType = nextRuntime.FieldType;
            var nextPolicy = RequireBoolField(runtimeType, "targetPolicyRegistered");
            var nextSelection = RequireBoolField(runtimeType, "targetSelectionModeAllowed");
            var nextPatch = RequireField(runtimeType, "hunterCorpseChoicePatch", false);
            var nextAvailable = nextPatch.FieldType.GetProperty("IsAvailable", BindingFlags.Instance | BindingFlags.Public);
            if (nextAvailable == null || nextAvailable.PropertyType != typeof(bool) || !nextAvailable.CanRead)
                throw new MissingMemberException("Improved Hunters corpse readiness is unavailable.");
            var nextCorpsePredicate = runtimeType.GetMethod("IsFreeUsableCorpse", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(GameUnit*) }, null);
            if (nextCorpsePredicate == null || nextCorpsePredicate.ReturnType != typeof(bool))
                throw new MissingMemberException("Improved Hunters free-corpse predicate is unavailable.");
            enableMod = nextEnable; collectFreeCorpses = nextCorpses; isKnownAnimal = nextKnown; isHuntingEnabled = nextHunting;
            actualModeAllowed = nextMode; runtimeField = nextRuntime; targetPolicyField = nextPolicy;
            targetSelectionModeField = nextSelection; corpsePatchField = nextPatch; corpsePatchAvailable = nextAvailable;
            corpsePredicateMethod = nextCorpsePredicate; corpsePredicateRuntime = null; corpsePredicate = null;
            endpoint = published;
        }

        private static Type RequireType(Assembly assembly, string name) => assembly.GetType(name, true);
        private static FieldInfo RequireField(Type type, string name, bool isStatic)
        {
            var field = type.GetField(name, BindingFlags.NonPublic | (isStatic ? BindingFlags.Static : BindingFlags.Instance));
            if (field == null) throw new MissingMemberException(type.FullName, name);
            return field;
        }
        private static FieldInfo RequireBoolField(Type type, string name)
        {
            var field = RequireField(type, name, false);
            if (field.FieldType != typeof(bool)) throw new MissingMemberException(type.FullName, name);
            return field;
        }
        private static Func<bool> BindBool(object target, Type type, string name)
        {
            var getter = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetGetMethod();
            if (getter == null || getter.ReturnType != typeof(bool) || getter.GetParameters().Length != 0)
                throw new MissingMemberException(type.FullName, name);
            return (Func<bool>)Delegate.CreateDelegate(typeof(Func<bool>), target, getter);
        }
        private static Func<eChimps, bool> BindAnimal(object target, Type type, string name)
        {
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(eChimps) }, null);
            if (method == null || method.ReturnType != typeof(bool)) throw new MissingMemberException(type.FullName, name);
            return (Func<eChimps, bool>)Delegate.CreateDelegate(typeof(Func<eChimps, bool>), target, method);
        }
    }
}
