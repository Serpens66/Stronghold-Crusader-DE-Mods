using Noesis;
using System;
using System.Collections.Generic;

namespace APIShared
{
    /// <summary>HUD surfaces on which a registered unit category may appear.</summary>
    [Flags]
    public enum UnitHudSurface
    {
        /// <summary>No surface.</summary>
        None = 0,
        /// <summary>The selected-troops HUD.</summary>
        TroopSelection = 1,
        /// <summary>The control-group overview.</summary>
        ControlGroups = 2,
        /// <summary>The hovered-unit name.</summary>
        UnitHover = 4,
        /// <summary>The army report.</summary>
        ArmyReport = 8,
        /// <summary>The Vanilla recruitment control for the category's base type.</summary>
        Recruitment = 16,
        /// <summary>The single-unit detail panel.</summary>
        UnitDetails = 32,
        /// <summary>Every currently supported unit presentation surface.</summary>
        All = TroopSelection | ControlGroups | UnitHover | ArmyReport | Recruitment | UnitDetails
    }

    /// <summary>Mouse buttons reported for a unit-category interaction.</summary>
    public enum UnitHudMouseButton
    {
        /// <summary>Left mouse button.</summary>
        Left,
        /// <summary>Right mouse button.</summary>
        Right,
        /// <summary>Middle mouse button.</summary>
        Middle
    }

    /// <summary>Typed image bindings reset by MainViewModel.UpdateUITroopSprites.</summary>
    public enum UnitHudImageSlot
    {
        /// <summary>Main/recruitment/control-group swordsman image.</summary>
        UIBuildingsO011,
        /// <summary>Main/recruitment/control-group swordsman hover image.</summary>
        UIBuildingsO012,
        /// <summary>Selected-swordsman troop HUD image.</summary>
        UIButtonsK007,
        /// <summary>Selected-swordsman troop HUD hover image.</summary>
        UIButtonsK008,
        /// <summary>Barracks swordsman recruitment image.</summary>
        UIButtonsO016,
        /// <summary>Barracks swordsman recruitment hover image.</summary>
        UIButtonsO017,
        /// <summary>Barracks swordsman recruitment disabled image.</summary>
        UIButtonsO018
    }

    /// <summary>Immutable RGB overlay tint used by shared category icons; alpha controls overlay opacity.</summary>
    public sealed class UnitHudTint
    {
        /// <summary>Creates an RGB overlay tint with explicit opacity.</summary>
        public UnitHudTint(byte red, byte green, byte blue, byte alpha)
        {
            Red = red; Green = green; Blue = blue; Alpha = alpha;
        }
        /// <summary>Red channel.</summary>
        public byte Red { get; }
        /// <summary>Green channel.</summary>
        public byte Green { get; }
        /// <summary>Blue channel.</summary>
        public byte Blue { get; }
        /// <summary>Overlay opacity; zero is transparent and 255 is fully opaque.</summary>
        public byte Alpha { get; }
    }

    /// <summary>Immutable validated unit identity presented to category matchers.</summary>
    public sealed class UnitHudUnitSnapshot
    {
        /// <summary>Creates a unit snapshot.</summary>
        public UnitHudUnitSnapshot(int gameId, uint globalId, int vanillaType, int ownerPlayerId, bool isAlive)
        {
            GameId = gameId; GlobalId = globalId; VanillaType = vanillaType; OwnerPlayerId = ownerPlayerId; IsAlive = isAlive;
        }
        /// <summary>Positive one-based game ID.</summary>
        public int GameId { get; }
        /// <summary>Native identity used to reject reused slots.</summary>
        public uint GlobalId { get; }
        /// <summary>Unmodified Vanilla unit type.</summary>
        public int VanillaType { get; }
        /// <summary>Controlling player ID.</summary>
        public int OwnerPlayerId { get; }
        /// <summary>Whether the validated native unit is alive.</summary>
        public bool IsAlive { get; }
    }

    /// <summary>Resolves whether a validated unit belongs to a custom HUD category.</summary>
    public delegate bool UnitHudCategoryMatcher(UnitHudUnitSnapshot unit);

