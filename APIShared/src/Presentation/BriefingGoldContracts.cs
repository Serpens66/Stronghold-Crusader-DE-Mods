using System;

namespace APIShared
{
    /// <summary>Fixed ordering stages for mission-briefing starting-gold adjustments.</summary>
    public enum BriefingGoldAdjustmentStage
    {
        /// <summary>Corrections that make the displayed Vanilla base match Vanilla gameplay.</summary>
        VanillaCorrection = 0,
        /// <summary>Adjustments contributed by gameplay mods after the effective Vanilla base is known.</summary>
        ModAdjustment = 100
    }

    /// <summary>Immutable state for one visible player slot in the mission briefing.</summary>
    public sealed class BriefingGoldContext
    {
        /// <summary>Creates a mission-briefing starting-gold context.</summary>
        public BriefingGoldContext(
            int slotIndex,
            bool isHuman,
            int vanillaDisplayedGold,
            int effectiveVanillaGold,
            int currentGold,
            bool hasNoStartingGoldState,
            bool noStartingGoldEnabled)
        {
            SlotIndex = slotIndex;
            IsHuman = isHuman;
            VanillaDisplayedGold = vanillaDisplayedGold;
            EffectiveVanillaGold = effectiveVanillaGold;
            CurrentGold = currentGold;
            HasNoStartingGoldState = hasNoStartingGoldState;
            NoStartingGoldEnabled = noStartingGoldEnabled;
        }

        /// <summary>Zero-based slot in the eight-player briefing presentation.</summary>
        public int SlotIndex { get; }
        /// <summary>Whether Vanilla classified this slot as a human player.</summary>
        public bool IsHuman { get; }
        /// <summary>Gold written by Vanilla's briefing method before shared adjustments.</summary>
        public int VanillaDisplayedGold { get; }
        /// <summary>Vanilla's actual starting gold after its No Starting Gold rule.</summary>
        public int EffectiveVanillaGold { get; }
        /// <summary>Value produced by all earlier adjustment stages.</summary>
        public int CurrentGold { get; }
        /// <summary>Whether the current native No Starting Gold state was resolved safely.</summary>
        public bool HasNoStartingGoldState { get; }
        /// <summary>Whether Vanilla's effective No Starting Gold flag is enabled.</summary>
        public bool NoStartingGoldEnabled { get; }
    }

    /// <summary>Returns the non-negative gold value to pass to later adjustments.</summary>
    public delegate int BriefingGoldAdjuster(BriefingGoldContext context);

    /// <summary>Owner-bound process-wide mission-briefing gold presentation service.</summary>
    public interface IBriefingGoldPresentationCapability
    {
        /// <summary>Registers one deterministic process-lifetime adjustment.</summary>
        bool TryRegisterAdjustment(
            string registrationId,
            BriefingGoldAdjustmentStage stage,
            BriefingGoldAdjuster adjuster,
            out NativeCapabilityDiagnostic diagnostic);
    }
}
