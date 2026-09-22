using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Backends.NativeX64;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API.LowLevel;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace FormationTest
{
    internal sealed unsafe class FormationPreviewMarkerRenderer
    {
        internal const int ResetDrawListRva = 0x41D10;
        internal const int ResetDrawListLength = 80;
        internal const int VisibleTileRendererRva = 0x41D60;
        internal const int VisibleTileHookRva = 0x436DE;
        internal const int VisibleTileMinimumHookLength = 9;
        internal const int ExpectedVisibleTileDisplacedBytes = 17;
        internal const int SpriteBuilderRva = 0x1A13C0;

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

        private static readonly Dictionary<int, int> EmptyPreview =
            new Dictionary<int, int>();

        private readonly ManualLogSource log;
        private readonly Action previewFailed;
        private readonly object stateRoot = new object();
        private readonly HookHandle<X64InlineHook> visibleTileHook =
            new HookHandle<X64InlineHook>();
        private readonly DetourHandle<ResetDrawListDelegate> resetDrawListHook =
            new DetourHandle<ResetDrawListDelegate>();

        private volatile Dictionary<int, int> publishedPreview = EmptyPreview;
        private HookTransaction transaction;
        private SpriteBuilderDelegate spriteBuilder;
        private BuildingHeightDelegate getBuildingHeight;
        private IntPtr libraryHandle;
        private volatile bool installed;
        private volatile bool failed;
        private volatile bool renderingActive;
        private bool published;
        private bool failureLogged;

        internal FormationPreviewMarkerRenderer(ManualLogSource log, Action previewFailed)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.previewFailed = previewFailed ?? throw new ArgumentNullException(nameof(previewFailed));
        }

        internal bool ReplacementAvailable =>
            installed && !failed && visibleTileHook.Success && visibleTileHook.IsInstalled &&
            resetDrawListHook.Success;

        internal void Install(CrusaderLibraryLoadContext context)
        {
            if (installed)
                return;
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            ReadOnlySpan<byte> memory = context.Memory;
            Shared.NativeResolution reset = Shared.NativePatternResolver.ResolveUnique(
                memory, ResetDrawListPattern, ResetDrawListRva, true,
                "Formation preview draw-list reset", log);
            Shared.NativeResolution traversal = Shared.NativePatternResolver.ResolveUnique(
                memory, VisibleTileTraversalPattern, VisibleTileHookRva - 7, true,
                "Formation preview visible-tile traversal", log);
            Shared.NativeResolution builder = Shared.NativePatternResolver.ResolveUnique(
                memory, SpriteBuilderPattern, SpriteBuilderRva, true,
                "Formation preview native sprite builder", log);
            if (reset.Rva != ResetDrawListRva || traversal.Rva + 7 != VisibleTileHookRva ||
                builder.Rva != SpriteBuilderRva ||
                FormationPreviewMarkerModel.MaximumMarkers != 4000)
            {
                throw new InvalidOperationException(
                    "Formation preview native marker contract mismatch.");
            }

            libraryHandle = context.ModuleHandle;
            spriteBuilder = Marshal.GetDelegateForFunctionPointer<SpriteBuilderDelegate>(
                IntPtr.Add(libraryHandle, SpriteBuilderRva));
            getBuildingHeight = Marshal.GetDelegateForFunctionPointer<BuildingHeightDelegate>(
                IntPtr.Add(libraryHandle, BuildingHeightHelperRva));

            HookTransaction candidate = new HookTransaction(
                context.Region,
                SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                new HookTransactionOptions
                {
                    FailureMode = TransactionFailureMode.RollbackAndThrow,
                    OwnsHooks = true
                });
            try
            {
                candidate.AddDetour(
                    resetDrawListHook,
                    HookTarget.FromAddress(
                        unchecked((ulong)(libraryHandle + ResetDrawListRva).ToInt64())),
                    ResetDrawList);
                candidate.AddContextHook(
                    visibleTileHook,
                    HookTarget.FromAddress(
                        unchecked((ulong)(libraryHandle + VisibleTileHookRva).ToInt64())),
                    RenderVisiblePreview,
                    new ContextHookOptions
                    {
                        Registers = X64SmartCPUContextRegs.All,
                        HookSize = VisibleTileMinimumHookLength,
                        ErrorMode = CallbackErrorMode.LogAndContinue,
                        Placement = OverwrittenInstructionPlacement.AfterCallback
                    });
                CommitResult result = candidate.Commit();
                if (!result.IsCompleteSuccess || !visibleTileHook.Success ||
                    !visibleTileHook.IsInstalled || !resetDrawListHook.Success ||
                    visibleTileHook.Hook.DisplacedByteCount !=
                        ExpectedVisibleTileDisplacedBytes)
                {
                    throw new InvalidOperationException(
                        "Formation preview marker hooks failed or displaced an unexpected span: " +
                        $"result={result}, visible={visibleTileHook.Hook?.DisplacedByteCount}.");
                }

                Shared.DebugLogHelper.LogInfo(
                    log,
                    "Formation preview marker renderer ready: " +
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

        internal void PublishProcessLifetime()
        {
            if (!ReplacementAvailable)
                throw new InvalidOperationException(
                    "Formation preview renderer cannot be published before installation.");
            published = true;
        }

        internal void RollbackUnpublished()
        {
            if (published)
                throw new InvalidOperationException(
                    "A published Formation preview renderer must remain process-lived.");
            transaction?.Dispose();
            transaction = null;
            installed = false;
            renderingActive = false;
        }

        internal bool SetPreviewMarkerTiles(IEnumerable<int> tileIds)
        {
            lock (stateRoot)
            {
                if (!ReplacementAvailable)
                    return false;
                try
                {
                    int[] normalized = FormationPreviewMarkerModel.NormalizeTileIds(tileIds);
                    if (normalized.Length == 0)
                    {
                        ClearPreviewMarkerTilesCore();
                        return false;
                    }

                    Dictionary<int, int> current = publishedPreview;
                    if (PreviewEquals(current, normalized))
                        return true;

                    var preview = new Dictionary<int, int>(normalized.Length);
                    int identity = FormationPreviewMarkerModel.FirstSyntheticIdentity;
                    for (int index = 0; index < normalized.Length; index++)
                        preview.Add(normalized[index], identity++);
                    publishedPreview = preview;
                    SetRenderingActiveCore(true);
                    return true;
                }
                catch (Exception exception)
                {
                    FailOpenCore(exception);
                    return false;
                }
            }
        }

        internal void ClearPreviewMarkerTiles()
        {
            lock (stateRoot)
            {
                try
                {
                    ClearPreviewMarkerTilesCore();
                }
                catch (Exception exception)
                {
                    FailOpenCore(exception);
                }
            }
        }

        private void ClearPreviewMarkerTilesCore()
        {
            publishedPreview = EmptyPreview;
            if (ReplacementAvailable)
                SetRenderingActiveCore(false);
        }

        private static bool PreviewEquals(
            Dictionary<int, int> current,
            IReadOnlyList<int> requested)
        {
            if (current.Count != requested.Count)
                return false;
            for (int index = 0; index < requested.Count; index++)
            {
                if (!current.ContainsKey(requested[index]))
                    return false;
            }
            return true;
        }

        private void ResetDrawList(IntPtr drawManager)
        {
            resetDrawListHook.Original(drawManager);
        }

        private void SetRenderingActiveCore(bool shouldBeActive)
        {
            if (!visibleTileHook.IsInstalled)
                throw new InvalidOperationException(
                    "Formation preview visible-tile hook is no longer installed.");
            renderingActive = shouldBeActive;
        }

        private void RenderVisiblePreview(NativePointer<X64SmartCPUContext> context)
        {
            if (!renderingActive || context.Pointer == null)
                return;
            Dictionary<int, int> preview = publishedPreview;
            if (preview.Count == 0)
                return;
            try
            {
                X64SmartCPUContext* registers = context.Pointer;
                int tileId = unchecked((int)(uint)registers->RBX);
                if ((uint)tileId >= FormationPreviewMarkerModel.NativeTileCount ||
                    !preview.TryGetValue(tileId, out int identity))
                    return;

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
            catch (Exception exception)
            {
                FailOpen(exception);
            }
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
            publishedPreview = EmptyPreview;
            renderingActive = false;
            previewFailed();
            if (failureLogged)
                return;
            failureLogged = true;
            Shared.DebugLogHelper.LogError(
                log,
                $"FORMATION_PREVIEW_MARKER_FAIL_OPEN: Vanilla rendering retained; {exception}");
        }
    }
}
