using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API.LowLevel;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace BugfixesAndQoL
{
    internal sealed unsafe class LargeMoveTargetMarkerRenderer
    {
        internal const int ResetDrawListRva = 0x41D10;
        internal const int ResetDrawListLength = 80;
        internal const int VisibleTileRendererRva = 0x41D60;
        internal const int VisibleTileHookRva = 0x436DE;
        internal const int VisibleTileMinimumHookLength = 9;
        internal const int ExpectedVisibleTileDisplacedBytes = 17;
        internal const int VisibleTileHookEndRva = 0x436EF;
        internal const int SpriteBuilderRva = 0x1A13C0;

        private const int DrawManagerRva = 0xA98820;
        private const int DrawRecordBaseOffset = 0x6206F0;
        private const int DrawRecordSize = 0x1C;
        private const int DrawRecordNextOffset = 0x18;
        private const int TileDrawHeadRva = 0x47BDD30;
        private const int CurrentTerrainHeightRva = 0x42D8D8;
        private const int DetailedTerrainRenderingRva = 0x60AD43C;
        private const int AnimationFrameRva = 0x60AD544;
        private const int TileFlagsRva = 0x48F71B0;
        private const int TileBuildingIdRva = 0x4B6AA50;
        private const int BuildingManagerRva = 0x64CCBB0;
        private const int BuildingTypeRva = 0x64CCCDE;
        private const int BuildingRecordSize = 0x32C;
        private const int BuildingHeightHelperRva = 0xC07C0;
        private const uint BuildingTileFlag = 0x100;
        private const uint ElevatedBuildingTileFlag = 0x10000000;

        private const string ResetDrawListPattern =
            "41 B8 01 00 00 00 44 39 81 48 22 62 00 7E 39 48 8D 91 24 07 62 00 " +
            "45 33 C9 4C 8D 15 00 C0 77 04 48 63 42 F8 48 8D 52 1C 41 FF C0 66 45 89 0C 42 " +
            "44 89 4A E4 44 3B 81 48 22 62 00 7C E3 C7 81 48 22 62 00 01 00 00 00 C3 " +
            "44 89 81 48 22 62 00 C3";

        private const string VisibleTileTraversalPattern =
            "4C 8D 0D D2 B6 01 04 41 0F B7 BC 59 80 EF 75 00 85 FF 0F 84 81 02 00 00 " +
            "83 3D 46 9D 06 06 00 8B 35 DC A1 3E 00";

        private const string SpriteBuilderPattern =
            "48 89 5C 24 08 44 89 4C 24 20 44 89 44 24 18 89 54 24 10 55 56 57 41 54 " +
            "41 55 41 56 41 57 48 81 EC C0 00 00 00 8B 84 24 60 01 00 00 48 8B D9 8B " +
            "AC 24 20 01 00 00 85 C0 44 8B F0 4D 63 F8 41 F7 D6 4C 63 EA";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ResetDrawListDelegate(IntPtr drawManager);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SpriteBuilderDelegate(
            IntPtr manager, int identity, int tileId, int secondaryTile, int mode,
            int category, int spriteId, int horizontalOffset, int unused, int player,
            int layer, int verticalOffset, int flagsHigh, int owner);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int BuildingHeightDelegate(IntPtr buildingManager, int buildingId);

        private static readonly Dictionary<int, int> EmptyPreview = new Dictionary<int, int>();
        private readonly ManualLogSource log;
        private readonly Func<bool> featureEnabled;
        private readonly object stateRoot = new object();
        private readonly HookHandle<X64InlineHook> visibleTileHook = new HookHandle<X64InlineHook>();
        private readonly DetourHandle<ResetDrawListDelegate> resetDrawListHook =
            new DetourHandle<ResetDrawListDelegate>();
        private readonly LargeMoveTargetOverflowBuffer firstBuffer = new LargeMoveTargetOverflowBuffer();
        private readonly LargeMoveTargetOverflowBuffer secondBuffer = new LargeMoveTargetOverflowBuffer();
        private readonly HashSet<int> previewRequestBuffer = new HashSet<int>();
        private volatile LargeMoveTargetOverflowBuffer publishedOverflow;
        private volatile Dictionary<int, int> publishedPreview = EmptyPreview;
        private LargeMoveTargetOverflowBuffer stagingBuffer;
        private HookTransaction transaction;
        private SpriteBuilderDelegate spriteBuilder;
        private BuildingHeightDelegate getBuildingHeight;
        private IntPtr libraryHandle;
        private volatile bool installed;
        private volatile bool failed;
        private volatile bool renderingActive;
        private bool overlayPassActive;
        private bool failureLogged;

        public LargeMoveTargetMarkerRenderer(ManualLogSource log, Func<bool> featureEnabled)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.featureEnabled = featureEnabled ?? throw new ArgumentNullException(nameof(featureEnabled));
        }

        public bool ReplacementAvailable =>
            installed && !failed && visibleTileHook.Success && visibleTileHook.IsInstalled &&
            resetDrawListHook.Success;

        public void Install(CrusaderLibraryLoadContext context, bool fixedLayoutHashValidated)
        {
            if (installed)
                return;
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (!fixedLayoutHashValidated)
                throw new InvalidOperationException(
                    "Large Move marker overflow requires the validated FBCB9319 native layout.");

            ReadOnlySpan<byte> memory = context.Memory;
            Shared.NativeResolution reset = Shared.NativePatternResolver.ResolveUnique(
                memory, ResetDrawListPattern, ResetDrawListRva, true,
                "Vanilla overlay draw-list reset", null);
            Shared.NativeResolution traversal = Shared.NativePatternResolver.ResolveUnique(
                memory, VisibleTileTraversalPattern, VisibleTileHookRva - 7, true,
                "visible-tile overlay traversal", null);
            Shared.NativeResolution builder = Shared.NativePatternResolver.ResolveUnique(
                memory, SpriteBuilderPattern, SpriteBuilderRva, true,
                "native sprite builder", null);
            if (reset.Rva != ResetDrawListRva || traversal.Rva + 7 != VisibleTileHookRva ||
                builder.Rva != SpriteBuilderRva ||
                VisibleTileHookRva + ExpectedVisibleTileDisplacedBytes != VisibleTileHookEndRva ||
                LargeMoveTargetOverflowModel.NativeMode8IdentityCapacity !=
                    (0x1025580 - 0x203A16 * sizeof(long)) / sizeof(long))
            {
                throw new InvalidOperationException("Large Move marker native contract mismatch.");
            }

            libraryHandle = context.ModuleHandle;
            spriteBuilder = Marshal.GetDelegateForFunctionPointer<SpriteBuilderDelegate>(
                IntPtr.Add(libraryHandle, SpriteBuilderRva));
            getBuildingHeight = Marshal.GetDelegateForFunctionPointer<BuildingHeightDelegate>(
                IntPtr.Add(libraryHandle, BuildingHeightHelperRva));

            HookTransaction candidate = BugfixesHookInfrastructure.CreateOwnedTransaction(context.Region);
            try
            {
                candidate.AddDetour(
                    resetDrawListHook,
                    HookTarget.FromAddress(unchecked((ulong)(libraryHandle + ResetDrawListRva).ToInt64())),
                    ResetDrawList);
                BugfixesHookInfrastructure.AddContextHook(
                    candidate, visibleTileHook,
                    unchecked((ulong)(libraryHandle + VisibleTileHookRva).ToInt64()),
                    RenderVisibleLargeMoveTarget, X64SmartCPUContextRegs.All,
                    VisibleTileMinimumHookLength, CallbackErrorMode.LogAndContinue,
                    OverwrittenInstructionPlacement.AfterCallback);
                CommitResult result = candidate.Commit();
                if (!result.IsCompleteSuccess || !visibleTileHook.Success ||
                    !visibleTileHook.IsInstalled || !resetDrawListHook.Success ||
                    visibleTileHook.Hook.DisplacedByteCount != ExpectedVisibleTileDisplacedBytes)
                {
                    throw new InvalidOperationException(
                        "Large Move marker overflow hooks failed or displaced an unexpected span: " +
                        $"result={result}, visible={visibleTileHook.Hook?.DisplacedByteCount}.");
                }

                Shared.DebugLogHelper.LogInfo(
                    log,
                    "Large Move marker renderer ready: " +
                    $"reset=0x{ResetDrawListRva:X}/span{ResetDrawListLength}, " +
                    $"visible=0x{VisibleTileHookRva:X}/span{ExpectedVisibleTileDisplacedBytes}.");
                transaction = candidate;
                installed = true;
                renderingActive = false;
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

        public void BeginOverlayPass(bool enabled)
        {
            lock (stateRoot)
            {
                overlayPassActive = enabled && ReplacementAvailable && publishedPreview.Count == 0;
                stagingBuffer = ReferenceEquals(publishedOverflow, firstBuffer) ? secondBuffer : firstBuffer;
                stagingBuffer.Clear();
            }
        }

        public bool TryAddOverflowMarker(
            IntPtr drawManager,
            int category, int spriteId, int layer, int verticalOffset, int tileId, int flags)
        {
            if (!overlayPassActive || failed || publishedPreview.Count != 0)
                return true;
            if (NativeContainsDuplicate(drawManager, tileId, category, spriteId))
                return true;
            LargeMoveTargetOverflowBuffer buffer = stagingBuffer;
            if (buffer != null &&
                buffer.TryAdd(category, spriteId, layer, verticalOffset, tileId, flags))
                return true;
            FailOpen(new InvalidOperationException(
                "The validated Move marker overflow capacity was exceeded."));
            return false;
        }

        private bool NativeContainsDuplicate(
            IntPtr drawManager,
            int tileId,
            int category,
            int spriteId)
        {
            if ((uint)tileId >= LargeMoveTargetOverflowModel.NativeTileCount)
                return false;
            int recordIndex = unchecked((ushort)Marshal.ReadInt16(
                libraryHandle + TileDrawHeadRva + tileId * sizeof(ushort)));
            for (int visited = 0;
                recordIndex > 0 &&
                recordIndex < LargeMoveTargetOverflowModel.NativeDrawCapacity &&
                visited < LargeMoveTargetOverflowModel.NativeUsableDrawRecords;
                visited++)
            {
                IntPtr record = IntPtr.Add(
                    drawManager,
                    DrawRecordBaseOffset + recordIndex * DrawRecordSize);
                if (Marshal.ReadInt32(record) == category &&
                    Marshal.ReadInt32(IntPtr.Add(record, sizeof(int))) == spriteId)
                {
                    return true;
                }
                recordIndex = Marshal.ReadInt32(IntPtr.Add(record, DrawRecordNextOffset));
            }
            return false;
        }

        public void EndOverlayPass(bool completed)
        {
            lock (stateRoot)
            {
                overlayPassActive = false;
                if (!completed || failed || publishedPreview.Count != 0 ||
                    stagingBuffer == null || stagingBuffer.Count == 0)
                {
                    publishedOverflow = null;
                    return;
                }
                publishedOverflow = stagingBuffer;
                SetRenderingActiveCore(true);
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
                        if ((uint)tileId < LargeMoveTargetOverflowModel.NativeTileCount)
                            previewRequestBuffer.Add(tileId);
                    }
                }
                if (PreviewEqualsRequest())
                    return;
                if (previewRequestBuffer.Count == 0)
                {
                    publishedPreview = EmptyPreview;
                    publishedOverflow = null;
                    SetRenderingActiveCore(false);
                    return;
                }

                var preview = new Dictionary<int, int>(previewRequestBuffer.Count);
                int identity = LargeMoveTargetOverflowModel.FirstSyntheticIdentity;
                foreach (int tileId in previewRequestBuffer)
                {
                    if (identity >= LargeMoveTargetOverflowModel.NativeMode8IdentityCapacity)
                        break;
                    preview.Add(tileId, identity++);
                }
                publishedOverflow = null;
                publishedPreview = preview;
                SetRenderingActiveCore(true);
            }
        }

        public void ClearPreviewMarkerTiles()
        {
            lock (stateRoot)
            {
                if (publishedPreview.Count == 0)
                    return;
                publishedPreview = EmptyPreview;
                publishedOverflow = null;
                SetRenderingActiveCore(false);
            }
        }

        public void Reset()
        {
            lock (stateRoot)
            {
                overlayPassActive = false;
                stagingBuffer = null;
                firstBuffer.Clear();
                secondBuffer.Clear();
                publishedOverflow = null;
                publishedPreview = EmptyPreview;
                previewRequestBuffer.Clear();
                if (ReplacementAvailable)
                    SetRenderingActiveCore(false);
            }
        }

        private bool PreviewEqualsRequest()
        {
            Dictionary<int, int> preview = publishedPreview;
            if (preview.Count != previewRequestBuffer.Count)
                return false;
            foreach (int tileId in previewRequestBuffer)
            {
                if (!preview.ContainsKey(tileId))
                    return false;
            }
            return true;
        }

        private void ResetDrawList(IntPtr drawManager)
        {
            try
            {
                resetDrawListHook.Original(drawManager);
            }
            finally
            {
                try
                {
                    OnVanillaDrawListReset();
                }
                catch (Exception exception)
                {
                    FailOpen(exception);
                }
            }
        }

        private void OnVanillaDrawListReset()
        {
            lock (stateRoot)
            {
                if (failed)
                    return;
                bool renderedOverflow = publishedOverflow != null && publishedOverflow.Count != 0;
                publishedOverflow = null;
                if (publishedPreview.Count == 0 && !renderedOverflow && renderingActive)
                    SetRenderingActiveCore(false);
            }
        }

        private void SetRenderingActiveCore(bool shouldBeActive)
        {
            if (!visibleTileHook.IsInstalled)
                throw new InvalidOperationException("Visible-tile marker hook is no longer installed.");
            renderingActive = shouldBeActive;
        }

        private void RenderVisibleLargeMoveTarget(NativePointer<X64SmartCPUContext> context)
        {
            if (!renderingActive || context.Pointer == null)
                return;
            Dictionary<int, int> preview = publishedPreview;
            LargeMoveTargetOverflowBuffer overflow = publishedOverflow;
            if ((preview.Count == 0 && overflow == null) || !featureEnabled())
                return;
            try
            {
                X64SmartCPUContext* registers = context.Pointer;
                int tileId = unchecked((int)(uint)registers->RBX);
                if ((uint)tileId >= LargeMoveTargetOverflowModel.NativeTileCount)
                    return;

                if (preview.Count != 0)
                {
                    if (preview.TryGetValue(tileId, out int previewIdentity))
                        DrawPreview(registers, tileId, previewIdentity);
                    return;
                }

                if (overflow == null)
                    return;
                int recordIndex = overflow.GetHead(tileId);
                if (recordIndex == 0)
                    return;

                int terrainHeight = GetTerrainHeight(registers, tileId);
                while (recordIndex != 0)
                {
                    LargeMoveTargetOverflowRecord record = overflow.GetRecord(recordIndex);
                    spriteBuilder(
                        libraryHandle + DrawManagerRva,
                        LargeMoveTargetOverflowModel.FirstSyntheticIdentity + recordIndex - 1,
                        tileId, 0, 8, record.Category, record.SpriteId, 0, 0, 0,
                        record.Layer, record.VerticalOffset - terrainHeight,
                        record.Flags >> 16, 0);
                    recordIndex = record.Next;
                }
            }
            catch (Exception exception)
            {
                FailOpen(exception);
            }
        }

        private void DrawPreview(X64SmartCPUContext* registers, int tileId, int identity)
        {
            int animation = Marshal.ReadInt32(libraryHandle + AnimationFrameRva) - 1;
            animation %= 16;
            if (animation < 0)
                animation += 16;
            int frame = animation < 8 ? animation : 15 - animation;
            spriteBuilder(
                libraryHandle + DrawManagerRva, identity, tileId, 0, 8,
                0x6B, 0x52 + frame, 0, 0, 0, 0xC,
                6 - GetTerrainHeight(registers, tileId), 0, 0);
        }

        private int GetTerrainHeight(X64SmartCPUContext* registers, int tileId)
        {
            int terrainHeight = Marshal.ReadInt32(libraryHandle + CurrentTerrainHeightRva);
            if (Marshal.ReadInt32(libraryHandle + DetailedTerrainRenderingRva) != 0 &&
                *(long*)(registers->RSP + 0x80) == 0)
                terrainHeight += GetStructureHeightAdjustment(tileId);
            return terrainHeight;
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
                FailOpenCore(exception);
        }

        private void FailOpenCore(Exception exception)
        {
            failed = true;
            overlayPassActive = false;
            stagingBuffer = null;
            publishedOverflow = null;
            publishedPreview = EmptyPreview;
            previewRequestBuffer.Clear();
            renderingActive = false;
            if (failureLogged)
                return;
            failureLogged = true;
            Shared.DebugLogHelper.LogError(
                log,
                $"MOVE_TARGET_MARKER_RENDER_FAIL_OPEN: Vanilla markers retained; {exception}");
        }
    }
}
