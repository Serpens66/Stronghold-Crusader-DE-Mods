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
    // Native access is immediate and serialized by the actual game's monitor. This
    // adapter never changes selection, ownership, membership or native hook code.
    internal static unsafe class RelayWorld
    {
        private const string SupportedNativeHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        // Hash-bound GameTribe membership layout: 625 words encode unit IDs 0..9999.
        private const int MembershipWords = 625;
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
                        Marshal.OffsetOf(typeof(GameTribe), nameof(GameTribe.r_UnitIdsInGroupBitfield)).ToInt32() != 0x36 ||
                        Marshal.OffsetOf(typeof(GameTribe), nameof(GameTribe.r_UnitsInGroup)).ToInt32() != 0x32 ||
                        Marshal.OffsetOf(typeof(GameTribe), nameof(GameTribe.r_LeaderUnitId)).ToInt32() != 0x30 ||
                        Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_GlobalId)).ToInt32() != 0x94 ||
                        Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_ControllableForPlayerId)).ToInt32() != 0x92 ||
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

        internal static void Initialize(CrusaderLibraryLoadContext context)
        {
            if (!Initialize(context, out string reason)) throw new InvalidOperationException(reason);
        }

        internal static int MapSize()
        {
            RequireEngineGate();
            if (!Available()) return 0;
            int size = GameTileManagerAPI.Instance.GetCurrentMapSize();
            return size > 0 && size <= GameTileManagerAPI.MAX_WIDTH ? size : 0;
        }

        // Called for an explicit UI refresh, never from a permanent per-unit hook.
        internal static RelayUnit[] SnapshotLiveUnits()
        {
            RequireEngineGate();
            if (!Available() || MapSize() == 0) return Array.Empty<RelayUnit>();
            var result = new List<RelayUnit>();
            var units = GameUnitManagerAPI.Instance;
            int capacity = units.GetUnitArray().Length;
            for (int id = 1; id <= capacity; id++)
            {
                if (!UnitAccess.TryGetById(units, id, out GameUnit* unit, out _) || !UnitAccess.IsReallyAlive(unit) ||
                    unit->r_GlobalId == 0 || !ValidPlayer(unit->r_ControllableForPlayerId) || unit->r_TribeId == 0)
                    continue;
                result.Add(new RelayUnit(id, unit->r_GlobalId, unit->r_ControllableForPlayerId));
            }
            return result.ToArray();
        }

        internal static bool ResolveUnit(RelayUnit captured, out RelayUnit live, out int tribeSlot)
        {
            RequireEngineGate();
            live = default; tribeSlot = 0;
            if (!Available() || captured.Slot <= 0 || captured.GlobalId == 0 || !ValidPlayer(captured.Owner) ||
                !UnitAccess.TryGetById(captured.Slot, out GameUnit* unit, out _) || !UnitAccess.IsReallyAlive(unit) ||
                unit->r_GlobalId != captured.GlobalId || unit->r_ControllableForPlayerId != captured.Owner ||
                !GameTribeManagerAPI.Instance.IsValidId(unit->r_TribeId)) return false;
            live = new RelayUnit(captured.Slot, unit->r_GlobalId, unit->r_ControllableForPlayerId);
            tribeSlot = unit->r_TribeId;
            return true;
        }

        internal static bool ResolveTribe(int tribeSlot, out RelayTribe result)
        {
            RequireEngineGate();
            result = null;
            if (!Available()) return false;
            var tribes = GameTribeManagerAPI.Instance;
            if (!tribes.IsValidId(tribeSlot) || !tribes.TryGetTribeById(tribeSlot, out GameTribe* tribe) || tribe == null ||
                tribe->r_AliveState != AliveState.IsAlive || tribe->r_GlobalId == 0 || !ValidPlayer(tribe->r_PlayerIdOwner) ||
                tribe->r_UnitsInGroup < 1 || tribe->r_UnitsInGroup > RelayCommand.MaximumUnits) return false;

            int owner = tribe->r_PlayerIdOwner, expectedCount = tribe->r_UnitsInGroup;
            uint identity = tribe->r_GlobalId;
            var members = new List<RelayUnit>(expectedCount);
            bool leaderFound = false;
            ushort* bitmap = &tribe->r_UnitIdsInGroupBitfield;
            // Scan the COMPLETE bitmap. GetUnits stops at the recorded count, which
            // cannot prove that no extra, unrequested membership bit is present.
            for (int word = 0; word < MembershipWords; word++)
            {
                ushort bits = bitmap[word];
                if (bits == 0) continue;
                for (int bit = 0; bit < 16; bit++)
                {
                    if ((bits & (1 << bit)) == 0) continue;
                    int id = word * 16 + bit; // The native bit position is the one-based game ID.
                    if (members.Count >= expectedCount || !UnitAccess.TryGetById(id, out GameUnit* unit, out _) ||
                        !UnitAccess.IsReallyAlive(unit) || unit->r_GlobalId == 0 ||
                        unit->r_ControllableForPlayerId != owner || unit->r_TribeId != tribeSlot) return false;
                    members.Add(new RelayUnit(id, unit->r_GlobalId, owner));
                    if (id == tribe->r_LeaderUnitId) leaderFound = true;
                }
            }
            if (members.Count != expectedCount || !leaderFound) return false;
            result = new RelayTribe(tribeSlot, identity, owner, members.ToArray());
            return true;
        }

        internal static bool CanControl(int requester, int owner)
        {
            RequireEngineGate();
            return Available() && ValidPlayer(requester) && ValidPlayer(owner) &&
                (requester == owner || GamePlayerManagerAPI.Instance.IsPlayerAlliedTo(requester, owner));
        }

        internal static bool TryPlan(RelayCommand command, out RelayGroupPlan plan, out string reason)
        {
            RequireEngineGate();
            plan = null; reason = null;
            if (!Available()) { reason = unavailableReason ?? "Native relay backend is unavailable."; return false; }
            if (MapSize() == 0) { reason = "No ready native map."; return false; }
            if (!RelayValidation.TryPlan(command, GameTileManagerAPI.MAX_WIDTH, GameTileManagerAPI.MAX_HEIGHT,
                ResolveUnit, ResolveTribe, out plan, out reason)) return false;
            if (command.Action == RelayAction.Move &&
                !GameTileManagerAPI.Instance.IsTileInsideMapBounds(command.TargetX, command.TargetY))
            { plan = null; reason = "Move target is outside the playable map."; return false; }
            return true;
        }

        internal static bool Execute(RelayCommand command, out string reason) => Execute(command, null, out reason);

        // The lifecycle owner may supply its epoch guard. Reentrant native-order
        // participants cannot make an old batch continue in a replacement world.
        internal static bool Execute(RelayCommand command, Func<bool> isCurrent, out string reason)
        {
            RequireEngineGate();
            reason = null;
            if (isCurrent != null && !isCurrent()) { reason = "Relay world changed before execution."; return false; }
            if (!TryPlan(command, out RelayGroupPlan plan, out reason)) return false;
            foreach (RelayTribe captured in plan.Groups)
            {
                if (isCurrent != null && !isCurrent())
                { reason = "Relay world changed during execution; no further group was called."; return false; }
                if (!ResolveTribe(captured.Slot, out RelayTribe current) || !SameTribe(captured, current))
                { reason = "A tribe changed before its terminal call; no further group was called."; return false; }
                bool success;
                try
                {
                    success = command.Action == RelayAction.Move
                        ? GameTribeManagerAPI.Instance.IssueMoveHereCommand(captured.Slot, command.TargetX, command.TargetY,
                            isPatrolPath: false, bIsNewOrder: 1, tribeMoveType: TribeMoveType.DefaultInSync)
                        : GameTribeManagerAPI.Instance.Stop(captured.Slot);
                }
                catch (Exception error)
                {
                    reason = "Native " + command.Action + " threw " + error.GetType().Name + "; effects may be partial; no retry.";
                    return false;
                }
                if (!success)
                { reason = "Native " + command.Action + " returned failure; effects may be partial; no retry."; return false; }
                if (isCurrent != null && !isCurrent())
                { reason = "Relay world changed in the terminal callback; effects are not confirmed."; return false; }
            }
            reason = "Native group API calls completed; movement/arrival is not confirmed.";
            return true;
        }

        private static bool SameTribe(RelayTribe left, RelayTribe right)
        {
            if (left == null || right == null || left.Slot != right.Slot || left.GlobalId != right.GlobalId ||
                left.Owner != right.Owner || left.Members.Count != right.Members.Count) return false;
            for (int i = 0; i < left.Members.Count; i++)
                if (!left.Members[i].Equals(right.Members[i])) return false;
            return true;
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