    /// <summary>Resolves an image for a category. Returning null requests the Vanilla base icon.</summary>
    public delegate ImageSource UnitHudCategoryImageResolver();

    /// <summary>Text fields resolved for custom unit presentation.</summary>
    public enum UnitHudTextKind
    {
        /// <summary>Full unit name.</summary>
        DisplayName,
        /// <summary>Compact recruitment-selector label.</summary>
        ShortLabel,
        /// <summary>Longer unit description.</summary>
        Description
    }

    /// <summary>Resolves text in the game's currently active language. Null or empty results use the registered fallback.</summary>
    public delegate string UnitHudTextResolver(UnitHudTextKind kind);

    /// <summary>Immutable localized text presentation with deterministic fallbacks.</summary>
    public sealed class UnitHudTextProfile
    {
        /// <summary>Creates a text profile.</summary>
        public UnitHudTextProfile(string displayNameFallback, string shortLabelFallback, string descriptionFallback, UnitHudTextResolver resolver = null)
        {
            DisplayNameFallback = displayNameFallback ?? string.Empty;
            ShortLabelFallback = shortLabelFallback ?? string.Empty;
            DescriptionFallback = descriptionFallback ?? string.Empty;
            Resolver = resolver;
        }
        /// <summary>Fallback full name.</summary>
        public string DisplayNameFallback { get; }
        /// <summary>Fallback compact label.</summary>
        public string ShortLabelFallback { get; }
        /// <summary>Fallback description.</summary>
        public string DescriptionFallback { get; }
        /// <summary>Optional current-language resolver.</summary>
        public UnitHudTextResolver Resolver { get; }
    }

    /// <summary>Immutable custom unit-category definition.</summary>
    public sealed class UnitHudCategoryDefinition
    {
        /// <summary>Creates a custom presentation category.</summary>
        public UnitHudCategoryDefinition(
            string categoryId,
            string displayName,
            int baseUnitType,
            UnitHudSurface surfaces,
            UnitHudCategoryImageResolver imageResolver = null,
            UnitHudTint tint = null,
            int order = 0)
        {
            CategoryId = categoryId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            BaseUnitType = baseUnitType;
            Surfaces = surfaces;
            ImageResolver = imageResolver;
            Tint = tint ?? new UnitHudTint(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
            Order = order;
            TextProfile = new UnitHudTextProfile(DisplayName, DisplayName, string.Empty);
        }

        /// <summary>Creates a custom presentation category with localized text fallbacks.</summary>
        public UnitHudCategoryDefinition(
            string categoryId,
            string displayName,
            int baseUnitType,
            UnitHudSurface surfaces,
            UnitHudCategoryImageResolver imageResolver,
            UnitHudTint tint,
            int order,
            UnitHudTextProfile textProfile)
        {
            CategoryId = categoryId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            BaseUnitType = baseUnitType;
            Surfaces = surfaces;
            ImageResolver = imageResolver;
            Tint = tint ?? new UnitHudTint(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);
            Order = order;
            TextProfile = textProfile ?? new UnitHudTextProfile(DisplayName, DisplayName, string.Empty);
        }
        /// <summary>Stable owner-local category identifier.</summary>
        public string CategoryId { get; }
        /// <summary>Displayed category name.</summary>
        public string DisplayName { get; }
        /// <summary>Real Vanilla unit type retained by simulation.</summary>
        public int BaseUnitType { get; }
        /// <summary>Enabled presentation surfaces.</summary>
        public UnitHudSurface Surfaces { get; }
        /// <summary>Optional custom image resolver.</summary>
        public UnitHudCategoryImageResolver ImageResolver { get; }
        /// <summary>Icon overlay tint.</summary>
        public UnitHudTint Tint { get; }
        /// <summary>Ordering after the Vanilla base category.</summary>
        public int Order { get; }
        /// <summary>Localized text resolver and fallbacks.</summary>
        public UnitHudTextProfile TextProfile { get; }
    }

