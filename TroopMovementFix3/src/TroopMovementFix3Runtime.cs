using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using UnityEngine;

namespace TroopMovementFix
{
    internal sealed unsafe class TroopMovementFix3Runtime
    {
        // The complete native tribe record stores freeUnitSpeeds at +0x56C.
        // Script Extender's GameTribe* begins +0x2A into that record.
        private const int TribeFreeUnitSpeedsOffset = 0x542;

        private readonly ManualLogSource log;

        private SpearmanMovementPatch spearmanMovementPatch;
        private IDisposable moveSubscription;
        private bool inputFailureLogged;

        public TroopMovementFix3Runtime(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public void Apply(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            if (spearmanMovementPatch != null ||
                moveSubscription != null)
            {
                return;
            }

            SpearmanMovementPatch newSpearmanMovementPatch = null;
            IDisposable newMoveSubscription = null;

            try
            {
                newSpearmanMovementPatch = new SpearmanMovementPatch(
                    log,
                    memory,
                    unchecked((ulong)libraryHandle.ToInt64()));

                newMoveSubscription =
                    TribeR3EventHooks.OnTribeIssueOrderMoveHere.Observable
                        .Subscribe(OnTribeIssueOrderMoveHere);

                spearmanMovementPatch = newSpearmanMovementPatch;
                moveSubscription = newMoveSubscription;
            }
            catch
            {
                newMoveSubscription?.Dispose();
                newSpearmanMovementPatch?.Dispose();
                throw;
            }

            ModLog.Debug(
                log,
                "Troop Movement Fix 3 active: normal orders remain Vanilla; " +
                "Spearmen use the Archer walk/run decision instead of the " +
                "Improved-Spearman movement override; Ctrl enables Vanilla " +
                "free-unit-speeds.");
        }

        private void OnTribeIssueOrderMoveHere(
            TribeIssueOrderMoveHereEventArgs args)
        {
            if (args.Phase != EventHookPhase.Pre ||
                !args.IsNewOrder ||
                args.MoveType == TribeMoveType.NoChange)
            {
                return;
            }

            if (!ReadCtrlModifier())
                return;

            TryEnableVanillaFreeUnitSpeeds(args.TribeId);
        }

        private bool TryEnableVanillaFreeUnitSpeeds(int tribeId)
        {
            if (tribeId <= 0 ||
                !GameTribeManagerAPI.Instance.TryGetTribeById(
                    tribeId,
                    out GameTribe* tribe) ||
                tribe == null)
            {
                ModLog.Warning(
                    log,
                    $"Ctrl movement could not enable Vanilla free-unit-speeds: " +
                    $"tribeId={tribeId} was not available.");
                return false;
            }

            ushort* freeUnitSpeeds =
                (ushort*)((byte*)tribe + TribeFreeUnitSpeedsOffset);
            *freeUnitSpeeds = 1;
            return true;
        }

        private bool ReadCtrlModifier()
        {
            try
            {
                return Input.GetKey(KeyCode.LeftControl) ||
                       Input.GetKey(KeyCode.RightControl);
            }
            catch (Exception ex)
            {
                if (!inputFailureLogged)
                {
                    inputFailureLogged = true;
                    ModLog.Error(
                        log,
                        $"Could not read the Ctrl movement modifier; " +
                        $"this order remains completely Vanilla: {ex}");
                }

                return false;
            }
        }
    }
}
