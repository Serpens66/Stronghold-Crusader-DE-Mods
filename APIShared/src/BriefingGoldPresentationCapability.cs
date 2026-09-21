using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace APIShared
{
    internal sealed unsafe class BriefingGoldPresentationService
    {
        private const int BriefingSlotCount = 8;
        private delegate void ButtonGotoBriefingDelegate(MainViewModel self, object parameter);

        private readonly object sync = new object();
        private readonly List<Registration> registrations = new List<Registration>();
        private readonly HashSet<string> loggedFailures = new HashSet<string>(StringComparer.Ordinal);
        private readonly ManualLogSource log;
        private Registration[] registrationView = Array.Empty<Registration>();
        private Hook briefingHook;
        private ButtonGotoBriefingDelegate briefingOriginal;

        internal BriefingGoldPresentationService(ManualLogSource logger)
        {
            log = logger;
        }

        internal static bool TryCreate(
            ManualLogSource log,
            out BriefingGoldPresentationService service,
            out NativeCapabilityDiagnostic diagnostic)
        {
            service = null;
            Hook pending = null;
            try
            {
                var candidate = new BriefingGoldPresentationService(log);
                MethodInfo method = typeof(MainViewModel).GetMethod(
                    nameof(MainViewModel.ButtonGotoBriefing),
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new[] { typeof(object) },
                    null) ?? throw new MissingMethodException(
                        typeof(MainViewModel).FullName,
                        nameof(MainViewModel.ButtonGotoBriefing));

                pending = new Hook(
                    method,
                    (ButtonGotoBriefingDelegate)candidate.ButtonGotoBriefingHook,
                    new HookConfig
                    {
                        ManualApply = true,
                        ID = "APIShared.BriefingGold.ButtonGotoBriefing"
                    });
                candidate.briefingOriginal = pending.GenerateTrampoline<ButtonGotoBriefingDelegate>();
                pending.Apply();
                candidate.briefingHook = pending;
                pending = null;

                service = candidate;
                diagnostic = candidate.Available(
                    "Process-wide post-Vanilla mission-briefing gold presentation is active.");
                TryLogInfo(log, "Process-wide briefing-gold presentation installed.");
                return true;
            }
            catch (Exception ex)
            {
                try { pending?.Undo(); } catch { }
                try { pending?.Dispose(); } catch { }
                diagnostic = new NativeCapabilityDiagnostic(
                    NativeCapabilityIds.BriefingGoldPresentation,
                    NativeCapabilityState.Faulted,
                    string.Empty,
                    ex.Message);
                TryLogError(log, $"Briefing-gold presentation failed before publication: {ex}");
                return false;
            }
        }

        internal IBriefingGoldPresentationCapability Bind(string ownerGuid) =>
            new Binding(this, ownerGuid);

        private bool Register(
            string owner,
            string registrationId,
            BriefingGoldAdjustmentStage stage,
            BriefingGoldAdjuster adjuster,
            out NativeCapabilityDiagnostic diagnostic)
        {
            if (string.IsNullOrWhiteSpace(registrationId) ||
                adjuster == null ||
                !Enum.IsDefined(typeof(BriefingGoldAdjustmentStage), stage))
            {
                diagnostic = new NativeCapabilityDiagnostic(
                    NativeCapabilityIds.BriefingGoldPresentation,
                    NativeCapabilityState.ValidationFailed,
                    string.Empty,
                    "A registration ID, supported stage and adjuster are required.");
                return false;
            }

            lock (sync)
            {
                if (registrations.Any(entry =>
                    string.Equals(entry.Owner, owner, StringComparison.Ordinal) &&
                    string.Equals(entry.Id, registrationId, StringComparison.Ordinal)))
                {
                    diagnostic = new NativeCapabilityDiagnostic(
                        NativeCapabilityIds.BriefingGoldPresentation,
                        NativeCapabilityState.Conflict,
                        string.Empty,
                        "The owner already registered this briefing-gold adjustment ID.",
                        owner);
                    return false;
                }

                registrations.Add(new Registration(owner, registrationId, stage, adjuster));
                registrations.Sort(Compare);
                registrationView = registrations.ToArray();
            }

            diagnostic = Available("Briefing-gold adjustment registered for the process lifetime.");
            return true;
        }

        private void ButtonGotoBriefingHook(MainViewModel self, object parameter)
        {
            briefingOriginal(self, parameter);
            try
            {
                ApplyToVisibleSlots(self);
            }
            catch (Exception ex)
            {
                LogFailure("presentation", ex);
            }
        }

        private void ApplyToVisibleSlots(MainViewModel self)
        {
            Registration[] view = registrationView;
            if (view.Length == 0 || self == null)
                return;

            bool hasNoGoldState = TryReadNoStartingGold(out bool noGoldEnabled);
            int count = Math.Min(
                BriefingSlotCount,
                Math.Min(
                    self.SkirmishBriefingGold?.Count ?? 0,
                    Math.Min(
                        self.SkirmishBriefingAlly?.Count ?? 0,
                        self.AlliesHumanFaceVis?.Count ?? 0)));

            for (int slotIndex = 0; slotIndex < count; slotIndex++)
            {
                if (!self.SkirmishBriefingAlly[slotIndex])
                    continue;
                if (!int.TryParse(
                    self.SkirmishBriefingGold[slotIndex],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int vanillaGold) || vanillaGold < 0)
                {
                    LogFailure(
                        "slot-" + slotIndex,
                        new FormatException("Vanilla briefing gold is not a non-negative integer."));
                    continue;
                }

                bool isHuman = self.AlliesHumanFaceVis[slotIndex];
                int adjusted = Evaluate(
                    view,
                    slotIndex,
                    isHuman,
                    vanillaGold,
                    hasNoGoldState,
                    noGoldEnabled);
                if (adjusted != vanillaGold)
                    self.SkirmishBriefingGold[slotIndex] =
                        adjusted.ToString(CultureInfo.InvariantCulture);
            }
        }

        internal int EvaluateForTests(
            int slotIndex,
            bool isHuman,
            int vanillaGold,
            bool hasNoGoldState,
            bool noGoldEnabled) =>
            Evaluate(
                registrationView,
                slotIndex,
                isHuman,
                vanillaGold,
                hasNoGoldState,
                noGoldEnabled);

        private int Evaluate(
            Registration[] view,
            int slotIndex,
            bool isHuman,
            int vanillaGold,
            bool hasNoGoldState,
            bool noGoldEnabled)
        {
            int effectiveVanillaGold =
                hasNoGoldState && noGoldEnabled && isHuman ? 0 : vanillaGold;
            int currentGold = vanillaGold;
            for (int index = 0; index < view.Length; index++)
            {
                Registration registration = view[index];
                var context = new BriefingGoldContext(
                    slotIndex,
                    isHuman,
                    vanillaGold,
                    effectiveVanillaGold,
                    currentGold,
                    hasNoGoldState,
                    noGoldEnabled);
                try
                {
                    int candidate = registration.Adjuster(context);
                    if (candidate < 0)
                    {
                        LogFailure(
                            registration.Key,
                            new InvalidOperationException(
                                "A briefing-gold adjustment returned a negative value."));
                        continue;
                    }
                    currentGold = candidate;
                }
                catch (Exception ex)
                {
                    LogFailure(registration.Key, ex);
                }
            }
            return currentGold;
        }

        private static bool TryReadNoStartingGold(out bool enabled)
        {
            enabled = false;
            try
            {
                var options = GamePlayerManagerAPI.Instance._choreManagerOptionsInternal;
                if (!options.IsValid())
                    return false;
                enabled = options.AdvOpt_NoGold > 0;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void LogFailure(string area, Exception ex)
        {
            lock (sync)
                if (!loggedFailures.Add(area))
                    return;
            TryLogError(
                log,
                $"Briefing-gold {area} failed closed; the last safe value remains active: {ex}");
        }

        private NativeCapabilityDiagnostic Available(string reason) =>
            new NativeCapabilityDiagnostic(
                NativeCapabilityIds.BriefingGoldPresentation,
                NativeCapabilityState.Available,
                string.Empty,
                reason);

        private static int Compare(Registration left, Registration right)
        {
            int result = left.Stage.CompareTo(right.Stage);
            if (result != 0)
                return result;
            result = string.CompareOrdinal(left.Owner, right.Owner);
            return result != 0 ? result : string.CompareOrdinal(left.Id, right.Id);
        }

        private static void TryLogInfo(ManualLogSource log, string message)
        {
            try { NativeApiLog.Info(log, message); } catch { }
        }

        private static void TryLogError(ManualLogSource log, string message)
        {
            try { NativeApiLog.Error(log, message); } catch { }
        }

        private sealed class Binding : IBriefingGoldPresentationCapability
        {
            private readonly BriefingGoldPresentationService service;
            private readonly string owner;

            internal Binding(BriefingGoldPresentationService service, string owner)
            {
                this.service = service;
                this.owner = owner;
            }

            public bool TryRegisterAdjustment(
                string registrationId,
                BriefingGoldAdjustmentStage stage,
                BriefingGoldAdjuster adjuster,
                out NativeCapabilityDiagnostic diagnostic) =>
                service.Register(owner, registrationId, stage, adjuster, out diagnostic);
        }

        private sealed class Registration
        {
            internal Registration(
                string owner,
                string id,
                BriefingGoldAdjustmentStage stage,
                BriefingGoldAdjuster adjuster)
            {
                Owner = owner;
                Id = id;
                Stage = stage;
                Adjuster = adjuster;
            }

            internal string Owner { get; }
            internal string Id { get; }
            internal BriefingGoldAdjustmentStage Stage { get; }
            internal BriefingGoldAdjuster Adjuster { get; }
            internal string Key => Owner + ":" + Id;
        }
    }
}
