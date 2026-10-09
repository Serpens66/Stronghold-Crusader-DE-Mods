using BugfixesAndQoL;
using BepInEx.Configuration;
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using R3;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Backends.NativeX64;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Input;
using SHCDESE.EventAPI.Network;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class FormationRuntime : IFormationCommandHandler
    {
        private const int ProtocolVersion = 6;
        private const int MapWidth = 800;
        private const int MaximumUnitCount = 10000;
        private const int MaximumTribeCount = 4500;
        private const int TribeRecordSize = 0x688;
        private const int TribeUnitCountOffset = 0x5C;
        private const int StandardSelectorRva = 0xE1D30;
        private const int AssassinSelectorRva = 0xE0970;
        private const int CommonGroupMoveRva = 0x118E00;
        private const int UnitMoveTargetRva = 0x196280;
        private const int GetGroupUnitIdRva = 0x119F90;
        private const int NativePathManagerRva = 0x60AD660;
        private const int NativeTribeManagerRva = 0x7CC6720;
        private const int MovementTargetAvailabilityRva = 0x3A11EA4;
        private const int ExpectedUnitMoveTargetAuditBytes = 14;
        private const int MaximumPreviewCandidates = 8192;
        private const int MaximumVanillaSelectorCandidates = 4001;
        private const int MinimumDragTileDistance = 2;

        private static readonly byte[] StandardSelectorPrefix =
        {
            0x48, 0x89, 0x5C, 0x24, 0x08,
            0x48, 0x89, 0x6C, 0x24, 0x18,
            0x48, 0x89, 0x74, 0x24, 0x20,
            0x89, 0x54, 0x24, 0x10, 0x57,
            0x41, 0x54, 0x41, 0x55, 0x41, 0x56, 0x41, 0x57,
            0x4C, 0x63, 0x1D, 0xE1, 0x49, 0xBE, 0x07,
            0x4C, 0x8B, 0xF1, 0x48, 0x63
        };

        private static readonly byte[] AssassinSelectorPrefix =
        {
            0x48, 0x89, 0x5C, 0x24, 0x08,
            0x48, 0x89, 0x6C, 0x24, 0x10,
            0x48, 0x89, 0x74, 0x24, 0x18,
            0x48, 0x89, 0x7C, 0x24, 0x20,
            0x41, 0x54, 0x41, 0x55, 0x41, 0x56, 0x41, 0x57,
            0x4C, 0x63, 0x1D, 0xA1, 0x5D, 0xBE, 0x07,
            0x4C, 0x8B, 0xF1,
            0x48, 0x63, 0x81, 0x6C, 0x5F, 0x15, 0x00,
            0x45, 0x8B, 0xF9
        };

        private static readonly byte[] CommonGroupMovePrefix =
        {
            0x48, 0x89, 0x5C, 0x24, 0x08,
            0x48, 0x89, 0x6C, 0x24, 0x10,
            0x48, 0x89, 0x74, 0x24, 0x18,
            0x57, 0x41, 0x54, 0x41, 0x55, 0x41, 0x56, 0x41, 0x57,
            0x48, 0x83, 0xEC, 0x30,
            0x48, 0x63, 0xF2, 0x45, 0x33, 0xED, 0x8B, 0xD6,
            0x45, 0x8B, 0xF1, 0x45, 0x8B, 0xF8, 0x48, 0x8B, 0xE9,
            0x41, 0x8B, 0xDD
        };

        private static readonly byte[] UnitMoveTargetPrefix =
        {
            0x48, 0x89, 0x5C, 0x24, 0x20,
            0x55, 0x56, 0x57, 0x41, 0x54, 0x41, 0x55, 0x41, 0x56,
            0x41, 0x57, 0x48, 0x83, 0xEC, 0x30,
            0x48, 0x63, 0xF2, 0x45, 0x33, 0xD2,
            0x48, 0x69, 0xFE, 0x90, 0x04, 0x00, 0x00,
            0x4D, 0x63, 0xF0,
            0x48, 0x8D, 0x15, 0x55, 0x9D, 0xE6, 0xFF,
            0x48, 0x03, 0xF9
        };

        private static readonly BindingFlags InstanceFields =
            BindingFlags.Instance | BindingFlags.NonPublic;

        private readonly ManualLogSource log;
        private readonly CrusaderLibraryLoadContext libraryContext;
        private readonly ConfigEntry<FormationKind> formationConfig;
        private readonly ConfigEntry<int> densityConfig;
        private readonly ConfigEntry<RangedPlacementMode> placementModeConfig;
        private readonly IFormationPresentation menuViewModel;
        private readonly object stateSync = new object();
        private readonly HashSet<string> loggedTargetRejections =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly IUnitCommandSettings settings;
        private readonly UnitCommandPathRuntime commandRuntime;
        private NativeGroundMoveFeedbackReader groundFeedbackReader;
        private long nativeFeedbackGeneration;
        private long nativeFeedbackRunGeneration;
        private bool nativeFeedbackRunActive;
        private bool startupConfirmed;
        internal bool Enabled => initialized && !failed && settings.EnableMod && settings.EnableMoveFormationEnhancements;
        private int dispatchDepth;
        internal bool IsDispatching { get { lock (stateSync) return dispatchDepth > 0; } }

        private LargeMoveTargetMarkerRenderer markerRenderer;
        private APIShared.Internal.NativeTroopCommandModeReader commandModeReader;
        private Hook engineRunHook;
        private Hook cameraUpdateHook;
        private EngineRunDelegate engineRunOriginal;
        private CameraUpdateDelegate cameraUpdateOriginal;
        private IDisposable keyDownSubscription;
        private IDisposable keyHeldSubscription;
        private IDisposable keyUpSubscription;
        private IDisposable packetSubscription;
        private R3PacketEventHook<FormationOrderPacket> packetHook;
        private FieldInfo leftMouseStateField;
        private FieldInfo rightMouseDownField;
        private FieldInfo rightMouseUpField;
        private FieldInfo mouseStateReadField;
        private FieldInfo mouseUpPendingField;
        private IntPtr nativeTribeManager;
        private IntPtr nativePathManager;
        private byte* movementTargetAvailability;
        private GetGroupUnitIdDelegate getGroupUnitId;
        private MethodInfo sendChorePayloadMethod;
        private ActiveDrag drag;
        private PendingFormationCommand pendingCommand;
        private ActiveFormationCommand activeCommand;
        private ActiveFormationCommand commonGroupCommand;
        private UnitAssignmentFrame unitAssignmentFrame;
        private UnitFallbackAttempt unitFallbackAttempt;
        private ReleaseConsumptionWatch releaseConsumptionWatch;
        private int nextOperationId;
        private int lastWheelFrame = -1;
        private int mainThreadId;
        private bool initialized;
        private bool failed;
    }
}