    /// <summary>Immutable owner-local ticket for one Vanilla recruitment action.</summary>
    public sealed class UnitHudRecruitmentTicket
    {
        /// <summary>Creates a ticket.</summary>
        public UnitHudRecruitmentTicket(long ticketId, string ownerGuid, string categoryId, int playerId, int baseUnitType, int requestedAmount)
        {
            TicketId = ticketId; OwnerGuid = ownerGuid; CategoryId = categoryId; PlayerId = playerId;
            BaseUnitType = baseUnitType; RequestedAmount = requestedAmount;
        }
        /// <summary>Process-local positive ticket ID.</summary>
        public long TicketId { get; }
        /// <summary>Registering owner.</summary>
        public string OwnerGuid { get; }
        /// <summary>Owner-local category.</summary>
        public string CategoryId { get; }
        /// <summary>Local player that requested recruitment.</summary>
        public int PlayerId { get; }
        /// <summary>Unmodified Vanilla type passed to GameAction.</summary>
        public int BaseUnitType { get; }
        /// <summary>Vanilla amount argument, including Shift/Ctrl semantics.</summary>
        public int RequestedAmount { get; }
    }

    /// <summary>Accepts or rejects a recruitment ticket before Vanilla's one GameAction call.</summary>
    public delegate bool UnitHudRecruitmentHandler(UnitHudRecruitmentTicket ticket);

    /// <summary>Immutable category instance list used by interaction and health consumers.</summary>
    public sealed class UnitHudCategorySnapshot
    {
        /// <summary>Creates a category snapshot.</summary>
        public UnitHudCategorySnapshot(string ownerGuid, string categoryId, string displayName, IReadOnlyList<UnitHudUnitSnapshot> units)
        {
            OwnerGuid = ownerGuid;
            CategoryId = categoryId;
            DisplayName = displayName;
            Units = new List<UnitHudUnitSnapshot>(units ?? throw new ArgumentNullException(nameof(units))).AsReadOnly();
        }
        /// <summary>Registering BepInEx owner.</summary>
        public string OwnerGuid { get; }
        /// <summary>Owner-local category ID.</summary>
        public string CategoryId { get; }
        /// <summary>Displayed name.</summary>
        public string DisplayName { get; }
        /// <summary>Validated category members.</summary>
        public IReadOnlyList<UnitHudUnitSnapshot> Units { get; }
    }

    /// <summary>Immutable one-of-eight selected-troop slot snapshot.</summary>
    public sealed class UnitHudSlotSnapshot
    {
        /// <summary>Creates a visible-slot snapshot.</summary>
        public UnitHudSlotSnapshot(int slot, int vanillaType, UnitHudCategorySnapshot category)
        {
            Slot = slot; VanillaType = vanillaType; Category = category;
        }
        /// <summary>Zero-based visual slot.</summary>
        public int Slot { get; }
        /// <summary>Vanilla type, or -1 for a custom category.</summary>
        public int VanillaType { get; }
        /// <summary>Custom category, or null for a Vanilla slot.</summary>
        public UnitHudCategorySnapshot Category { get; }
    }

    /// <summary>Immutable validated membership snapshot of one Vanilla control group.</summary>
    public sealed class UnitHudControlGroupSnapshot
    {
        /// <summary>Creates a control-group snapshot.</summary>
        public UnitHudControlGroupSnapshot(int group, IReadOnlyList<UnitHudUnitSnapshot> units)
        {
            Group = group;
            Units = new List<UnitHudUnitSnapshot>(units ?? throw new ArgumentNullException(nameof(units))).AsReadOnly();
        }
        /// <summary>Zero-based Vanilla group number.</summary>
        public int Group { get; }
        /// <summary>Validated concrete members with one-based game IDs.</summary>
        public IReadOnlyList<UnitHudUnitSnapshot> Units { get; }
    }

    /// <summary>Immutable category mouse interaction.</summary>
    public sealed class UnitHudInteractionContext
    {
        /// <summary>Creates an interaction context.</summary>
        public UnitHudInteractionContext(UnitHudMouseButton button, UnitHudCategorySnapshot category)
        {
            Button = button; Category = category;
        }
        /// <summary>Pressed mouse button.</summary>
        public UnitHudMouseButton Button { get; }
        /// <summary>Interacted custom category.</summary>
        public UnitHudCategorySnapshot Category { get; }
    }

