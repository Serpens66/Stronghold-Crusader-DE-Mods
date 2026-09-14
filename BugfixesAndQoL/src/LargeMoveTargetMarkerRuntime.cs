using BepInEx.Logging;
using SHCDESE.API.LowLevel;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace BugfixesAndQoL
{
    internal sealed class LargeMoveTargetMarkerRuntime
    {
        private const int DrawListCountOffset = 0x622248;

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly LargeMoveTargetMarkerRenderer renderer;
        private bool overlayPassActive;

        public LargeMoveTargetMarkerRuntime(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            renderer = new LargeMoveTargetMarkerRenderer(log, () => FeatureEnabled);
        }

        private bool FeatureEnabled =>
            settings.EnableMod && settings.EnableMoveFormationEnhancements;

        public bool MarkerReplacementAvailable => renderer.ReplacementAvailable;

        public void SetPreview(IEnumerable<int> tileIds) =>
            renderer.SetPreviewMarkerTiles(tileIds);

        public void ClearPreview() => renderer.ClearPreviewMarkerTiles();

        public void Install(
            bool markerReplacementEnabled,
            CrusaderLibraryLoadContext context,
            bool fixedLayoutHashValidated)
        {
            if (!markerReplacementEnabled)
                return;
            try
            {
                renderer.Install(context, fixedLayoutHashValidated);
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"MOVE_TARGET_MARKER_HOOK_FAIL_OPEN: Vanilla markers retained; {exception.Message}");
            }
        }

        public void ApplySetting()
        {
            if (!FeatureEnabled)
                Reset();
        }

        public void Reset()
        {
            overlayPassActive = false;
            renderer.Reset();
        }

        public void BeginOverlayPass(int tribeId)
        {
            overlayPassActive = FeatureEnabled && tribeId > 0;
            renderer.BeginOverlayPass(overlayPassActive);
        }

        public void EndOverlayPass(bool completed)
        {
            overlayPassActive = false;
            renderer.EndOverlayPass(completed);
        }

        public bool TryCaptureOverflowCandidate(
            IntPtr drawManager,
            int category,
            int spriteId,
            int layer,
            int verticalOffset,
            int tileId,
            int flags)
        {
            if (!overlayPassActive || !FeatureEnabled ||
                !LargeMoveTargetOverflowModel.IsVanillaMoveTargetMarker(
                    category, spriteId, layer, verticalOffset, flags))
            {
                return true;
            }

            int drawCountBeforeOriginal = Marshal.ReadInt32(
                IntPtr.Add(drawManager, DrawListCountOffset));
            if (!LargeMoveTargetOverflowModel.IsRejectedByFullVanillaList(
                    drawCountBeforeOriginal))
            {
                return true;
            }

            return renderer.TryAddOverflowMarker(
                drawManager,
                category,
                spriteId,
                layer,
                verticalOffset,
                tileId,
                flags);
        }
    }
}
