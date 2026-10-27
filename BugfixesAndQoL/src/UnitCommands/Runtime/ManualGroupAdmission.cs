using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.X64.Assembly;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime
    {
        private bool manualAdmissionReady;
        private APIShared.Internal.NativeTroopCommandModeReader manualGroundCommandMode;
        private readonly Stack<ManualRegionAdmissionBatch> manualAdmissionPool = new Stack<ManualRegionAdmissionBatch>();
        [ThreadStatic] private static ManualRegionAdmissionBatch manualAdmission;
        private long manualAdmissionGeneration;
        private IntPtr manualAdmissionSlot;
        private ExecutableFunction manualThreadTokenCode;
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate IntPtr ThreadTokenDelegate();
        private ThreadTokenDelegate manualThreadToken;
        internal struct ManualPlayerMoveFrame { internal bool Valid, GroupSeen, Rejected; internal int Tribe; internal object GroupArguments; }
        [ThreadStatic] private static ManualPlayerMoveFrame manualPlayerMove;
        private Func<int,int,int,int,int> manualRegionQuery;
        private readonly HookHandle<X64InlineHook> manualAdmissionGate = new HookHandle<X64InlineHook>();

        private void InstallManualGroupAdmission(ReadOnlySpan<byte> memory, ulong libraryBase)
        {
            HookTransaction candidate = null;
            try
            {
                manualGroundCommandMode = new APIShared.Internal.NativeTroopCommandModeReader(
                    new IntPtr(unchecked((long)libraryBase)), memory);
                if (originalRegionPairReachability == null || originalCommonGroupMove == null ||
                    originalRegionReachability == null || originalFirstGroupUnitOnCompletedMoat == null)
                    throw new InvalidOperationException("The shared native group/region owners are unavailable.");
                ValidateExactBytes(memory, ManualGroupAdmissionGate.Rva, ManualGroupAdmissionGate.Bytes, "manual group admission before order writes");
                ValidateExactBytes(memory, ManualGroupAdmissionGate.SkipRva, new byte[] { 0x0F,0xBF,0x4F,0x5C,0x3B,0xD9 }, "manual group admission loop continuation");
                ValidateGameUnitFieldOffset(nameof(GameUnit.r_GlobalId), 0x94);
                ValidateGameUnitFieldOffset(nameof(GameUnit.r_PathConnectionMode), 0x35C);
                ValidateGameUnitFieldOffset(nameof(GameUnit.N000000DB), 0x442);
                ValidateGameUnitFieldOffset(nameof(GameUnit.r_PathPlanStateBitFlags), 0xF2);
                ValidateGameUnitFieldOffset(nameof(GameUnit.r_MovementSubstep), 0xF4);
                // Full E2610 non-stack write closure, pinned by the enclosing native hash.
                ValidateExactBytes(memory, 0xE2660, new byte[] { 0xFF,0x81,0xC4,0,0,0 }, "region counter");
                ValidateExactBytes(memory, 0xE2747, new byte[] { 0x45,0x89,0xB4,0x24,0xC0,0,0,0 }, "region state");
                ValidateExactBytes(memory, 0xE2C29, new byte[] { 0x44,0x89,0x35,0xC8,0xAA,0xFC,0x05 }, "region fallback state");
                byte[] live = new byte[14];
                Marshal.Copy(new IntPtr(unchecked((long)(libraryBase + ManualGroupAdmissionGate.Rva))), live, 0, live.Length);
                if (!new ReadOnlySpan<byte>(live).SequenceEqual(ManualGroupAdmissionGate.Bytes))
                    throw new InvalidOperationException("Another hook overlaps the manual admission span.");
                manualAdmissionSlot = Marshal.AllocHGlobal(IntPtr.Size);
                Marshal.WriteIntPtr(manualAdmissionSlot, IntPtr.Zero);
                manualThreadTokenCode = ExecutableFunction.Create(ManualGroupAdmissionGate.EmitThreadToken);
                manualThreadToken = manualThreadTokenCode.GetDelegate<ThreadTokenDelegate>();
                manualRegionQuery = QueryManualGroupRegion;
                for (int i = 0; i < 4; i++) manualAdmissionPool.Push(new ManualRegionAdmissionBatch());
                candidate = CreateOwnedHookTransaction();
                candidate.AddInline(manualAdmissionGate, HookTarget.FromAddress(libraryBase + ManualGroupAdmissionGate.Rva),
                    (asm, original, resume) => {
                        ManualGroupAdmissionGate.Emit(asm, manualAdmissionSlot, libraryBase + ManualGroupAdmissionGate.SkipRva);
                        asm.AddInstructions(original);
                    }, hookSize: 14);
                CommitResult result = CommitPermanentHooks(candidate);
                if (!result.IsCompleteSuccess || !manualAdmissionGate.Success || !manualAdmissionGate.IsInstalled ||
                    manualAdmissionGate.Hook.DisplacedByteCount != 14)
                    throw new InvalidOperationException("The manual group admission hook contract changed.");
                manualAdmissionReady = true;
                Shared.DebugLogHelper.LogInfo(log, "Manual group admission installed: reference-rva=0x118F50 span=14 resume=0x118F5E reject=0x118FBE; native-only member gate.");
            }
            catch (Exception exception)
            {
                manualAdmissionReady = false;
                RollbackUnpublishedTransaction(candidate);
                TryLogDiagnosticFailure("manual-group-admission-install", exception);
            }
        }

        private ManualRegionAdmissionBatch BeginManualGroupAdmission(TribeIssueOrderMoveHereEventArgs args)
        {
            ManualRegionAdmissionBatch parent = manualAdmission;
            bool ownsPlayerFrame = manualPlayerMove.Valid && !manualPlayerMove.GroupSeen && manualPlayerMove.Tribe == args.TribeId;
            if (ownsPlayerFrame) { manualPlayerMove.GroupSeen = true; manualPlayerMove.GroupArguments = args; }
            manualAdmission = null; // Irrelevant nested calls never inherit a parent's decision.
            if (!manualAdmissionReady || !ManualCommandsEnabled || nativeManualProbe || args.SkipOriginalFunction ||
                queueDefersMove?.Invoke(args) == true ||
                activeAttackCommand != null || activeDirectFillCommand != null || activeMoatWorkSelection != null ||
                activePlan != null || pendingPlan != null || GetCurrentUnitMoveFrame() != null ||
                (uint)args.TileX >= MapWidth || (uint)args.TileY >= MapWidth || (args.TileX == 0 && args.TileY == 0) ||
                movementTargetAvailability[args.TileY * MapWidth + args.TileX] == 0 ||
                !GameTribeManagerAPI.Instance.TryGetTribeById(args.TribeId, out GameTribe* tribe) || tribe == null ||
                !GamePlayerManagerAPI.Instance.IsPlayerIdValid(tribe->r_PlayerIdOwner) ||
                (!APIShared.GameModes.GameModeHelper.IsMapEditor() &&
                 GamePlayerManagerAPI.Instance.IsAIPlayer(tribe->r_PlayerIdOwner))) return parent;
            if (!APIShared.UnitAccess.TryGetById(tribe->r_LeaderUnitId, out GameUnit* leader, out _) ||
                leader == null || !APIShared.UnitAccess.IsReallyAlive(leader) || leader->r_TribeId != args.TribeId ||
                leader->r_ControllableForPlayerId != tribe->r_PlayerIdOwner) return parent;
            int tile = GameTileManagerAPI.Instance.GetTileId(args.TileX, args.TileY);
            if (!IsValidTileId(tile) || IsCompletedMoatTile(tile) || (tileFlags[tile] & 0x30) != 0 ||
                unchecked((short)pathRegionGrid[tile]) <= 0) return parent;
            ManualRegionAdmissionBatch batch = manualAdmissionPool.Count != 0 ? manualAdmissionPool.Pop() : new ManualRegionAdmissionBatch();
            batch.Reset(checked(++manualAdmissionGeneration));
            batch.Parent = parent; batch.Tribe = args.TribeId; batch.X = args.TileX; batch.Y = args.TileY;
            batch.NativeArguments = args;
            batch.Player = tribe->r_PlayerIdOwner; batch.Epoch = mapEpoch; batch.Tick = CaptureCurrentGameTick(); batch.Revision = placementRevision;
            manualAdmission = batch;
            long measured = 0;
            Shared.DevDiag.Start(ref measured);
            try
            {
                int count = tribe->r_UnitsInGroup;
                if (count > ManualRegionAdmissionBatch.Capacity) { batch.Supported = false; return parent; }
                for (int ordinal = 0; ordinal < count; ordinal++)
                {
                    int id = getGroupUnitId(nativeTribeManager, args.TribeId, ordinal);
                    AddManualAdmissionMember(batch, id, tile);
                }
                batch.Evaluate(batch.Player, unchecked((short)pathRegionGrid[tile]), manualRegionQuery);
                if (batch.Supported) batch.Publish(manualThreadToken(), (IntPtr)nativeUnitManager);
                if (batch.RejectAll)
                {
                    args.SkipOriginalFunction = true; args.ReturnValue = 0;
                    if (ownsPlayerFrame) manualPlayerMove.Rejected = true;
                }
                Shared.DevDiag.Count("manual.admission.members", batch.Count);
                Shared.DevDiag.Count("manual.admission.excluded", batch.Supported ? batch.Excluded : 0);
            }
            catch (Exception exception)
            {
                batch.Supported = false;
                TryLogDiagnosticFailure("manual-group-admission", exception);
            }
            finally { Shared.DevDiag.Stop(ref measured, "manual.admission"); }
            return parent;
        }

        private void AddManualAdmissionMember(ManualRegionAdmissionBatch batch, int id, int tile)
        {
            if (!APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _) || unit == null || !APIShared.UnitAccess.IsReallyAlive(unit)) return;
            GetNativeMovementStart(unit, out int x, out int y);
            bool valid = (uint)x < MapWidth && (uint)y < MapWidth;
            int start = valid ? GameTileManagerAPI.Instance.GetTileId(x, y) : -1;
            bool ordinary = valid && IsValidTileId(start) && unit->r_TribeId == batch.Tribe &&
                unit->r_ControllableForPlayerId == batch.Player && !IsCompletedMoatTile(start) &&
                (uint)unit->r_CurrentTilePositionX < MapWidth && (uint)unit->r_CurrentTilePositionY < MapWidth &&
                GameTileManagerAPI.Instance.GetTileId(unit->r_CurrentTilePositionX, unit->r_CurrentTilePositionY) ==
                    unchecked((int)unit->r_CurrentPositionTileId) &&
                IsValidTileId(unchecked((int)unit->r_CurrentPositionTileId)) &&
                !IsCompletedMoatTile(unchecked((int)unit->r_CurrentPositionTileId)) &&
                (tileFlags[start] & CursorSpecialStructureTileFlagMask) == 0 &&
                (tileFlags[tile] & CursorSpecialStructureTileFlagMask) == 0 &&
                (int)unit->r_UnitChimp != (int)SHCDESE.Interop.eChimps.CHIMP_TYPE_ARAB_ASSASIN &&
                !(TraversalEnabled && CanDigMoat(unit));
            batch.Add(id, unit->r_GlobalId, ordinary ? unchecked((short)pathRegionGrid[start]) : 0,
                unchecked((short)unit->r_PathConnectionMode), ordinary, unit->N000000DB != 0);
        }

        internal FormationUnit[] CapturePreviewFormationAdmission(FormationUnit[] units, int tribeId, int x, int y,
            out long revision)
        {
            revision = placementRevision;
            if (!manualAdmissionReady || !ManualCommandsEnabled || (uint)x >= MapWidth || (uint)y >= MapWidth ||
                movementTargetAvailability[y * MapWidth + x] == 0 ||
                !GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out GameTribe* tribe) || tribe == null ||
                !GamePlayerManagerAPI.Instance.IsPlayerIdValid(tribe->r_PlayerIdOwner) ||
                (!APIShared.GameModes.GameModeHelper.IsMapEditor() &&
                 GamePlayerManagerAPI.Instance.IsAIPlayer(tribe->r_PlayerIdOwner))) return units;
            int tile = GameTileManagerAPI.Instance.GetTileId(x, y);
            if (!IsValidTileId(tile) || IsCompletedMoatTile(tile) ||
                (tileFlags[tile] & (CursorSpecialStructureTileFlagMask | 0x30u)) != 0 ||
                unchecked((short)pathRegionGrid[tile]) <= 0) return units;
            ManualRegionAdmissionBatch batch = manualAdmissionPool.Count != 0 ? manualAdmissionPool.Pop() : new ManualRegionAdmissionBatch();
            batch.Reset(checked(++manualAdmissionGeneration));
            batch.Tribe = tribeId; batch.Player = tribe->r_PlayerIdOwner;
            try
            {
                foreach (FormationUnit unit in units) AddManualAdmissionMember(batch, unit.UnitId, tile);
                if (batch.Count != units.Length) return units;
                batch.Evaluate(batch.Player, unchecked((short)pathRegionGrid[tile]), QueryPreviewGroupRegion);
                if (!batch.Supported || batch.Excluded == 0) return units;
                batch.Publish(IntPtr.Zero, IntPtr.Zero); // Private table; never activate a UI decision in a native hook.
                return FilterFormationMembers(units, batch);
            }
            finally { manualAdmissionPool.Push(batch); }
        }

        private void EndManualGroupAdmission(ManualRegionAdmissionBatch parent)
        {
            ManualRegionAdmissionBatch completed = manualAdmission;
            manualAdmission = parent;
            if (completed != null && !ReferenceEquals(completed, parent))
            {
                completed.Parent = null;
                completed.NativeArguments = null;
                manualAdmissionPool.Push(completed);
            }
        }

        internal void RejectPreparedMove(TribeIssueOrderMoveHereEventArgs args)
        {
            args.SkipOriginalFunction = true; args.ReturnValue = 0;
            if (manualPlayerMove.Valid && ReferenceEquals(manualPlayerMove.GroupArguments, args)) manualPlayerMove.Rejected = true;
        }

        internal bool TryRejectPreparedFormationWithoutSlots(TribeIssueOrderMoveHereEventArgs args)
        {
            ManualRegionAdmissionBatch batch = manualAdmission;
            if (batch?.Supported != true || !ReferenceEquals(batch.NativeArguments, args) ||
                batch.Epoch != mapEpoch || batch.Tick != CaptureCurrentGameTick() || batch.Revision != placementRevision)
                return false;
            RejectPreparedMove(args);
            return true;
        }

        private void PrepareDefaultGroupDestinations(TribeIssueOrderMoveHereEventArgs args)
        {
            ManualRegionAdmissionBatch batch = manualAdmission;
            if (args.SkipOriginalFunction || batch?.Mixed != true ||
                batch.Tribe != args.TribeId || batch.X != args.TileX || batch.Y != args.TileY ||
                formationRuntime?.HasPreparedFormation(args) == true) return;
            try
            {
                // 118E00 has no E1D30 selector. One bounded shared collection replaces
                // the missing ordinary slots; never use MoatPlacement's exact path probes.
                FormationRuntime.NativeDestination[] slots = FormationRuntime.CaptureGroundSlots(
                    batch.X, batch.Y, batch.Reachable, 1, false, movementTargetAvailability);
                if (slots.Length == 0) { RejectPreparedMove(args); return; }
                int next = 0;
                for (int i = 0; i < batch.Count; i++)
                {
                    ManualRegionAdmissionBatch.Member member = batch.Members[i];
                    if (member.Decision != ManualRegionAdmission.Reachable) continue;
                    FormationRuntime.NativeDestination slot = slots[next++ % slots.Length];
                    member.DestinationX = slot.X; member.DestinationY = slot.Y;
                    batch.Members[i] = member;
                }
                batch.DefaultSlotsPrepared = true;
                Shared.DevDiag.Count("manual.slots.unique", slots.Length);
            }
            catch (Exception exception)
            {
                RejectPreparedMove(args); // Still before queues or native member mutations.
                TryLogDiagnosticFailure("manual-default-slots", exception);
            }
        }

        private void ApplyDefaultGroupDestination(SHCDESE.EventAPI.Units.UnitMoveHereEventArgs args)
        {
            ManualRegionAdmissionBatch batch = manualAdmission;
            if (args.SkipOriginalFunction || GetCurrentUnitMoveFrame() != null || batch?.Mixed != true || !batch.CommonEntered ||
                args.TileX != batch.X || args.TileY != batch.Y ||
                batch.Epoch != mapEpoch || batch.Tick != CaptureCurrentGameTick() || batch.Revision != placementRevision ||
                manualAdmissionSlot == IntPtr.Zero || Marshal.ReadIntPtr(manualAdmissionSlot) != batch.NativeState ||
                !APIShared.UnitAccess.TryGetById(args.UnitId, out GameUnit* unit, out _) || unit == null ||
                !APIShared.UnitAccess.IsReallyAlive(unit) || unit->r_TribeId != batch.Tribe ||
                unit->r_ControllableForPlayerId != batch.Player ||
                !batch.TryGetDefaultDestination(args.UnitId, unit->r_GlobalId, out int x, out int y)) return;
            args.TileX = x; args.TileY = y;
            unit->r_AttackMoveToTargetTileX = (ushort)x;
            unit->r_AttackMoveToTargetTileY = (ushort)y;
        }

        private bool IsMixedManualGroup(int tribe, int x, int y) => manualAdmissionReady && !disposed &&
            manualAdmission?.Mixed == true && !manualAdmission.CommonEntered && manualAdmission.Tribe == tribe &&
            manualAdmission.X == x && manualAdmission.Y == y && manualAdmission.Epoch == mapEpoch &&
            manualAdmission.Tick == CaptureCurrentGameTick() && manualAdmission.Revision == placementRevision &&
            manualAdmission.NativeArguments is TribeIssueOrderMoveHereEventArgs args && !args.SkipOriginalFunction &&
            args.TribeId == tribe && args.TileX == x && args.TileY == y;

        private void InvalidateManualGroupAdmission()
        {
            if (manualAdmission != null) manualAdmission.Supported = false;
            if (manualAdmissionSlot != IntPtr.Zero) Marshal.WriteIntPtr(manualAdmissionSlot, IntPtr.Zero);
        }

        private void ResetManualGroupAdmission()
        {
            InvalidateManualGroupAdmission();
            manualAdmission = null;
            moveAdmissionParents?.Clear();
            manualPlayerMove = default;
        }

        private bool CanRestoreManualAdmission(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero) return true;
            ManualRegionAdmissionBatch batch = manualAdmission;
            if (batch != null && batch.NativeState == pointer)
                return batch.Supported && batch.Epoch == mapEpoch && batch.Revision == placementRevision;
            if (moveAdmissionParents != null)
                foreach (ManualRegionAdmissionBatch parent in moveAdmissionParents)
                    if (parent != null && parent.NativeState == pointer)
                        return parent.Supported && parent.Epoch == mapEpoch && parent.Revision == placementRevision;
            return false;
        }

        private int QueryManualGroupRegion(int player, int source, int target, int mode)
        {
            Shared.DevDiag.Count("manual.admission.regionQuery");
            return QueryGroupRegionPreservingState(player, source, target, mode);
        }

        private int QueryPreviewGroupRegion(int player, int source, int target, int mode)
        {
            Shared.DevDiag.Count("manual.preview.regionQuery");
            return QueryGroupRegionPreservingState(player, source, target, mode);
        }

        private int QueryGroupRegionPreservingState(int player, int source, int target, int mode)
        {
            byte* manager = (byte*)nativePathManager;
            int fallback = *(int*)(manager + 0x98), state = *(int*)(manager + 0xC0), counter = *(int*)(manager + 0xC4);
            try { return originalRegionPairReachability(nativePathManager, player, source, target, mode); }
            finally { *(int*)(manager + 0x98) = fallback; *(int*)(manager + 0xC0) = state; *(int*)(manager + 0xC4) = counter; }
        }

        internal FormationUnit[] AdmitFormationMembers(FormationUnit[] units, int tribe, int x, int y)
        {
            ManualRegionAdmissionBatch batch = manualAdmission;
            if (batch?.Supported != true || batch.Tribe != tribe || batch.X != x || batch.Y != y ||
                batch.Epoch != mapEpoch || batch.Tick != CaptureCurrentGameTick() || batch.Revision != placementRevision)
                return units;
            return FilterFormationMembers(units, batch);
        }

        private static FormationUnit[] FilterFormationMembers(FormationUnit[] units, ManualRegionAdmissionBatch batch)
        {
            int count = 0;
            foreach (FormationUnit unit in units) if (!batch.IsExcluded(unit.UnitId, unit.GlobalId)) count++;
            if (count == units.Length) return units;
            var movers = new FormationUnit[count];
            count = 0;
            foreach (FormationUnit unit in units) if (!batch.IsExcluded(unit.UnitId, unit.GlobalId)) movers[count++] = unit;
            return movers;
        }
    }
}
