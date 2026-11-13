using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using APIShared;
using APIShared.Economy;
using APIShared.Modifiers;
using APIShared.Simulation;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace UiAcceptance
{
    // Explicitly armed local integration acceptance. The existing Runner is the only
    // frame driver; the mission observer captures restore state before simulation starts.
    // Manual SE yield calls prove installed hook integration, not a wood delivery cycle.
    internal static class WorkerProductionSteps
    {
        private static WorkerProductionModifierRegistration registration;
        private static MethodInfo calculateYield;
        private static IntPtr rows, image;
        private static ChecksumCall checksum;
        private static int woodId, millerId, owner;
        private static uint woodGid, millerGid;
        private static bool observer, editor, reloadArmed, reloadVerified, restartArmed, freshVerified;
        private static string savedPath, callbackFailure;
        private static byte[] savedRow;
        private static long previousSession;
        private static bool capacityRegistered, capacityPending;
        private static bool capacityEnsureRoom, capacityPrepareFixture;
        private static int capacityBuilds;
        private static AllyTransferCapacity flourCapacity;
        private static string capacityFailure;
        private static string capacityState = "not requested";
        private static long capacitySession;

        private sealed class MillerTrace
        {
            internal readonly Stopwatch Clock = Stopwatch.StartNew();
            internal readonly int DeadlineMs;
            private string last, lastKey;
            private int lines;
            private long lastLogMs;
            internal MillerTrace(int timeoutMs) { DeadlineMs = Math.Max(1, timeoutMs - 1500); }
            internal void Observe(string state, string key)
            {
                bool structuralChange = key != lastKey;
                if (structuralChange) RequestCapacity();
                lastKey = key;
                if (Clock.ElapsedMilliseconds >= DeadlineMs)
                {
                    Finish("timeout", state);
                    throw new StepFailure("Miller completion timeout; last observed state: " + state);
                }
                if (state == last) return;
                last = state;
                if (lines >= 19 || !structuralChange && Clock.ElapsedMilliseconds - lastLogMs < 1000) return;
                lines++; lastLogMs = Clock.ElapsedMilliseconds;
                AcceptLog.Info("PRODUCTION_MILLER_TRACE n=" + lines + " elapsedMs=" + Clock.ElapsedMilliseconds + " " + state);
            }
            internal void Finish(string outcome, string state)
            {
                if (lines >= 20) return;
                lines++;
                AcceptLog.Info("PRODUCTION_MILLER_TRACE n=" + lines + " outcome=" + outcome + " elapsedMs=" + Clock.ElapsedMilliseconds + " " + state);
            }
        }
        // FBCB9319 native checksum owner. arg1 suppresses Chore12 publication.
        private const int ChecksumRva = 0x22020, NetworkManagerRva = 0x8574320;
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void ChecksumCall(IntPtr manager, int noSend);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string name);

        internal static void Register()
        {
            Runner.Register("Production.Configure", Configure);
            Runner.Register("Production.Setup", Setup);
            Runner.Register("Production.MillerDelivery", MillerDelivery);
            Runner.Register("Production.QuarterAndCrc", QuarterAndCrc);
            Runner.Register("Production.Save", Save);
            Runner.Register("Production.Reload", Reload);
            Runner.Register("Production.Restart", Restart);
            Runner.Register("Production.ExpectFresh", ExpectFresh);
            Runner.Register("Production.Disable", Disable);
        }

        private static IEnumerable<Wait> Configure(Step step)
        {
            Runner.Require(GameSteps.MenuReady(), "Configure production only before a fresh mission.");
            if (registration == null)
                Runner.Require(WorkerProductionModifiers.TryRegister(UiAcceptancePlugin.Guid, "worker-production-acceptance",
                    out registration, out string reason, int.MaxValue), reason);
            registration.SetProfile(new WorkerProductionProfile(new Dictionary<eChimps, WorkerProductionValues> {
                { eChimps.CHIMP_TYPE_WOODCUTTER, new WorkerProductionValues(ValueModifierContribution.Factor(.25m), ValueModifierContribution.Factor(.25m)) },
                { eChimps.CHIMP_TYPE_MILLER, new WorkerProductionValues(ValueModifierContribution.FixedValue(3), ValueModifierContribution.FixedValue(3)) }
            }, ValueModifierContribution.FixedValue(0)));
            if (!observer)
            {
                Runner.Require(UiAcceptancePlugin.Missions != null && UiAcceptancePlugin.Missions.TryRegisterObserver(
                    "worker-production-acceptance", null, null, Initializing, out var diagnostic),
                    "Production lifecycle observer unavailable.");
                observer = true;
            }
            var status = WorkerProductionModifiers.Status;
            Runner.Require(status.CanStart && status.HasBaseContributions && status.ResolvedFixedBonusPercent == 0,
                "Production preflight failed: " + Issues(status));
            ProbeSteps.RequireNativeReferenceHash();
            image = GetModuleHandle("CrusaderDE.dll");
            Runner.Require(image != IntPtr.Zero, "Native library is not loaded.");
            var assembly = typeof(GameUnitManagerAPI).Assembly;
            var wrapper = assembly.GetType("SHCDESE.Detours.BulkUnitDetours", true);
            calculateYield = wrapper.GetMethod("c_game_unit_calculate_worker_good_yield_hook_impl", BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(int), typeof(int), typeof(int) }, null);
            Runner.Require(calculateYield != null && calculateYield.ReturnType == typeof(long), "Installed SE worker yield wrapper ABI changed.");
            var store = typeof(WorkerProductionModifiers).Assembly.GetType("APIShared.Economy.WorkerProductionStateStore", true);
            var address = store.GetField("RowsAddress", BindingFlags.Static | BindingFlags.NonPublic);
            Runner.Require(address != null && address.FieldType == typeof(IntPtr), "Sidecar observation ABI changed.");
            rows = (IntPtr)address.GetValue(null);
            Runner.Require(rows != IntPtr.Zero, "Sidecar table unavailable.");
            checksum = Marshal.GetDelegateForFunctionPointer<ChecksumCall>(image + ChecksumRva);
            AcceptLog.Info("PRODUCTION_CONFIGURED woodFactor=0.25 millerFixed=3 fixedBonus=0 desiredDigest=" + status.Digest);
            yield break;
        }

        private static string Issues(WorkerProductionStatus status)
        {
            var messages = new List<string>();
            foreach (var issue in status.Issues) messages.Add(issue.Code + ":" + issue.Diagnostic);
            return string.Join(";", messages);
        }

        private static IEnumerable<Wait> Setup(Step step)
        {
            var context = UiAcceptancePlugin.LastContext;
            editor = step.Arg(0, "sp") == "editor";
            Runner.Require(context != null && !context.IsSave && !context.Mode.IsRealMultiplayer && context.IsEditor == editor,
                "Production fixture requires a fresh local SP/editor mission.");
            Runner.Require(WorkerProductionModifiers.Status.IsAvailable && WorkerProductionModifiers.Status.CanStart,
                "Installed production backend unavailable: " + Issues(WorkerProductionModifiers.Status));
            woodId = millerId = 0;
            owner = editor ? 1 : HuntSteps.Local();
            if (editor)
            {
                Director.instance.SetPausedState(true);
                lock (HuntSteps.Engine()) CreateEditorWorker();
            }
            else
            {
                if (!capacityRegistered)
                {
                    Runner.Require(SimulationTickEvents.TryRegister(UiAcceptancePlugin.Guid, "production-capacity-observation", CaptureCapacity,
                        out string reason), reason);
                    capacityRegistered = true;
                }
                RequestCapacity(true, true);
                yield return new Until("shared simulation fixture with at least64 free flour capacity", () => !Volatile.Read(ref capacityPending));
                lock (HuntSteps.Engine())
                {
                    Runner.Require(capacityFailure == null && flourCapacity != null && flourCapacity.HasStorage && flourCapacity.FreeAmount >= 64,
                        "Flour fixture capacity was not established: " + capacityFailure + "; " + capacityState);
                    AcceptLog.Info("PRODUCTION_STORAGE_PRECHECK requiredFlourFree=64 verified=True builds=" + capacityBuilds + " " + capacityState);
                }
                yield return new Until("naturally assigned woodcutter and miller", () => {
                    lock (HuntSteps.Engine()) return FindWorker(eChimps.CHIMP_TYPE_WOODCUTTER, out woodId) && FindWorker(eChimps.CHIMP_TYPE_MILLER, out millerId);
                });
                lock (HuntSteps.Engine()) CaptureWoodIdentity();
            }
            AcceptLog.Info("PRODUCTION_FIXTURE editor=" + editor + " owner=" + owner + " wood=" + woodId + ":" + woodGid + " miller=" + millerId);
        }

        private static void RequestCapacity(bool ensureRoom = false, bool prepareFixture = false)
        {
            lock (HuntSteps.Engine())
            {
                capacitySession = UiAcceptancePlugin.LastContext?.SessionId ?? 0;
                capacityEnsureRoom = ensureRoom; capacityPrepareFixture = prepareFixture;
                capacityFailure = null;
                if (ensureRoom) capacityBuilds = 0;
                Volatile.Write(ref capacityPending, true);
            }
        }

        // The shared query explicitly requires this synchronous simulation scope.
        // This callback does no work unless the existing UI driver requested a snapshot.
        private static void CaptureCapacity(int tick)
        {
            if (!Volatile.Read(ref capacityPending)) return;
            lock (HuntSteps.Engine())
            {
                if (!Volatile.Read(ref capacityPending)) return;
                bool waitForNextTick = false;
                try
                {
                    if (capacitySession == 0 || UiAcceptancePlugin.LastContext?.SessionId != capacitySession)
                        throw new StepFailure("Capacity source session changed.");
                    if (!AllyTransfers.TryGetCapacity(owner, eGoods.STORED_FLOUR, out flourCapacity, out string reason))
                        throw new StepFailure("Native flour capacity query failed: " + reason);
                    capacityState = "capacityTick=" + tick + " hasStorage=" + flourCapacity.HasStorage + " flourFree=" + flourCapacity.FreeAmount +
                        " flourStored=" + flourCapacity.AmountStored + " " + StorageRecords();
                    // A short-lived64 reserve was consumed by normal wood arrivals in
                    // the real SP fixture. Establish a larger stockpile BEFORE placing
                    // the hut/mill, then verify the required64 again after their setup.
                    int required = capacityPrepareFixture ? 512 : 64;
                    if (capacityEnsureRoom && (!flourCapacity.HasStorage || flourCapacity.FreeAmount < required))
                    {
                        Runner.Require(capacityBuilds < 16, "Sixteen ordinary stockpile extensions did not establish" + required + " flour capacity: " + capacityState);
                        ExtendStockpile(); capacityBuilds++;
                        // Newly created records may need their ordinary next native update.
                        // Re-query in the next shared tick instead of assuming their capacity.
                        waitForNextTick = true;
                    }
                    else if (capacityPrepareFixture)
                    {
                        AcceptLog.Info("PRODUCTION_STORAGE_INITIAL_RESERVE requiredFlourFree=512 verified=True " + capacityState);
                        PlaceProductionBuildings();
                        capacityPrepareFixture = false;
                        waitForNextTick = true;
                    }
                }
                catch (Exception error) { capacityFailure = error.Message; capacityState = "capacity observation failed: " + error.Message; }
                finally { if (!waitForNextTick) Volatile.Write(ref capacityPending, false); }
            }
        }

        private static unsafe void ExtendStockpile()
        {
            var buildings = GameBuildingManagerAPI.Instance.GetBuildingsArray();
            int anchors = 0;
            for (int index = 0; index < buildings.Length; index++)
            {
                GameBuilding* building = buildings.GetValuePointer(index);
                if (building->r_AliveState != AliveState.IsAlive || building->r_PlayerIdOwner != owner || building->r_BuildingType != eStructs.STRUCT_GOODS_YARD) continue;
                anchors++;
                // Canonical76E80 places four cells three tiles apart. Starting a
                // new6x6 palette at+/-6 stays on that grid; radius2 can overlap it.
                if (HuntSteps.Place(owner, building->r_TilePositionXBegin, building->r_TilePositionYBegin,
                    eMappers.MAPPER_STORES, eStructs.STRUCT_GOODS_YARD, 6, out int x, out int y))
                {
                    AcceptLog.Info("PRODUCTION_STORAGE_EXTENDED attempt=" + (capacityBuilds + 1) + " anchor=" + (index + 1) + " anchorsTried=" + anchors +
                        " at=" + x + "," + y + " prior=" + capacityState);
                    return;
                }
            }
            throw new StepFailure("No legal connected stockpile extension after" + anchors + " living owned anchors; " + capacityState);
        }

        private static unsafe string StorageRecords()
        {
            var records = new List<string>(); int count = 0;
            var buildings = GameBuildingManagerAPI.Instance.GetBuildingsArray();
            for (int index = 0; index < buildings.Length; index++)
            {
                GameBuilding* building = buildings.GetValuePointer(index);
                if (building->r_AliveState != AliveState.IsAlive || building->r_PlayerIdOwner != owner || building->r_BuildingType != eStructs.STRUCT_GOODS_YARD) continue;
                count++;
                if (records.Count < 4) records.Add((index + 1) + ":" + building->r_GlobalId + "@" + building->r_TilePositionXBegin + "," + building->r_TilePositionYBegin +
                    "=" + building->r_LocalStorageGoodType + ":" + building->r_CurrentGoodStackAmount + "/" + building->r_MaxGoodStackAmount);
            }
            return "stockpileRecords=" + count + " first4=" + string.Join(";", records);
        }

        private static unsafe void CreateEditorWorker()
        {
            woodId = checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(owner, owner, 100, 100, 0, eChimps.CHIMP_TYPE_WOODCUTTER));
            Runner.Require(UnitAccess.TryGetById(woodId, out GameUnit* unit, out _) && unit->r_GlobalId != 0,
                "Native editor worker creation returned no identity.");
            woodGid = unit->r_GlobalId;
        }

        private static unsafe void PlaceProductionBuildings()
        {
            Runner.Require(UnitAccess.TryGetById(GamePlayerManagerAPI.Instance.GetLordUnitId(owner), out GameUnit* lord, out _), "Local lord unavailable.");
            int x = lord->r_CurrentTilePositionX, y = lord->r_CurrentTilePositionY;
            Runner.Require(HuntSteps.Place(owner, x, y, eMappers.MAPPER_WOODSMAN, eStructs.STRUCT_WOODCUTTERS_HUT, 8, out _, out _), "No woodsman's site.");
            Runner.Require(HuntSteps.Place(owner, x, y, eMappers.MAPPER_HOVEL, eStructs.STRUCT_HOVEL, 12, out _, out _), "No extra fixture housing site.");
            Runner.Require(HuntSteps.Place(owner, x, y, eMappers.MAPPER_MILL, eStructs.STRUCT_MILL, 10, out _, out _), "No mill site.");
            Runner.Require(GamePlayerManagerAPI.Instance.TryAddGood(owner, eGoods.STORED_RAW_WHEAT, 30), "Wheat could not enter the real stockpile.");
        }

        private static unsafe void CaptureWoodIdentity()
        {
            Runner.Require(UnitAccess.TryGetById(woodId, out GameUnit* unit, out _), "Woodcutter disappeared.");
            woodGid = unit->r_GlobalId;
        }

        private static unsafe bool FindWorker(eChimps kind, out int id)
        {
            foreach (int candidate in UnitAccess.GetAllReallyAliveUnits())
                if (UnitAccess.TryGetById(candidate, out GameUnit* unit, out _) && unit->r_ControllableForPlayerId == owner &&
                    unit->r_UnitChimp == kind && unit->r_LinkedProductionBuildingId != 0)
                { id = candidate; return true; }
            id = 0; return false;
        }

        private static IEnumerable<Wait> MillerDelivery(Step step)
        {
            Runner.Require(!editor, "Natural mill delivery is the SP fixture; editor uses bounded manual yield integration.");
            var trace = new MillerTrace(step.TimeoutMs);
            // Other workers can occupy storage while this fixture is being assigned.
            // Repeat the authoritative query immediately before the delivery measurement.
            RequestCapacity(true);
            yield return new Until("at least64 native free flour capacity before Miller measurement", () => !Volatile.Read(ref capacityPending));
            lock (HuntSteps.Engine())
            {
                Runner.Require(capacityFailure == null && flourCapacity != null && flourCapacity.HasStorage && flourCapacity.FreeAmount >= 64,
                    "Native pre-measurement flour space missing: " + capacityFailure + "; " + capacityState);
                AcceptLog.Info("PRODUCTION_STORAGE_MEASUREMENT_READY requiredFlourFree=64 verified=True builds=" + capacityBuilds + " " + capacityState);
            }
            int before;
            lock (HuntSteps.Engine()) before = GamePlayerManagerAPI.Instance.GetGoodAmount(owner, eGoods.STORED_FLOUR);
            yield return new Until("a genuine manufactured three-flour batch", () => {
                lock (HuntSteps.Engine())
                {
                    bool found = ObserveMillerBatch();
                    trace.Observe(MillerState(before, out string key), key);
                    return found;
                }
            });
            AcceptLog.Info("PRODUCTION_MILLER_BATCH unit=" + millerId + " cargo=3 provenance=MillerBatch");
            int delivered = 0;
            yield return new Until("this miller's complete three-item stockpile delivery", () => {
                lock (HuntSteps.Engine())
                {
                    bool complete = ObserveMillerDelivery(before, out delivered);
                    string state = MillerState(before, out string key);
                    trace.Observe(state, key);
                    if (complete) trace.Finish("complete", state);
                    return complete;
                }
            });
            Director.instance.SetPausedState(true);
            AcceptLog.Info("PRODUCTION_MILLER_DELIVERED unit=" + millerId + " stockpileDelta=" + delivered + " completeBatch=True");
        }

        private static unsafe string MillerState(int before, out string key)
        {
            int flour = GamePlayerManagerAPI.Instance.GetGoodAmount(owner, eGoods.STORED_FLOUR);
            int wheat = GamePlayerManagerAPI.Instance.GetGoodAmount(owner, eGoods.STORED_RAW_WHEAT);
            if (!UnitAccess.TryGetById(millerId, out GameUnit* worker, out _))
            { key = "missing/" + millerId; return key + " expectedGid=" + millerGid + " flourDelta=" + (flour - before) + " wheat=" + wheat + " " + capacityState; }
            uint rawState = unchecked((uint)Marshal.ReadInt32((IntPtr)worker, 0x2BC)); // native origin+918, AI+resume words
            ushort target = unchecked((ushort)Marshal.ReadInt16((IntPtr)worker, 0x332)); // native origin+98E, Miller storage target
            ushort marker = ReadU16(Row(millerId), 6);
            key = worker->r_GlobalId + "/" + worker->r_UnitChimp + "/" + worker->r_AliveState + "/" + worker->r_CarryGoodAmount + "/" + marker + "/" + rawState + "/" + target;
            string targetState = "unresolved";
            if (target > 0 && global::APIShared.BuildingAccess.TryGetById(target, out GameBuilding* store, out _) && store != null)
                targetState = store->r_GlobalId + ":" + store->r_BuildingType + ":" + store->r_AliveState + "@" + store->r_TilePositionXBegin + "," + store->r_TilePositionYBegin;
            return "unit=" + millerId + ":" + worker->r_GlobalId + " expectedGid=" + millerGid + " kind=" + worker->r_UnitChimp + " alive=" + worker->r_AliveState +
                " carry=" + worker->r_CarryGoodAmount + " marker=" + marker + " ai=" + worker->r_AIState + " raw918=0x" + rawState.ToString("X8") +
                " tile=" + worker->r_CurrentTilePositionX + "," + worker->r_CurrentTilePositionY + " production=" + worker->r_LinkedProductionBuildingId +
                " target98E=" + target + " targetRecord=" + targetState + " flourDelta=" + (flour - before) + " wheat=" + wheat + " " + capacityState;
        }

        private static unsafe bool ObserveMillerBatch()
        {
            foreach (int id in UnitAccess.GetAllReallyAliveUnits())
                if (UnitAccess.TryGetById(id, out GameUnit* worker, out _) && worker->r_ControllableForPlayerId == owner &&
                    worker->r_UnitChimp == eChimps.CHIMP_TYPE_MILLER && worker->r_CarryGoodAmount == 3 && (ReadU16(Row(id), 6) & 4) != 0)
                { millerId = id; millerGid = worker->r_GlobalId; return true; }
            return false;
        }

        private static unsafe bool ObserveMillerDelivery(int before, out int delivered)
        {
            delivered = GamePlayerManagerAPI.Instance.GetGoodAmount(owner, eGoods.STORED_FLOUR) - before;
            return UnitAccess.TryGetById(millerId, out GameUnit* worker, out _) && worker->r_GlobalId == millerGid &&
                worker->r_UnitChimp == eChimps.CHIMP_TYPE_MILLER && UnitAccess.IsReallyAlive(worker) && worker->r_CarryGoodAmount == 0 &&
                (ReadU16(Row(millerId), 6) & 4) == 0 && delivered >= 3;
        }

        private static IEnumerable<Wait> QuarterAndCrc(Step step)
        {
            Director.instance.SetPausedState(true);
            lock (HuntSteps.Engine()) ProbeQuarterAndCrc();
            yield break;
        }

        private static unsafe void ProbeQuarterAndCrc()
        {
            lock (HuntSteps.Engine())
            {
                Runner.Require(UnitAccess.TryGetById(woodId, out GameUnit* unit, out _) && unit->r_GlobalId == woodGid &&
                    unit->r_UnitChimp == eChimps.CHIMP_TYPE_WOODCUTTER, "Physical fixture worker identity changed.");
                int productivity = Marshal.ReadInt32(image + 0x379CEEC + owner * 0x583C);
                Runner.Require(productivity <= 100, "Isolated quarter fixture requires no positive fear productivity; actual=" + productivity);
                // Four calls preserve the starting base credit and yield exactly one in total.
                // Calls enter the installed SE wrapper and native hook, not an arithmetic clone.
                int total = 0; bool crcObserved = false;
                for (int n = 0; n < 4; n++)
                {
                    byte[] beforeRow = Row(woodId), beforeNative = UnitRecord(unit);
                    int beforeCrc = UnitChecksum(woodId, out int beforeCategory);
                    long amount = (long)calculateYield.Invoke(null, new object[] { woodId, 1, 0 });
                    Runner.Require(amount == 0 || amount == 1, "Quarter native output outside0..1: " + amount);
                    total += (int)amount;
                    byte[] afterRow = Row(woodId), afterNative = UnitRecord(unit);
                    int afterCrc = UnitChecksum(woodId, out int afterCategory);
                    Runner.Require(Equal(beforeNative, afterNative), "Native worker fields changed during isolated sidecar-only CRC probe.");
                    Runner.Require(!Equal(beforeRow, afterRow) && beforeCrc != afterCrc && beforeCategory != afterCategory,
                        "Installed unit/category CRC did not incorporate the changed sidecar.");
                    crcObserved = true;
                    AcceptLog.Info("PRODUCTION_LIVE_YIELD unit=" + woodId + " call=" + n + " result=" + amount + " fraction=" + Fraction(afterRow) +
                        " nativeBytesEqual=True crcBefore=" + beforeCrc + " crcAfter=" + afterCrc);
                }
                Runner.Require(total == 1 && crcObserved, "Four real quarter outputs did not total one.");
                // Leave a nonzero exact credit for save/load; at most one additional call
                // is needed when the preceding four returned to an empty starting credit.
                if (ReadU64(Row(woodId), 8) == 0) calculateYield.Invoke(null, new object[] { woodId, 1, 0 });
                Runner.Require(ReadU64(Row(woodId), 8) != 0, "Save fixture has no fractional credit.");
                AcceptLog.Info("PRODUCTION_QUARTER_RESULT total=" + total + " savedFraction=" + Fraction(Row(woodId)) + " scope=manual-installed-yield");
            }
        }

        private static int UnitChecksum(int id, out int category)
        {
            IntPtr manager = image + NetworkManagerRva;
            int slot = Marshal.ReadInt32(manager, 0x36FA50);
            Runner.Require(slot >= 0 && slot < 9 && Marshal.ReadInt32(manager, 0xDA8) == 0 && Marshal.ReadInt32(manager, 0x279FA4) == 0,
                "Native checksum owner is throttled or not in the audited state.");
            checksum(manager, 1);
            category = Marshal.ReadInt32(manager, 0x14D108 + slot * 0x38);
            return Marshal.ReadInt32(manager, 0x14F0CC + (slot * 10000 + id) * 4);
        }

        private static IEnumerable<Wait> Save(Step step)
        {
            Director.instance.SetPausedState(true);
            savedPath = Path.Combine(editor ? ConfigSettings.GetUserMapsPath() : ConfigSettings.GetSavesPath(),
                "ProductionAcceptance-" + Guid.NewGuid().ToString("N").Substring(0, 8) + (editor ? ".map" : ".sav"));
            Runner.Require(!File.Exists(savedPath), "Acceptance will not overwrite an existing player file: " + savedPath);
            previousSession = UiAcceptancePlugin.LastContext.SessionId;
            var screen = CameraControls2D.instance.getScreenXYForSaveCentring(1, 1);
            int x = -1, y = -1; GameMap.instance.getRotationCentre(ref x, ref y);
            // SafeToSave is normally called BEFORE EngineInterface's monitor. Drain
            // its director queue first, so an InEngine waiter cannot be blocked by
            // our outer monitor while SafeToSave waits for that same waiter to finish.
            yield return new Until("quiescent paused director for production save", () => Director.instance.SafeToSave());
            lock (HuntSteps.Engine())
            {
                savedRow = Row(woodId);
                Runner.Require(EngineInterface.SaveSaveGame(savedPath, screen.x, screen.y, x, y, false, false, editor), "Critical native save returned failure.");
            }
            Runner.Require(File.Exists(savedPath), "Production save file missing.");
            var runtime = typeof(WorkerProductionModifiers).Assembly.GetType("APIShared.Economy.WorkerProductionStateRuntime", true);
            object requirement = runtime.GetMethod("InspectSelectedSaveRequirement", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { savedPath });
            Runner.Require((bool)Property(requirement, "IsValid") && (bool)Property(requirement, "RequiresExtended") &&
                ((string)Property(requirement, "Digest")).Length == 64, "The actual native save lacks a valid required production sidecar.");
            AcceptLog.Info("PRODUCTION_SAVED path=" + savedPath + " fraction=" + Fraction(savedRow));
            yield break;
        }

        private static IEnumerable<Wait> Reload(Step step)
        {
            Runner.Require(GameSteps.MenuReady() && savedPath != null && savedRow != null, "Return to main menu after Production.Save before reloading.");
            reloadArmed = true; reloadVerified = false; callbackFailure = null;
            long before = UiAcceptancePlugin.StartedSession;
            var header = MapFileManager.Instance.GetFileInfoFromFileName(savedPath, savedPath, 0, true);
            Runner.Require(header != null, "Vanilla save/map header rejected.");
            if (editor)
            {
                // FRONT_EditorSetup's ordinary Load requester callback performs this
                // complete sequence. The loader itself does not leave the front-end scene.
                Runner.Require(header.isMapEditable(), "Vanilla rejected this map as non-editable.");
                GameData.Instance.SetMissionTextFromHeader(header);
                MainViewModel.Instance.InitNewScene(Enums.SceneIDS.MapEditor);
                Runner.Require(EditorDirector.instance.loadMapIntoEditor(header.filePath, header.standAlone_filename), "Editor rejected saved production map.");
            }
            else
            {
                var view = MainViewModel.Instance;
                Platform_Multiplayer.Instance.gameMembers = null;
                view.InitNewScene(Enums.SceneIDS.MainGame);
                EditorDirector.instance.stopGameSim();
                EditorDirector.instance.loadSaveGame(savedPath, header.standAlone_filename, header);
                view.InitObjectiveGoodsPanelDelayed();
            }
            yield return new Until("restored production mission", () => GameSteps.InMission(before));
            Runner.Require(callbackFailure == null && reloadVerified, "NativeLoaded restore checkpoint failed: " + callbackFailure);
            Runner.Require(UiAcceptancePlugin.LastContext.SessionId != previousSession, "Load reused the previous session.");
            AcceptLog.Info("PRODUCTION_RESTORED editor=" + editor + " nativeLoadedBeforeRun=True fraction=" + Fraction(savedRow));
        }

        private static unsafe void Initializing(MissionLifecycleNotification note)
        {
            if (restartArmed && note.Phase == MissionInitializationPhase.NativeLoaded)
            {
                try
                {
                    Runner.Require(note.Context.IsRestart && note.Context.RestartedFromSessionId == previousSession, "Unlinked production restart checkpoint.");
                    lock (HuntSteps.Engine()) RequireEmptyRows();
                    freshVerified = true;
                }
                catch (Exception error) { callbackFailure = error.ToString(); }
                finally { restartArmed = false; }
            }
            if (!reloadArmed || note.Phase != MissionInitializationPhase.NativeLoaded) return;
            try
            {
                Runner.Require(note.Context.IsEditor == editor && note.Context.FilePath != null &&
                    string.Equals(Path.GetFullPath(note.Context.FilePath), Path.GetFullPath(savedPath), StringComparison.OrdinalIgnoreCase), "Unexpected native restore source.");
                lock (HuntSteps.Engine())
                {
                    Runner.Require(UnitAccess.TryGetById(woodId, out GameUnit* unit, out _) && unit->r_GlobalId == woodGid,
                        "Saved native unit identity did not round-trip.");
                    Runner.Require(Equal(savedRow, Row(woodId)), "Exact saved base fraction/provenance did not round-trip.");
                    UnitChecksum(woodId, out _);
                }
                Director.instance.SetPausedState(true);
                reloadVerified = true;
                AcceptLog.Info("PRODUCTION_NATIVE_RESTORE_CHECKPOINT editor=" + editor + " unit=" + woodId + ":" + woodGid +
                    " exactRow=True fraction=" + Fraction(savedRow) + " crcExecuted=True checkpoint=NativeLoaded");
            }
            catch (Exception error)
            {
                callbackFailure = error.ToString();
                AcceptLog.Warn("PRODUCTION_NATIVE_RESTORE_CHECKPOINT_FAILED " + callbackFailure);
            }
            finally { reloadArmed = false; }
        }

        private static IEnumerable<Wait> Restart(Step step)
        {
            Runner.Require(!editor && UiAcceptancePlugin.LastContext != null, "Restart requires the local skirmish fixture.");
            long before = UiAcceptancePlugin.StartedSession;
            previousSession = before; restartArmed = true; freshVerified = false; callbackFailure = null;
            MainViewModel.Instance.HUDmain.InGameOptions(null, null);
            yield return new Delay(300);
            MainViewModel.Instance.ButtonIngameMenuCommand.Execute("5");
            yield return new Delay(300);
            MainViewModel.Instance.ButtonConfirmationCommand.Execute("1");
            yield return new Until("new restart mission", () => GameSteps.InMission(before));
            Runner.Require(UiAcceptancePlugin.LastContext.IsRestart && UiAcceptancePlugin.LastContext.RestartedFromSessionId == before,
                "Restart did not use the linked mission lifecycle.");
            AcceptLog.Info("PRODUCTION_RESTART session=" + UiAcceptancePlugin.LastContext.SessionId + " from=" + before);
        }

        private static IEnumerable<Wait> ExpectFresh(Step step)
        {
            Runner.Require(freshVerified && callbackFailure == null, "Fresh native restart checkpoint failed: " + callbackFailure);
            AcceptLog.Info("PRODUCTION_FRESH_ROWS count=10000 empty=True checkpoint=NativeLoaded");
            yield break;
        }

        private static void RequireEmptyRows()
        {
            var bytes = new byte[10000 * 32]; Marshal.Copy(rows, bytes, 0, bytes.Length);
            for (int offset = 0; offset < bytes.Length; offset++)
                Runner.Require(bytes[offset] == 0, "A fresh restart inherited production state at unit " + offset / 32);
        }

        private static IEnumerable<Wait> Disable(Step step)
        {
            Runner.Require(GameSteps.MenuReady(), "Disable acceptance only after leaving the mission.");
            registration?.SetProfile(null);
            AcceptLog.Info("PRODUCTION_ACCEPTANCE_DISABLED");
            yield break;
        }

        private static object Property(object value, string name) => value.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value, null);
        private static byte[] Row(int id) { var bytes = new byte[32]; Marshal.Copy(rows + checked(id * 32), bytes, 0, bytes.Length); return bytes; }
        private static unsafe byte[] UnitRecord(GameUnit* unit) { var bytes = new byte[0x490]; Marshal.Copy((IntPtr)((byte*)unit - 4), bytes, 0, bytes.Length); return bytes; }
        private static ushort ReadU16(byte[] bytes, int offset) => BitConverter.ToUInt16(bytes, offset);
        private static ulong ReadU64(byte[] bytes, int offset) => BitConverter.ToUInt64(bytes, offset);
        private static string Fraction(byte[] row) => ReadU64(row, 8) + "/" + ReadU64(row, 16);
        private static bool Equal(byte[] first, byte[] second)
        { if (first.Length != second.Length) return false; for (int i = 0; i < first.Length; i++) if (first[i] != second[i]) return false; return true; }
    }
}
