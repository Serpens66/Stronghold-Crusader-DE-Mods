using APIShared;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace PlayerControlRelayTest
{
    // Native access is immediate and serialized by the actual game's monitor. Group changes
    // happen only inside the authenticated Chore, identically on every peer. Local selection,
    // ownership and native hook code stay untouched.
    internal static unsafe class RelayWorld
    {
        private const string SupportedNativeHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        // Vanilla's disband state, excluded by its marking function 0x1904C0.
        private const int NativeDisbandingState = 0x6D;
        private static readonly FieldInfo EngineGateField = FindEngineGateField();
        internal static readonly object EngineGate = ReadEngineGate();
        private static volatile bool ready;
        private static string unavailableReason = "Native relay backend has not initialized.";
        private static IntPtr nativeModule;

        internal static bool IsReady => ready;
        internal static string UnavailableReason => unavailableReason;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetModuleFileName(IntPtr module, StringBuilder path, int capacity);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string moduleName);

        internal static bool Initialize(CrusaderLibraryLoadContext context, out string reason)
        {
            lock (EngineGate)
            {
                ready = false;
                try
                {
                    RequireEngineGate();
                    if (context == null || context.ModuleHandle == IntPtr.Zero || context.Memory.Length == 0 ||
                        context.ModuleHandle != GetModuleHandle("CrusaderDE.dll"))
                        throw new InvalidOperationException("No current Crusader library context.");
                    var path = new StringBuilder(32768);
                    uint length = GetModuleFileName(context.ModuleHandle, path, path.Capacity);
                    if (length == 0 || length >= path.Capacity ||
                        !StringComparer.OrdinalIgnoreCase.Equals(Path.GetFileName(path.ToString()), "CrusaderDE.dll"))
                        throw new InvalidOperationException("The loaded Crusader library path is unavailable.");
                    string actualHash = Shared.DebugLogHelper.GetFileSha256(path.ToString());
                    if (!StringComparer.OrdinalIgnoreCase.Equals(actualHash, SupportedNativeHash))
                        throw new InvalidOperationException("Native relay commands require the audited CrusaderDE.dll image.");
                    // Public typed access still depends on the installed interop layout.
                    if (Marshal.SizeOf(typeof(GameTribe)) != 0x688 || Marshal.SizeOf(typeof(GameUnit)) != 0x490 ||
                        Marshal.OffsetOf(typeof(GameTribe), nameof(GameTribe.r_UnitsInGroup)).ToInt32() != 0x32 ||
                        Marshal.OffsetOf(typeof(GameTribe), nameof(GameTribe.r_TribeStance)).ToInt32() != 0x60A ||
                        Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_GlobalId)).ToInt32() != 0x94 ||
                        Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_ControllableForPlayerId)).ToInt32() != 0x92 ||
                        Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.N0000011E)).ToInt32() != 0xAC ||
                        Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_SelectionAllowed)).ToInt32() != 0x2A0 ||
                        Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_AIState)).ToInt32() != 0x2BC ||
                        Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_AIObjectiveState)).ToInt32() != 0x354 ||
                        Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_TribeId)).ToInt32() != 0x2D4)
                        throw new InvalidOperationException("Installed unit/tribe interop differs from the audited layout.");
                    nativeModule = context.ModuleHandle;
                    unavailableReason = null;
                    ready = true;
                }
                catch (Exception error)
                {
                    nativeModule = IntPtr.Zero;
                    unavailableReason = error.Message;
                }
                reason = unavailableReason;
                return ready;
            }
        }

        internal static int MapSize()
        {
            RequireEngineGate();
            if (!Available()) return 0;
            int size = GameTileManagerAPI.Instance.GetCurrentMapSize();
            return size > 0 && size <= GameTileManagerAPI.MAX_WIDTH ? size : 0;
        }

        // Uses the per-unit filter of Vanilla's selection marking 0x1904C0, without its owner and
        // Lord tests. The raw native field at +0xAC has no Extender name yet.
        internal static bool TryGetSelectableUnit(int id, out RelayUnit result)
        {
            RequireEngineGate();
            result = default;
            if (!Available() || id <= 0 || !UnitAccess.TryGetById(id, out GameUnit* unit, out _) || !UnitAccess.IsReallyAlive(unit) ||
                unit->r_SelectionAllowed == 0 || unit->r_AIObjectiveState != 0 || unit->r_AIState == NativeDisbandingState ||
                unit->N0000011E != 0 ||
                unit->r_GlobalId == 0 || !ValidPlayer(unit->r_ControllableForPlayerId)) return false;
            result = new RelayUnit(id, unit->r_GlobalId, unit->r_ControllableForPlayerId);
            return true;
        }

        // 0x1904C0 leaves Lords out of box marking. Input applies that locally; execution stays
        // independent of it, so all peers decide alike.
        internal static bool IsLord(int id)
        {
            RequireEngineGate();
            return UnitAccess.TryGetById(id, out GameUnit* unit, out _) && unit->r_UnitChimp == eChimps.CHIMP_TYPE_LORD;
        }

        // What Vanilla's troop HUD needs to know about a selected unit. Engine gate held.
        internal static bool TryGetHudUnit(RelayUnit captured, out int type, out int stance)
        {
            type = 0; stance = -1;
            if (!IsSameLiveUnit(captured) || !UnitAccess.TryGetById(captured.Slot, out GameUnit* unit, out _)) return false;
            type = (int)unit->r_UnitChimp;
            if (unit->r_TribeId > 0 && GameTribeManagerAPI.Instance.TryGetTribeById(unit->r_TribeId, out GameTribe* tribe) && tribe != null)
                stance = (int)tribe->r_TribeStance;
            return true;
        }

        internal static bool IsSameLiveUnit(RelayUnit captured)
        {
            RequireEngineGate();
            return Available() && UnitAccess.TryGetById(captured.Slot, out GameUnit* unit, out _) && UnitAccess.IsReallyAlive(unit) &&
                unit->r_GlobalId == captured.GlobalId && unit->r_ControllableForPlayerId == captured.Owner;
        }

        internal static bool CanControl(int requester, int owner)
        {
            RequireEngineGate();
            return Available() && ValidPlayer(requester) && ValidPlayer(owner) &&
                (requester == owner || GamePlayerManagerAPI.Instance.IsPlayerAlliedTo(requester, owner));
        }

        internal static bool TryPlan(RelayCommand command, out RelayOrderPlan plan, out string reason)
        {
            RequireEngineGate();
            plan = null; reason = null;
            if (!Available()) { reason = unavailableReason ?? "Native relay backend is unavailable."; return false; }
            if (MapSize() == 0) { reason = "No ready native map."; return false; }
            if (!RelayValidation.TryPlan(command, GameTileManagerAPI.MAX_WIDTH, GameTileManagerAPI.MAX_HEIGHT,
                ResolveOrderUnit, TribeMemberCount, out plan, out reason)) return false;
            if (RelayValidation.UsesTile(command.Action) &&
                !GameTileManagerAPI.Instance.IsTileInsideMapBounds(command.TargetX, command.TargetY))
            { plan = null; reason = "Order target is outside the playable map."; return false; }
            return true;
        }

        // A unit the owner has selected is ordered too: Vanilla's selection code (0x1173F0, 0x19A5D0,
        // 0x19A700) reads the unit's current group, so it reaches the unit in its new group.
        private static bool ResolveOrderUnit(int slot, int owner, uint maxGlobalId, out int tribeId, out int stance)
        {
            tribeId = 0; stance = 0;
            if (!TryGetSelectableUnit(slot, out RelayUnit live) || live.Owner != owner || live.GlobalId > maxGlobalId ||
                !UnitAccess.TryGetById(slot, out GameUnit* unit, out _)) return false;
            tribeId = unit->r_TribeId;
            if (tribeId > 0 && GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out GameTribe* tribe) && tribe != null)
                stance = (int)tribe->r_TribeStance;
            return true;
        }

        private static int TribeMemberCount(int tribeId) =>
            GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out GameTribe* tribe) && tribe != null &&
            tribe->r_AliveState == AliveState.IsAlive ? tribe->r_UnitsInGroup : -1;

        // The lifecycle owner may supply its epoch guard. Reentrant native-order
        // participants cannot make an old batch continue in a replacement world.
        // nativeCalled tells a clean rejection apart from possible partial effects.
        internal static bool Execute(RelayCommand command, Func<bool> isCurrent, out string reason, out bool nativeCalled)
        {
            RequireEngineGate();
            reason = null; nativeCalled = false;
            if (isCurrent != null && !isCurrent()) { reason = "Relay world changed before execution."; return false; }
            if (!TryPlan(command, out RelayOrderPlan plan, out reason)) return false;
            int tribeId = plan.ExistingTribe;
            nativeCalled = true;
            if (tribeId <= 0 && !TryRegroup(command.Owner, plan, out tribeId, out reason)) return false;
            bool success;
            try
            {
                success = Issue(command, tribeId);
            }
            catch (Exception error)
            {
                reason = "Native " + command.Action + " threw " + error.GetType().Name + "; effects may be partial; no retry.";
                return false;
            }
            if (isCurrent != null && !isCurrent()) { reason = "Relay world changed in the terminal callback; effects are not confirmed."; return false; }
            reason = (success ? plan.Slots.Count + " units ordered" : "Native order for " + plan.Slots.Count + " units returned failure") +
                "; group=" + tribeId + "; skipped=" + plan.Skipped + "; movement/arrival is not confirmed.";
            return success;
        }

        // One public group call; each reads and writes simulation state only, so all peers agree.
        private static bool Issue(RelayCommand command, int tribeId)
        {
            var tribes = GameTribeManagerAPI.Instance;
            switch (command.Action)
            {
                case RelayAction.Move:
                    return tribes.IssueMoveHereCommand(tribeId, command.TargetX, command.TargetY,
                        isPatrolPath: false, bIsNewOrder: 1, tribeMoveType: TribeMoveType.DefaultInSync);
                case RelayAction.Stop: return tribes.Stop(tribeId);
                case RelayAction.Stance: return tribes.SetStance(tribeId, (TribeStance)command.TargetX);
                case RelayAction.Disband: return tribes.IssueTargettedCommand(tribeId, TribeAICommand.UnitDissolve, 0, 0, 1);
                case RelayAction.AttackHere: return tribes.AttackTile(tribeId, command.TargetX, command.TargetY);
                case RelayAction.Patrol:
                    // Between the group's current place and the clicked tile, as endless patrol.
                    if (!tribes.TryGetTribeById(tribeId, out GameTribe* tribe) || tribe == null ||
                        !UnitAccess.TryGetById(tribe->r_LeaderUnitId, out GameUnit* leader, out _)) return false;
                    var path = new[] {
                        new UnmanagedVector2<ushort>((ushort)leader->r_CurrentTilePositionX, (ushort)leader->r_CurrentTilePositionY),
                        new UnmanagedVector2<ushort>((ushort)command.TargetX, (ushort)command.TargetY) };
                    return tribes.SetPatrolPath(tribeId, path, TribePatrolMode.PatrolInfinite, 1);
                default: return false;
            }
        }

        // Same public calls and order as BugfixesAndQoL's EnsureDedicatedTribe, and the same native
        // functions as Vanilla's recruit path. Vanilla's maintenance 0x1261F0 clears emptied groups.
        private static bool TryRegroup(int owner, RelayOrderPlan plan, out int created, out string reason)
        {
            reason = null;
            var tribes = GameTribeManagerAPI.Instance;
            long value = tribes.Create(owner, false);
            if (value <= 0 || value > int.MaxValue) { created = 0; reason = "No native group could be created."; return false; }
            created = (int)value;
            tribes.SetStance(created, (TribeStance)plan.Stance);
            for (int index = 0; index < plan.Slots.Count; index++)
            {
                int slot = plan.Slots[index], source = plan.SourceTribes[index];
                bool moved = (source <= 0 || tribes.UnassignUnit(source, slot)) && tribes.AssignUnit(created, slot) &&
                    UnitAccess.TryGetById(slot, out GameUnit* unit, out _) && unit->r_TribeId == created;
                if (moved) continue;
                RollBack(plan, index, created);
                reason = "Native regrouping failed for unit " + slot + " and was rolled back.";
                return false;
            }
            return true;
        }

        private static void RollBack(RelayOrderPlan plan, int failedIndex, int created)
        {
            var tribes = GameTribeManagerAPI.Instance;
            for (int index = failedIndex; index >= 0; index--)
            {
                int slot = plan.Slots[index], source = plan.SourceTribes[index];
                if (!UnitAccess.TryGetById(slot, out GameUnit* unit, out _)) continue;
                if (unit->r_TribeId == created) tribes.UnassignUnit(created, slot);
                if (unit->r_TribeId == 0 && source > 0) tribes.AssignUnit(source, slot);
            }
            tribes.DeleteTribeSafe(created);
        }

        private static bool ValidPlayer(int id) => id > 0 && id <= GamePlayerManagerAPI.MAX_PLAYERS;
        private static bool Available() => ready && nativeModule != IntPtr.Zero &&
            nativeModule == GetModuleHandle("CrusaderDE.dll");

        private static FieldInfo FindEngineGateField()
        {
            FieldInfo field = typeof(EngineInterface).GetField("threadLock", BindingFlags.NonPublic | BindingFlags.Static);
            if (field == null || field.FieldType != typeof(object))
                throw new MissingFieldException("Installed EngineInterface.threadLock contract changed.");
            return field;
        }

        private static object ReadEngineGate() => EngineGateField.GetValue(null) ??
            throw new InvalidOperationException("The installed engine monitor is absent.");

        private static void RequireEngineGate()
        {
            if (!Monitor.IsEntered(EngineGate) || !ReferenceEquals(EngineGateField.GetValue(null), EngineGate))
                throw new InvalidOperationException("Relay native access requires the current EngineInterface monitor.");
        }
    }
}
