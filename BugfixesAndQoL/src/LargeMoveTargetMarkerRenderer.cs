using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace BugfixesAndQoL
{
    internal sealed unsafe class LargeMoveTargetMarkerRenderer
    {
        internal const int VisibleTileHookRva = 0x436DE;
        internal const int VisibleTileHookLength = 9;
        internal const int VisibleTileHookEndRva = 0x436E7;
        internal const int SpriteBuilderRva = 0x1A13C0;
        internal const int FirstSyntheticIdentity = 250;
        internal const int NativeMode8IdentityCapacity = 4250;
        internal const int MaximumSyntheticMarkers =
            NativeMode8IdentityCapacity - FirstSyntheticIdentity;

        private const int DrawManagerRva = 0xA98820;
        private const int CurrentTerrainHeightRva = 0x42D8D8;
        private const int DetailedTerrainRenderingRva = 0x60AD43C;
        private const int AnimationFrameRva = 0x60AD544;
        private const int TileFlagsRva = 0x48F71B0;
        private const int TileBuildingIdRva = 0x4B6AA50;
        private const int BuildingManagerRva = 0x64CCBB0;
        private const int BuildingTypeRva = 0x64CCCDE;
        private const int BuildingRecordSize = 0x32C;
        private const int BuildingHeightHelperRva = 0xC07C0;
        private const int NativeTileCount = 320000;
        private const uint BuildingTileFlag = 0x100;
        private const uint ElevatedBuildingTileFlag = 0x10000000;

        // This signature begins at the visible-tile list-head read in FUN_180041D60.
        // RBX is the current visible tile; the nine-byte instruction only defines EDI.
        private const string VisibleTileTraversalPattern =
            "4C 8D 0D D2 B6 01 04 41 0F B7 BC 59 80 EF 75 00 85 FF 0F 84 81 02 00 00 " +
            "83 3D 46 9D 06 06 00 8B 35 DC A1 3E 00";

        // FBCB...31E2 FUN_1801A13C0 entry. A normal function-entry delegate is used;
        // no instructions in this function are replaced by this feature.
        private const string SpriteBuilderPattern =
            "48 89 5C 24 08 44 89 4C 24 20 44 89 44 24 18 89 54 24 10 55 56 57 41 54 " +
            "41 55 41 56 41 57 48 81 EC C0 00 00 00 8B 84 24 60 01 00 00 48 8B D9 8B " +
            "AC 24 20 01 00 00 85 C0 44 8B F0 4D 63 F8 41 F7 D6 4C 63 EA";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SpriteBuilderDelegate(
            IntPtr manager,
            int identity,
            int tileId,
            int secondaryTile,
            int mode,
            int category,
            int spriteId,
            int horizontalOffset,
            int unused,
            int player,
            int layer,
            int verticalOffset,
            int flagsHigh,
            int owner);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int BuildingHeightDelegate(IntPtr buildingManager, int buildingId);

        private readonly ManualLogSource log;
        private readonly Func<bool> featureEnabled;
        private readonly object stateRoot = new object();
        private readonly HookHandle<X64InlineHook> visibleTileHook =
            new HookHandle<X64InlineHook>();
        private volatile Dictionary<int, int> markerIdentityByTile =
            new Dictionary<int, int>();
        private readonly Dictionary<int, int> stableIdentityByTile =
            new Dictionary<int, int>();
        private readonly HashSet<int> previewTiles = new HashSet<int>();
        private readonly HashSet<int> previewRequestBuffer = new HashSet<int>();
        private readonly HashSet<int> reservedIdentities = new HashSet<int>();
        private readonly Stack<int> recycledIdentities = new Stack<int>();
        private int nextIdentity = FirstSyntheticIdentity;
        private HookTransaction transaction;
        private SpriteBuilderDelegate spriteBuilder;
        private BuildingHeightDelegate getBuildingHeight;
        private IntPtr libraryHandle;
        private volatile bool installed;
        private volatile bool failed;
        private volatile bool renderingActive;
        private bool publicationDirty;
        private bool failureLogged;

        public LargeMoveTargetMarkerRenderer(ManualLogSource log, Func<bool> featureEnabled)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.featureEnabled = featureEnabled ?? throw new ArgumentNullException(nameof(featureEnabled));
        }

        public bool ReplacementAvailable => installed && !failed && visibleTileHook.Success;

        public bool ReplacementActive => ReplacementAvailable && renderingActive;

        public void Install(CrusaderLibraryLoadContext context, bool fixedLayoutHashValidated)
        {
            if (installed)
                return;
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (!fixedLayoutHashValidated)
                throw new InvalidOperationException(
                    "Large Move marker replacement requires the validated FBCB9319 native layout.");

            ReadOnlySpan<byte> memory = context.Memory;
            Shared.NativeResolution traversal = Shared.NativePatternResolver.ResolveUnique(
                memory,
                VisibleTileTraversalPattern,
                VisibleTileHookRva - 7,
                referenceHashMatches: true,
                name: "visible-tile overlay traversal",
                log: null);
            Shared.NativeResolution builder = Shared.NativePatternResolver.ResolveUnique(
                memory,
                SpriteBuilderPattern,
                SpriteBuilderRva,
                referenceHashMatches: true,
                name: "native sprite builder",
                log: null);
            if (traversal.Rva + 7 != VisibleTileHookRva || builder.Rva != SpriteBuilderRva ||
                VisibleTileHookRva + VisibleTileHookLength != VisibleTileHookEndRva ||
                NativeMode8IdentityCapacity !=
                    (0x1025580 - 0x203A16 * sizeof(long)) / sizeof(long))
            {
                throw new InvalidOperationException("Large Move marker native contract mismatch.");
            }

            libraryHandle = context.ModuleHandle;
            spriteBuilder = Marshal.GetDelegateForFunctionPointer<SpriteBuilderDelegate>(
                IntPtr.Add(libraryHandle, SpriteBuilderRva));
            getBuildingHeight = Marshal.GetDelegateForFunctionPointer<BuildingHeightDelegate>(
                IntPtr.Add(libraryHandle, BuildingHeightHelperRva));

            HookTransaction candidate =
                BugfixesHookInfrastructure.CreateOwnedTransaction(context.Region);
            try
            {
                // The callback runs before the original MOVZX. RedBird preserves every register;
                // the relocated MOVZX restores EDI and the following TEST recreates all live flags.
                BugfixesHookInfrastructure.AddContextHook(
                    candidate,
                    visibleTileHook,
                    unchecked((ulong)(libraryHandle + VisibleTileHookRva).ToInt64()),
                    RenderVisibleLargeMoveTarget,
                    X64SmartCPUContextRegs.All,
                    hookSize: VisibleTileHookLength,
                    errorMode: CallbackErrorMode.LogAndContinue,
                    placement: OverwrittenInstructionPlacement.AfterCallback);
                CommitResult result = candidate.Commit();
                if (!result.IsCompleteSuccess || !visibleTileHook.Success ||
                    !visibleTileHook.IsInstalled)
                {
                    throw new InvalidOperationException(
                        "Visible-tile marker hook reported no success.");
                }

                transaction = candidate;
                installed = true;
                renderingActive = true;
                SetRenderingActiveCore(false);
            }
            catch
            {
                installed = false;
                renderingActive = false;
                transaction = null;
                candidate.Dispose();
                throw;
            }
        }

        public void AddMarkerTile(int tileId)
        {
            lock (stateRoot)
            {
                if (!ReplacementAvailable)
                    return;
                try
                {
                    if ((uint)tileId >= NativeTileCount)
                        throw new InvalidOperationException(
                            $"Active Move marker tile {tileId} is outside the native tile array.");
                    if (stableIdentityByTile.ContainsKey(tileId))
                        return;
                    if (stableIdentityByTile.Count >= MaximumSyntheticMarkers)
                        throw new InvalidOperationException(
                            $"The validated native capacity of {MaximumSyntheticMarkers} markers is exhausted.");
                    int identity = recycledIdentities.Count != 0
                        ? recycledIdentities.Pop()
                        : nextIdentity++;
                    if (identity >= NativeMode8IdentityCapacity)
                        throw new InvalidOperationException(
                            "The native Move marker identity range is exhausted.");
                    stableIdentityByTile.Add(tileId, identity);
                    reservedIdentities.Add(identity);
                    publicationDirty = true;
                }
                catch (Exception exception)
                {
                    FailOpen(exception);
                }
            }
        }

        public void RemoveMarkerTile(int tileId)
        {
            lock (stateRoot)
            {
                if (!stableIdentityByTile.TryGetValue(tileId, out int identity))
                    return;
                stableIdentityByTile.Remove(tileId);
                reservedIdentities.Remove(identity);
                recycledIdentities.Push(identity);
                publicationDirty = true;
            }
        }

        public void PublishMarkerTiles()
        {
            lock (stateRoot)
            {
                if (!ReplacementAvailable)
                    return;
                PublishMarkerTilesCore();
            }
        }

        private void PublishMarkerTilesCore()
        {
            if (!publicationDirty)
                return;
            try
            {
                // Render callbacks only read the published immutable snapshot.
                var published = new Dictionary<int, int>(stableIdentityByTile);
                int previewIdentity = NativeMode8IdentityCapacity - 1;
                foreach (int tileId in previewTiles)
                {
                    if (published.ContainsKey(tileId))
                        continue;
                    while (previewIdentity >= FirstSyntheticIdentity &&
                        reservedIdentities.Contains(previewIdentity))
                        previewIdentity--;
                    if (previewIdentity < FirstSyntheticIdentity)
                        break;
                    published.Add(tileId, previewIdentity--);
                }

                markerIdentityByTile = published;
                publicationDirty = false;
                SetRenderingActiveCore(published.Count != 0);
            }
            catch (Exception exception)
            {
                FailOpen(exception);
            }
        }

        public void SetPreviewMarkerTiles(IEnumerable<int> tileIds)
        {
            lock (stateRoot)
            {
                previewRequestBuffer.Clear();
                if (ReplacementAvailable && tileIds != null)
                {
                    foreach (int tileId in tileIds)
                    {
                        if ((uint)tileId < NativeTileCount)
                            previewRequestBuffer.Add(tileId);
                    }
                }
                if (previewTiles.SetEquals(previewRequestBuffer))
                    return;
                previewTiles.Clear();
                previewTiles.UnionWith(previewRequestBuffer);
                publicationDirty = true;
                PublishMarkerTilesCore();
            }
        }

        public void ClearPreviewMarkerTiles()
        {
            lock (stateRoot)
            {
                if (previewTiles.Count == 0)
                    return;
                previewTiles.Clear();
                publicationDirty = true;
                PublishMarkerTilesCore();
            }
        }

        private void SetRenderingActiveCore(bool shouldBeActive)
        {
            if (renderingActive == shouldBeActive)
                return;
            if (shouldBeActive)
            {
                visibleTileHook.Hook.Enable();
                if (!visibleTileHook.IsInstalled)
                    throw new InvalidOperationException(
                        "Visible-tile marker hook could not be enabled.");
                renderingActive = true;
                return;
            }

            visibleTileHook.Hook.Disable();
            if (visibleTileHook.IsInstalled)
                throw new InvalidOperationException(
                    "Visible-tile marker hook could not be disabled.");
            renderingActive = false;
        }

        private void RenderVisibleLargeMoveTarget(NativePointer<X64SmartCPUContext> context)
        {
            if (!renderingActive || context.Pointer == null)
                return;

            Dictionary<int, int> markers = markerIdentityByTile;
            if (markers.Count == 0 || !featureEnabled())
                return;
            try
            {
                X64SmartCPUContext* registers = context.Pointer;
                int tileId = unchecked((int)(uint)registers->RBX);
                if ((uint)tileId >= NativeTileCount ||
                    !markers.TryGetValue(tileId, out int identity))
                {
                    return;
                }

                int animation = Marshal.ReadInt32(libraryHandle + AnimationFrameRva) - 1;
                animation %= 16;
                if (animation < 0)
                    animation += 16;
                int frame = animation < 8 ? animation : 15 - animation;
                int terrainHeight = Marshal.ReadInt32(libraryHandle + CurrentTerrainHeightRva);
                if (Marshal.ReadInt32(libraryHandle + DetailedTerrainRenderingRva) != 0 &&
                    *(long*)(registers->RSP + 0x80) == 0)
                {
                    terrainHeight += GetStructureHeightAdjustment(tileId);
                }

                spriteBuilder(
                    libraryHandle + DrawManagerRva,
                    identity,
                    tileId,
                    0,
                    8,
                    0x6B,
                    0x52 + frame,
                    0,
                    0,
                    0,
                    0xC,
                    6 - terrainHeight,
                    0,
                    0);
            }
            catch (Exception exception)
            {
                FailOpen(exception);
            }
        }

        private int GetStructureHeightAdjustment(int tileId)
        {
            uint flags = unchecked((uint)Marshal.ReadInt32(
                libraryHandle + TileFlagsRva + tileId * sizeof(uint)));
            int buildingId = Marshal.ReadInt16(
                libraryHandle + TileBuildingIdRva + tileId * sizeof(short));
            if ((flags & ElevatedBuildingTileFlag) != 0)
                return getBuildingHeight(libraryHandle + BuildingManagerRva, buildingId);
            if ((flags & BuildingTileFlag) == 0)
                return 0;
            if (buildingId == 0)
                return 4;
            int buildingType = Marshal.ReadInt16(
                libraryHandle + BuildingTypeRva + buildingId * BuildingRecordSize);
            return buildingType == 0x2F ? 6 : 20;
        }

        private void FailOpen(Exception exception)
        {
            lock (stateRoot)
            {
                failed = true;
                renderingActive = false;
                markerIdentityByTile = new Dictionary<int, int>();
                stableIdentityByTile.Clear();
                previewTiles.Clear();
                previewRequestBuffer.Clear();
                reservedIdentities.Clear();
                recycledIdentities.Clear();
                publicationDirty = false;
                if (failureLogged)
                    return;
                failureLogged = true;
            }
            Shared.DebugLogHelper.LogError(
                log,
                $"MOVE_TARGET_MARKER_RENDER_FAIL_OPEN: Vanilla markers retained; {exception}");
        }
    }
}
