using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace EnemyBridgePathTest
{
    // Makes a lowered drawbridge that is coupled to a gatehouse behave like a raised one in
    // Vanilla's own topology, while the gate's portal record keeps it usable for everyone the
    // gate admits. Two permanent native detours, rooted by the static plugin runtime:
    //  * 0xC4BF0 (temporarily close/open/restore all gates). Inside every "close all" window
    //    (component rebuild 0xE49D0, AI siege field 0xD95E0, AI tactical 0x113BC0/E00/F60)
    //    the deck is cut from its surroundings and linked to the gate corridor. The window is
    //    restored byte-exactly before Vanilla reopens the gates.
    //  * 0xF4540 (per-tick portal refresh). Afterwards the region field of the gate endpoint
    //    that lies on such a deck is pointed at the region beyond the far deck end.
    // Effect: the deck and everything behind it is only reachable through the gate portal,
    // i.e. with the gate's owner/alliance/capture rules (0xE2610/0xE2F60).
    internal sealed unsafe class DrawbridgeDeckRuntime : IDeckGrid
    {
        // FBCB9319 hash-bound native layout; derivations in UpdateToNewDLL.md.
        internal const string NativeHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        private const int RvaGateClosure = 0xC4BF0, RvaPortalRefresh = 0xF4540, RvaAperture = 0xD8CE0;
        private const string GateClosurePrefix = "48896C24184889742420", PortalRefreshPrefix = "48895C240857", AperturePrefix = "48895C240848896C2418";
        private const int RvaBuildings = 0x64CCBB0, BuildingStride = 0x32C, BuildingCountOffset = 0x50;
        private const int RvaPathManager = 0x60AD660, PortalBase = 0x2024, PortalStride = 0x204, PortalLimit = 200;
        private const int RvaDirections = 0x51890D0, RvaComponents = 0x50EC690, RvaTileFlags = 0x48F71B0;
        private const int RvaStructures = 0x4B6AA50, RvaMoatTasks = 0x5F011A0, RvaDeckMapper = 0x2D1A30, RvaRowTable = 0x402FF2C;
        internal const int TileCount = 320800;
        private const uint RaisedDeckFlag = 0x40000000;
        // Native building record offsets (sentinel-relative id * stride).
        private const int OffAlive = 0x12C, OffType = 0x12E, OffX = 0x14A, OffY = 0x14C, OffSize = 0x154;
        private const int OffOwner = 0x132, OffCapturer = 0x322, OffVariation = 0x15E, OffOccupied = 0x224, OffGateState = 0x2FE, OffPortalId = 0x32E;
        private const short ApertureAxisVariation = 0x50;
        // Portal record offsets relative to entry base +0x2024.
        private const int PState = 0, PClass = 4, PBuilding = 0xC, PEntryTile = 0x24, PExitTile = 0x30, PCompA = 0x34, PCompB = 0x38, PCompC = 0x1E8;
        // The hooked 0xC4BF0 selects gates by (type - 0x2D) < 3: STRUCT_GATE_MAIN..STRUCT_GATE_WOOD.
        private const int NativeFirstGateType = 0x2D, NativeGateTypeCount = 3, NativeDrawbridgeType = 0x31;

        [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate void GateClosureDelegate(IntPtr buildings, int mode);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate void PortalRefreshDelegate(IntPtr pathManager);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)] internal delegate void ApertureDelegate(IntPtr pathManager, int buildingId);

        private readonly ManualLogSource log;
        private readonly List<IHook> permanent = new List<IHook>();
        private readonly List<Delegate> rooted = new List<Delegate>();
        private NativeDetour<GateClosureDelegate> closureHook;
        private NativeDetour<PortalRefreshDelegate> refreshHook;
        private ApertureDelegate aperture;
        private byte* module, buildings, pathManager, directions, tileFlags, deckMapper;
        private ushort* components, moatTasks;
        private short* structures;
        private GameTileManagerAPI tiles;

        private readonly DirectionJournal journal = new DirectionJournal(TileCount);
        private readonly int[] deckStamp = new int[TileCount];
        private int deckGeneration = 1;
        private readonly List<int> deckCells = new List<int>(32);
        private readonly List<Candidate> candidates = new List<Candidate>(16);
        private readonly List<int> cutBridges = new List<int>(16);
        private bool[] openEdges = new bool[32];
        private readonly Func<int, bool> isDeck, isOnBridge;
        private int currentBridge;
        private readonly Func<int, int> componentOf;
        private bool cutActive;
        private int cutThread;
        private volatile bool enabled = true;
        private volatile bool installed;

        // Counters are written on the simulation thread and only read for summaries.
        private long windows, cutWindows, bridgesCut, edgesRemoved, linksAdded, refreshes, portalWrites, failures;
        private long cutTicks, cutMaxTicks, patchTicks, patchMaxTicks, windowTicks, windowMaxTicks, windowOpenedAt;
        private static void Measure(long started, ref long total, ref long max)
        {
            long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - started;
            total += elapsed;
            if (elapsed > max) max = elapsed;
        }
        private static string Ms(long ticks) => (ticks * 1000.0 / System.Diagnostics.Stopwatch.Frequency).ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
        private int windowsSinceStart = -1, refreshesSinceStart = -1;
        private readonly object detailLock = new object();
        private readonly HashSet<long> loggedCuts = new HashSet<long>();
        private readonly HashSet<long> loggedPatches = new HashSet<long>();

        private struct Candidate { internal int Gate, Outside, Inside, Bridge, Abutment, NearDeck; internal bool NearToAbutment, AbutmentToNear; }

        internal DrawbridgeDeckRuntime(ManualLogSource log)
        {
            this.log = log;
            isDeck = IsDeckCell;
            isOnBridge = tile => (uint)tile < TileCount && currentBridge > 0 && structures[tile] == currentBridge;
            componentOf = tile => (uint)tile < TileCount ? components[tile] : 0;
        }

        internal bool Installed => installed;
        internal bool Enabled { get => enabled; set { enabled = value; PublishClosureState(); } }

        // WIP coupling for the Pathfinding work copy (branch wip/bridge-deck-mask): its per-player
        // deck entry mask is only consistent while this region closure is active.
        private const string ClosureStateKey = "SerpEnemyBridgeDeckClosureActive";
        private void PublishClosureState() => AppDomain.CurrentDomain.SetData(ClosureStateKey, enabled && installed);
        internal bool DetailLog { get; set; } = true;

        internal void Install(SHCDESE.API.LowLevel.CrusaderLibraryLoadContext context)
        {
            if (!string.Equals(Shared.DebugLogHelper.InstalledNativeSha256(), NativeHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("CrusaderDE.dll is not the audited build " + NativeHash + "; drawbridge closure stays inactive.");
            if ((int)eStructs.STRUCT_GATE_MAIN != NativeFirstGateType || (int)eStructs.STRUCT_GATE_WOOD != NativeFirstGateType + NativeGateTypeCount - 1 ||
                (int)eStructs.STRUCT_DRAWBRIDGE != NativeDrawbridgeType || (int)AliveState.IsAlive != 2)
                throw new InvalidOperationException("Extender gate/drawbridge enum values differ from the audited native contract.");
            module = (byte*)context.ModuleHandle;
            long length = context.Memory.Length;
            foreach (int rva in new[] { RvaGateClosure, RvaPortalRefresh, RvaAperture, RvaBuildings, RvaPathManager, RvaDirections,
                RvaComponents, RvaTileFlags, RvaStructures, RvaMoatTasks, RvaDeckMapper, RvaRowTable })
                if (rva <= 0 || rva >= length) throw new InvalidOperationException("Native address outside module: 0x" + rva.ToString("X"));
            RequirePrefix(RvaGateClosure, GateClosurePrefix);
            RequirePrefix(RvaPortalRefresh, PortalRefreshPrefix);
            RequirePrefix(RvaAperture, AperturePrefix);
            buildings = module + RvaBuildings; pathManager = module + RvaPathManager;
            directions = module + RvaDirections; tileFlags = module + RvaTileFlags; deckMapper = module + RvaDeckMapper;
            components = (ushort*)(module + RvaComponents); moatTasks = (ushort*)(module + RvaMoatTasks);
            structures = (short*)(module + RvaStructures);
            tiles = GameTileManagerAPI.Instance;
            // 0xD8CE0 and the gatehouse portal handlers address rows through the table at
            // 0x402FF2C (12-byte rows). Our coordinates come from the Extender: both must agree.
            for (int y = 1; y < 800; y += 37)
                if (tiles.GetTileId(0, y) != *(int*)(module + RvaRowTable + 12L * y))
                    throw new InvalidOperationException("Extender tile ids differ from the native row table at y=" + y + ".");
            aperture =Marshal.GetDelegateForFunctionPointer<ApertureDelegate>((IntPtr)(module + RvaAperture));
            rooted.Add(aperture);

            GateClosureDelegate closure = OnGateClosure;
            PortalRefreshDelegate refresh = OnPortalRefresh;
            rooted.Add(closure); rooted.Add(refresh);
            closureHook = Prepare(RvaGateClosure, GateClosurePrefix.Length / 2, closure, "gate-closure");
            refreshHook = Prepare(RvaPortalRefresh, PortalRefreshPrefix.Length / 2, refresh, "portal-refresh");
            // Published hooks are permanent; settings only change the logical state.
            foreach (IHook hook in permanent) hook.Enable();
            Validate(closureHook, RvaGateClosure, GateClosurePrefix.Length / 2, true);
            Validate(refreshHook, RvaPortalRefresh, PortalRefreshPrefix.Length / 2, true);
            installed = true;
            PublishClosureState();
            Shared.DebugLogHelper.LogInfo(log, "Drawbridge deck closure hooks installed: gateClosure=0xC4BF0, portalRefresh=0xF4540, scheme=Indirect, nativeSha256=" + NativeHash);
        }

        private void RequirePrefix(int rva, string hex)
        {
            for (int i = 0; i < hex.Length / 2; i++)
                if (module[rva + i] != Convert.ToByte(hex.Substring(i * 2, 2), 16))
                    throw new InvalidOperationException("Native entry 0x" + rva.ToString("X") + " differs from the audited bytes (another hook owner?).");
        }

        private NativeDetour<T> Prepare<T>(int rva, int displaced, T callback, string name) where T : Delegate
        {
            var backend = new NativeDetourBackend(new NativeDetourOptions
            {
                AllowedSchemes = DetourScheme.Indirect, FollowCalls = false, FollowJumps = false, Chaining = HookChainingPolicy.Refuse
            });
            var request = new DetourRequest<T> { Name = "EnemyBridgePathTest " + name, TargetAddress = (ulong)(module + rva), Callback = callback };
            var hook = backend.CreateDetour(in request) as NativeDetour<T>;
            if (hook == null) throw new InvalidOperationException("NativeX64 backend unavailable for " + name);
            try { Validate(hook, rva, displaced, false); }
            catch { ((IDisposable)hook).Dispose(); throw; } // only an unpublished candidate is released
            permanent.Add(hook);
            return hook;
        }

        private void Validate<T>(NativeDetour<T> hook, int rva, int displaced, bool published) where T : Delegate
        {
            ulong address = (ulong)(module + rva);
            if (hook.Scheme != DetourScheme.Indirect || hook.TargetAddress != address || hook.DisplacedByteCount != displaced ||
                hook.IsInstalled != published || hook.TrampolineAddress == IntPtr.Zero || hook.PointerSlot == IntPtr.Zero ||
                hook.HookEntryPointAddress == IntPtr.Zero)
                throw new InvalidOperationException("NativeX64 detour contract changed at 0x" + rva.ToString("X") + ": scheme=" + hook.Scheme +
                    ",displaced=" + hook.DisplacedByteCount + "/" + displaced + ",installed=" + hook.IsInstalled);
            if (!published) return;
            byte* target = module + rva;
            if (target[0] != 0xFF || target[1] != 0x25 || (IntPtr)(target + 6 + *(int*)(target + 2)) != hook.PointerSlot ||
                *(long*)hook.PointerSlot != hook.HookEntryPointAddress.ToInt64())
                throw new InvalidOperationException("NativeX64 Indirect patch differs at 0x" + rva.ToString("X"));
        }

        // ---- 0xC4BF0 -------------------------------------------------------------------------

        private void OnGateClosure(IntPtr manager, int mode)
        {
            // Any further gate transition first ends our window, so Vanilla always reopens
            // the gates on the unmodified grid (mode 0 is the only observed successor).
            if (cutActive) RestoreCut();
            // Diagnostic: total time Vanilla spends inside "all gates closed" windows (its AI
            // planning floods and the component rebuild), to separate behaviour from hook cost.
            long now = System.Diagnostics.Stopwatch.GetTimestamp();
            if (mode != 1 && windowOpenedAt != 0) { long span = now - windowOpenedAt; windowTicks += span; if (span > windowMaxTicks) windowMaxTicks = span; }
            windowOpenedAt = mode == 1 ? now : 0;
            closureHook.Original(manager, mode);
            windows++;
            if (windowsSinceStart == 0) { windowsSinceStart = 1; Shared.DebugLogHelper.LogInfo(log, "Drawbridge deck closure: first gate-closure window after map start (runtime confirmed)."); }
            if (mode != 1 || !enabled || !installed || (byte*)manager != buildings) return; // inert unless fully installed and validated
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            try { ApplyCut(); }
            catch (Exception error)
            {
                failures++;
                try { if (cutActive) RestoreCut(); } catch { }
                Shared.DebugLogHelper.LogErrorLimited(log, "deck-cut", "Drawbridge deck cut failed; window restored: " + error);
            }
            Measure(started, ref cutTicks, ref cutMaxTicks);
        }

        private void RestoreCut()
        {
            if (cutThread != Thread.CurrentThread.ManagedThreadId)
                Shared.DebugLogHelper.LogErrorLimited(log, "deck-thread", "Drawbridge deck window restored on another thread than it was opened.");
            journal.Restore(this);
            cutActive = false;
        }

        private void ApplyCut()
        {
            candidates.Clear();
            int count = *(int*)(buildings + BuildingCountOffset);
            for (int gate = 1; gate < count; gate++)
            {
                byte* record = buildings + (long)gate * BuildingStride;
                if (*(short*)(record + OffAlive) == 0 || !IsGateType(*(ushort*)(record + OffType)) || !HasOwnPortal(gate, record)) continue;
                for (int side = 0; side < 2; side++)
                    if (TryAperture(record, side, out int outside, out int inside) && TryLoweredBridge(outside, out int bridge) &&
                        DeckClosureCore.FindNearDeck(this, outside, inside, isDeck, isOnBridge, out int abutment, out int nearDeck))
                    {
                        var c = new Candidate { Gate = gate, Outside = outside, Inside = inside, Bridge = bridge, Abutment = abutment, NearDeck = nearDeck };
                        // The abutment row stays joined to the deck: remember its original edges before any cut.
                        if (abutment >= 0 && TryGetCoordinates(nearDeck, out int nx, out int ny) && TryGetCoordinates(abutment, out int ax, out int ay))
                        {
                            int d = DirectionBits.FromDelta(ax - nx, ay - ny);
                            c.NearToAbutment = d >= 0 && (directions[nearDeck] & DirectionBits.Bit[d]) != 0;
                            c.AbutmentToNear = d >= 0 && (directions[abutment] & DirectionBits.Bit[DirectionBits.Opposite(d)]) != 0;
                        }
                        candidates.Add(c);
                    }
            }
            if (candidates.Count == 0) return;

            cutActive = true; cutThread = Thread.CurrentThread.ManagedThreadId; cutWindows++;
            // 1) Vanilla's own open-gate edge values for each aperture (state 0, then back to 2).
            // Candidates of one gate are consecutive because the scan runs gate by gate.
            if (openEdges.Length < candidates.Count * 2) openEdges = new bool[candidates.Count * 4];
            bool[] open = openEdges;
            Array.Clear(open, 0, candidates.Count * 2);
            for (int i = 0; i < candidates.Count; i++)
                if (i == 0 || candidates[i].Gate != candidates[i - 1].Gate) EvaluateGate(candidates[i].Gate, i, open);
            // 2) Cut every deck once, 3) link it to its gate corridor.
            cutBridges.Clear();
            for (int i = 0; i < candidates.Count; i++)
            {
                Candidate c = candidates[i];
                LoadDeck(c.Bridge);
                int removed = 0;
                if (!cutBridges.Contains(c.Bridge)) { cutBridges.Add(c.Bridge); bridgesCut++; removed = DeckClosureCore.CutDeck(this, deckCells, isDeck, journal); edgesRemoved += removed; }
                int linked = DeckClosureCore.Link(this, c.Outside, c.Inside, open[i * 2], open[i * 2 + 1], journal);
                if (c.Abutment >= 0) linked += DeckClosureCore.Link(this, c.NearDeck, c.Abutment, c.NearToAbutment, c.AbutmentToNear, journal);
                linksAdded += linked;
                if (DetailLog) LogCut(c, removed, linked, open[i * 2], open[i * 2 + 1]);
            }
        }

        private void EvaluateGate(int gate, int first, bool[] open)
        {
            byte* record = buildings + (long)gate * BuildingStride;
            byte* state = record + OffGateState;
            // Snapshot every aperture byte of this gate to prove the second call restores them.
            Span<int> apertureTiles = stackalloc int[4];
            Span<byte> before = stackalloc byte[4];
            int n = 0;
            for (int side = 0; side < 2; side++)
                if (TryAperture(record, side, out int outside, out int inside)) { apertureTiles[n++] = outside; apertureTiles[n++] = inside; }
            for (int i = 0; i < n; i++) before[i] = directions[apertureTiles[i]];
            byte saved = *state;
            *state = 0;
            try
            {
                aperture((IntPtr)pathManager, gate);
                for (int i = first; i < candidates.Count && candidates[i].Gate == gate; i++) EvaluateOpenEdge(candidates[i], open, i);
            }
            finally
            {
                *state = saved;
                aperture((IntPtr)pathManager, gate);
            }
            for (int i = 0; i < n; i++)
                if (directions[apertureTiles[i]] != before[i])
                {
                    directions[apertureTiles[i]] = before[i];
                    Shared.DebugLogHelper.LogErrorLimited(log, "aperture-restore", "Gate " + gate + " aperture byte did not return to its closed value; restored explicitly.");
                }
        }

        // Called only while EvaluateGate holds the gate in its open state.
        private void EvaluateOpenEdge(Candidate c, bool[] open, int index)
        {
            if (!TryGetCoordinates(c.Outside, out int ox, out int oy) || !TryGetCoordinates(c.Inside, out int ix, out int iy)) return;
            int d = DirectionBits.FromDelta(ix - ox, iy - oy);
            if (d < 0) return;
            open[index * 2] = (directions[c.Outside] & DirectionBits.Bit[d]) != 0;
            open[index * 2 + 1] = (directions[c.Inside] & DirectionBits.Bit[DirectionBits.Opposite(d)]) != 0;
        }

        // ---- 0xF4540 -------------------------------------------------------------------------

        private void OnPortalRefresh(IntPtr manager)
        {
            refreshHook.Original(manager);
            refreshes++;
            if (refreshesSinceStart == 0) { refreshesSinceStart = 1; Shared.DebugLogHelper.LogInfo(log, "Drawbridge deck closure: first portal refresh after map start (runtime confirmed)."); }
            if (!enabled || !installed || (byte*)manager != pathManager) return;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            try { PatchPortals(); }
            catch (Exception error) { failures++; Shared.DebugLogHelper.LogErrorLimited(log, "portal-patch", "Drawbridge portal patch failed: " + error); }
            Measure(started, ref patchTicks, ref patchMaxTicks);
        }

        private void PatchPortals()
        {
            int top = Math.Min(*(int*)pathManager, PortalLimit);
            for (int id = 1; id < top; id++)
            {
                byte* portal = pathManager + PortalBase + (long)id * PortalStride;
                if (*(int*)(portal + PState) == 0) continue;
                int cls = *(int*)(portal + PClass);
                if (cls != (int)PathConnectionClass.GatehouseBig && cls != (int)PathConnectionClass.GatehouseSmall) continue;
                int gate = *(int*)(portal + PBuilding);
                if (gate <= 0 || gate >= *(int*)(buildings + BuildingCountOffset)) continue;
                byte* record = buildings + (long)gate * BuildingStride;
                if (*(short*)(record + OffAlive) == 0 || !IsGateType(*(ushort*)(record + OffType))) continue;
                PatchEndpoint(id, portal, record, gate, PEntryTile, PCompA);
                PatchEndpoint(id, portal, record, gate, PExitTile, PCompB);
            }
        }

        private void PatchEndpoint(int id, byte* portal, byte* record, int gate, int tileOffset, int componentOffset)
        {
            int outside = *(int*)(portal + tileOffset);
            if ((uint)outside >= TileCount) return;
            int inside = -1;
            for (int side = 0; side < 2 && inside < 0; side++)
                if (TryAperture(record, side, out int o, out int i) && o == outside) inside = i;
            if (inside < 0) return;
            // Only our closure window can put the deck tile into the gate corridor component.
            int deckComponent = components[outside];
            if (deckComponent == 0 || deckComponent != components[inside] || !TryLoweredBridge(outside, out int bridge)) return;
            int landing = DeckClosureCore.FindLanding(this, outside, inside, isOnBridge, componentOf);
            if (landing <= 0) return;
            int* field = (int*)(portal + componentOffset);
            if (*field == landing) return;
            int old = *field;
            *field = landing;
            portalWrites++;
            if (DetailLog) LogPatch(id, gate, bridge, outside, inside, old, landing, *(int*)(portal + PCompC));
        }

        // ---- geometry ------------------------------------------------------------------------

        private static bool IsGateType(ushort type) => (uint)(type - NativeFirstGateType) < NativeGateTypeCount;

        private bool HasOwnPortal(int gate, byte* record)
        {
            int id = *(short*)(record + OffPortalId);
            if (id <= 0 || id >= PortalLimit) return false;
            byte* portal = pathManager + PortalBase + (long)id * PortalStride;
            int cls = *(int*)(portal + PClass);
            return *(int*)(portal + PState) != 0 && *(int*)(portal + PBuilding) == gate &&
                (cls == (int)PathConnectionClass.GatehouseBig || cls == (int)PathConnectionClass.GatehouseSmall);
        }

        // The two edges 0xD8CE0 opens/closes: outside tile and the gate tile behind it.
        private bool TryAperture(byte* record, int side, out int outside, out int inside)
        {
            outside = inside = -1;
            int x = *(short*)(record + OffX), y = *(short*)(record + OffY), size = *(int*)(record + OffSize);
            if (size <= 0 || size > 16) return false;
            int ox, oy, ix, iy;
            if (*(short*)(record + OffVariation) != ApertureAxisVariation)
            {
                int column = x + size / 2;
                ox = ix = column;
                if (side == 0) { oy = y - 1; iy = y; } else { oy = y + size; iy = y + size - 1; }
            }
            else
            {
                int row = y + size / 2;
                oy = iy = row;
                if (side == 0) { ox = x - 1; ix = x; } else { ox = x + size; ix = x + size - 1; }
            }
            return TryGetTile(ox, oy, out outside) && TryGetTile(ix, iy, out inside);
        }

        // A live drawbridge whose footprint contains the tile and whose moat deck is not raised.
        // Loads that bridge's deck as the current one.
        private bool TryLoweredBridge(int tile, out int bridge)
        {
            bridge = structures[tile];
            if (bridge <= 0 || bridge >= *(int*)(buildings + BuildingCountOffset)) return false;
            byte* record = buildings + (long)bridge * BuildingStride;
            if (*(short*)(record + OffAlive) != (short)AliveState.IsAlive || *(ushort*)(record + OffType) != NativeDrawbridgeType) return false;
            LoadDeck(bridge);
            if (deckCells.Count == 0) return false;
            foreach (int cell in deckCells)
                if ((*(uint*)(tileFlags + (long)cell * 4) & RaisedDeckFlag) != 0) return false;
            return true;
        }

        // Exactly the cells 0x645C0 would flag when raising: mapper cell with a moat task index.
        private void LoadDeck(int bridge)
        {
            if (++deckGeneration == int.MaxValue) { Array.Clear(deckStamp, 0, deckStamp.Length); deckGeneration = 1; }
            deckCells.Clear();
            currentBridge = bridge;
            byte* record = buildings + (long)bridge * BuildingStride;
            int orientation = *(short*)(record + OffVariation);
            if (orientation < 0 || orientation > 6 || (orientation & 1) != 0) return;
            int* mapper = (int*)(deckMapper + (orientation / 2) * 100);
            uint* occupied = (uint*)(record + OffOccupied);
            for (int i = 0; i < 25; i++)
            {
                uint tile = occupied[i];
                if (mapper[i] == 0 || tile >= TileCount || moatTasks[tile] == 0) continue;
                deckStamp[tile] = deckGeneration;
                deckCells.Add((int)tile);
            }
        }

        private bool IsDeckCell(int tile) => (uint)tile < TileCount && deckStamp[tile] == deckGeneration;

        // ---- IDeckGrid -----------------------------------------------------------------------

        public byte GetDirections(int tile) => directions[tile];
        public void SetDirections(int tile, byte value) => directions[tile] = value;
        public bool TryGetCoordinates(int tile, out int x, out int y)
        {
            x = y = 0;
            if ((uint)tile >= TileCount) return false;
            var v = tiles.GetTileVectorFromId(tile);
            x = v.X; y = v.Y;
            return tiles.IsTileInsideMapBounds(x, y) && tiles.GetTileId(x, y) == tile;
        }
        public bool TryGetTile(int x, int y, out int tile)
        {
            tile = -1;
            if (!tiles.IsTileInsideMapBounds(x, y)) return false;
            tile = tiles.GetTileId(x, y);
            return (uint)tile < TileCount;
        }

        // ---- acceptance views (call under the engine monitor) -------------------------------

        internal sealed class DeckReport
        {
            internal int Gate, GateOwner, GateCaptured, Bridge, Portal, PortalSide, FrontTile, GateTile, LandingTile, InnerTile = -1;
            internal int DeckRegion, FrontRegion, GateRegion, LandingRegion, EndpointField, OtherField, CenterField;
            internal bool Raised;
            internal readonly List<int> DeckTiles = new List<int>();
        }

        // Every gate aperture that opens onto a drawbridge deck, lowered or raised.
        internal List<DeckReport> DescribeDecks()
        {
            var result = new List<DeckReport>();
            if (!installed) return result;
            int count = *(int*)(buildings + BuildingCountOffset);
            for (int gate = 1; gate < count; gate++)
            {
                byte* record = buildings + (long)gate * BuildingStride;
                if (*(short*)(record + OffAlive) == 0 || !IsGateType(*(ushort*)(record + OffType)) || !HasOwnPortal(gate, record)) continue;
                int portalId = *(short*)(record + OffPortalId);
                byte* portal = pathManager + PortalBase + (long)portalId * PortalStride;
                for (int side = 0; side < 2; side++)
                {
                    if (!TryAperture(record, side, out int outside, out int inside)) continue;
                    int bridge = structures[outside];
                    if (bridge <= 0 || bridge >= count) continue;
                    byte* bridgeRecord = buildings + (long)bridge * BuildingStride;
                    if (*(ushort*)(bridgeRecord + OffType) != NativeDrawbridgeType || *(short*)(bridgeRecord + OffAlive) != (short)AliveState.IsAlive) continue;
                    LoadDeck(bridge);
                    if (deckCells.Count == 0 || !DeckClosureCore.FindNearDeck(this, outside, inside, isDeck, isOnBridge, out _, out _)) continue;
                    bool raised = false;
                    foreach (int cell in deckCells) raised |= (*(uint*)(tileFlags + (long)cell * 4) & RaisedDeckFlag) != 0;
                    var report = new DeckReport
                    {
                        Gate = gate, GateOwner = *(short*)(record + OffOwner), GateCaptured = *(short*)(record + OffCapturer), Bridge = bridge, Portal = portalId,
                        FrontTile = outside, GateTile = inside, DeckRegion = components[deckCells[deckCells.Count / 2]], FrontRegion = components[outside], GateRegion = components[inside],
                        Raised = raised, CenterField = *(int*)(portal + PCompC)
                    };
                    report.DeckTiles.AddRange(deckCells);
                    if (TryAperture(record, 1 - side, out int innerOutside, out _)) report.InnerTile = innerOutside;
                    report.LandingRegion = DeckClosureCore.FindLanding(this, outside, inside, isOnBridge, componentOf, out report.LandingTile);
                    if (*(int*)(portal + PEntryTile) == outside) { report.PortalSide = 0; report.EndpointField = *(int*)(portal + PCompA); report.OtherField = *(int*)(portal + PCompB); }
                    else if (*(int*)(portal + PExitTile) == outside) { report.PortalSide = 1; report.EndpointField = *(int*)(portal + PCompB); report.OtherField = *(int*)(portal + PCompA); }
                    else report.PortalSide = -1;
                    result.Add(report);
                }
            }
            return result;
        }

        // Raw inventory of every gate and drawbridge for acceptance diagnostics.
        internal List<string> DescribeStructures()
        {
            var lines = new List<string>();
            if (!installed) return lines;
            int count = *(int*)(buildings + BuildingCountOffset);
            lines.Add("buildingCount=" + count);
            for (int id = 1; id < count; id++)
            {
                byte* record = buildings + (long)id * BuildingStride;
                ushort type = *(ushort*)(record + OffType);
                if (!IsGateType(type) && type != NativeDrawbridgeType) continue;
                string text = (type == NativeDrawbridgeType ? "bridge" : "gate") + " id=" + id + " type=" + type + " alive=" + *(short*)(record + OffAlive) +
                    " owner=" + *(short*)(record + OffOwner) + " xy=" + *(short*)(record + OffX) + "/" + *(short*)(record + OffY) +
                    " size=" + *(int*)(record + OffSize) + " variation=" + *(short*)(record + OffVariation) + " state=" + record[OffGateState];
                if (type == NativeDrawbridgeType)
                {
                    LoadDeck(id);
                    var cells = new List<string>();
                    uint* occupied = (uint*)(record + OffOccupied);
                    for (int i = 0; i < 25; i++)
                    {
                        uint tile = occupied[i];
                        TryGetCoordinates((int)tile, out int x, out int y);
                        cells.Add(i + ":" + x + "/" + y + (tile < TileCount ? "m" + moatTasks[tile] + (((*(uint*)(tileFlags + (long)tile * 4)) & RaisedDeckFlag) != 0 ? "R" : "-") : "?"));
                    }
                    text += " deckCells=" + deckCells.Count + " occupied=[" + string.Join(" ", cells) + "]";
                }
                else
                {
                    int portalId = *(short*)(record + OffPortalId);
                    text += " portal=" + portalId + " ownPortal=" + HasOwnPortal(id, record);
                    for (int side = 0; side < 2; side++)
                        if (TryAperture(record, side, out int outside, out int inside))
                        {
                            TryGetCoordinates(outside, out int ox, out int oy); TryGetCoordinates(inside, out int ix, out int iy);
                            text += " aperture" + side + "=" + ox + "/" + oy + "->" + ix + "/" + iy + " structureAtFront=" + structures[outside] +
                                " region=" + components[outside] + "/" + components[inside];
                        }
                        else text += " aperture" + side + "=invalid";
                }
                lines.Add(text);
            }
            return lines;
        }

        internal static int RegionOf(int tile) => (uint)tile < TileCount ? GamePathingManagerAPI.Instance.GetPathComponentGrid()[tile] : 0;

        // ---- diagnostics ---------------------------------------------------------------------

        internal void BeginSession() { windowsSinceStart = 0; refreshesSinceStart = 0; lock (detailLock) { loggedCuts.Clear(); loggedPatches.Clear(); } }

        internal string Summary() => "windows=" + windows + ",cutWindows=" + cutWindows + ",bridgeCuts=" + bridgesCut + ",edgesRemoved=" + edgesRemoved +
            ",linksAdded=" + linksAdded + ",portalRefreshes=" + refreshes + ",portalWrites=" + portalWrites + ",failures=" + failures + ",enabled=" + enabled +
            ",cutMs=" + Ms(cutTicks) + ",cutMaxMs=" + Ms(cutMaxTicks) + ",patchMs=" + Ms(patchTicks) + ",patchMaxMs=" + Ms(patchMaxTicks) +
            ",closedWindowMs=" + Ms(windowTicks) + ",closedWindowMaxMs=" + Ms(windowMaxTicks) + NativeCounters();

        // Vanilla's own counters: region queries 0xE2610 (+0xC4) and 0xE2F60 (+0xC8), rebuild generation (+0x74).
        private string NativeCounters() => pathManager == null ? "" :
            ",regionQueries=" + *(int*)(pathManager + 0xC4) + ",regionQueriesNoLadder=" + *(int*)(pathManager + 0xC8) + ",rebuilds=" + *(int*)(pathManager + 0x74);

        private void LogCut(Candidate c, int removed, int linked, bool outsideToInside, bool insideToOutside)
        {
            long key = ((long)c.Gate << 32) | (uint)c.Bridge;
            lock (detailLock) if (!loggedCuts.Add(key)) return;
            TryGetCoordinates(c.Outside, out int ox, out int oy); TryGetCoordinates(c.Inside, out int ix, out int iy);
            Shared.DebugLogHelper.LogInfo(log, "Drawbridge deck closed for planning: gate=" + c.Gate + ",bridge=" + c.Bridge + ",front=" + ox + "/" + oy +
                ",gateTile=" + ix + "/" + iy + ",deckCells=" + deckCells.Count + ",edgesRemoved=" + removed + ",linkEdges=" + linked +
                ",openEdges=" + outsideToInside + "/" + insideToOutside);
        }

        private void LogPatch(int portal, int gate, int bridge, int outside, int inside, int old, int landing, int corridor)
        {
            long key = ((long)portal << 32) | (uint)outside;
            lock (detailLock) if (!loggedPatches.Add(key)) return;
            Shared.DebugLogHelper.LogInfo(log, "Drawbridge portal endpoint redirected: portal=" + portal + ",gate=" + gate + ",bridge=" + bridge +
                ",deckRegion=" + components[outside] + ",gateTileRegion=" + components[inside] + ",portalC=" + corridor + ",oldEndpoint=" + old + ",landingRegion=" + landing);
        }
    }
}