    /// <summary>Observes category interactions after API selection handling.</summary>
    public delegate void UnitHudInteractionHandler(UnitHudInteractionContext context);

    /// <summary>Immutable context for a post-Vanilla HUD image override.</summary>
    public sealed class UnitHudImageOverrideContext
    {
        /// <summary>Creates an image override context.</summary>
        public UnitHudImageOverrideContext(int colour, bool arabic, UnitHudImageSlot slot, ImageSource vanillaImage, ImageSource currentImage)
        {
            Colour = colour; Arabic = arabic; Slot = slot; VanillaImage = vanillaImage; CurrentImage = currentImage;
        }
        /// <summary>Vanilla colour argument.</summary>
        public int Colour { get; }
        /// <summary>Vanilla culture argument.</summary>
        public bool Arabic { get; }
        /// <summary>Target property.</summary>
        public UnitHudImageSlot Slot { get; }
        /// <summary>Image produced by Vanilla.</summary>
        public ImageSource VanillaImage { get; }
        /// <summary>Image produced by earlier API resolvers.</summary>
        public ImageSource CurrentImage { get; }
    }

    /// <summary>Resolves a post-Vanilla HUD image. Null preserves the current image.</summary>
    public delegate ImageSource UnitHudImageOverrideResolver(UnitHudImageOverrideContext context);

    /// <summary>Immutable image override definition.</summary>
    public sealed class UnitHudImageOverrideDefinition
    {
        /// <summary>Creates an image override definition.</summary>
        public UnitHudImageOverrideDefinition(string overrideId, UnitHudImageSlot slot, int priority = 0)
        {
            OverrideId = overrideId ?? string.Empty; Slot = slot; Priority = priority;
        }
        /// <summary>Stable owner-local ID.</summary>
        public string OverrideId { get; }
        /// <summary>Typed target slot.</summary>
        public UnitHudImageSlot Slot { get; }
        /// <summary>Ascending pipeline priority.</summary>
        public int Priority { get; }
    }

    /// <summary>Owner-bound shared Unit HUD service.</summary>
    public interface IUnitHudPresentationCapability
    {
        /// <summary>Registers one process-lifetime custom category.</summary>
        bool TryRegisterCategory(UnitHudCategoryDefinition definition, UnitHudCategoryMatcher matcher, out NativeCapabilityDiagnostic diagnostic);
        /// <summary>Registers one process-lifetime interaction observer.</summary>
        bool TryRegisterInteraction(string registrationId, UnitHudInteractionHandler handler, out NativeCapabilityDiagnostic diagnostic);
        /// <summary>Registers one deterministic post-Vanilla image override.</summary>
        bool TryRegisterImageOverride(UnitHudImageOverrideDefinition definition, UnitHudImageOverrideResolver resolver, out NativeCapabilityDiagnostic diagnostic);
        /// <summary>Registers recruitment handling for an existing category. The first implementation supports the European Archer base type.</summary>
        bool TryRegisterRecruitment(string categoryId, UnitHudRecruitmentHandler handler, out NativeCapabilityDiagnostic diagnostic);
        /// <summary>Completes an owner-bound ticket and releases its recruitment lock.</summary>
        bool TryCompleteRecruitment(UnitHudRecruitmentTicket ticket, int matchedCount, string reason, out NativeCapabilityDiagnostic diagnostic);
        /// <summary>Gets immutable snapshots of the current visible troop slots.</summary>
        IReadOnlyList<UnitHudSlotSnapshot> GetVisibleTroopSlots();
        /// <summary>Gets all custom categories represented by the current selected unit IDs.</summary>
        IReadOnlyList<UnitHudCategorySnapshot> GetSelectedCategories();
        /// <summary>Gets all ten validated Vanilla control-group memberships, or an empty list when unavailable.</summary>
        IReadOnlyList<UnitHudControlGroupSnapshot> GetControlGroups();
        /// <summary>Removes a positive one-based unit game ID from all native control groups.</summary>
        bool TryRemoveUnitFromControlGroups(
            int unitId,
            out int removedCount,
            out NativeCapabilityDiagnostic diagnostic);
        /// <summary>Requests a safe Unity-thread presentation refresh.</summary>
        void RequestRefresh();
    }
}
