using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace EngineHarness
{
    internal sealed unsafe partial class FriendlyMoatMovementRuntime
    {
        private void SurfacePolicyAndPublicationTests()
        {
            byte Tile(uint flags, int building = 0, bool whitelist = false) => MovementSurfacePolicy.EncodeTile(flags, building, whitelist);
            bool Allowed(RouteSurfaceClass surface, uint flags, int building = 0, bool whitelist = false) =>
                MovementSurfacePolicy.Allows(surface, Tile(flags, building, whitelist));
            foreach (RouteSurfaceClass surface in Enum.GetValues(typeof(RouteSurfaceClass)))
            {
                Check(Allowed(surface, 0), "plain ground was excluded by a surface class");
                Check(Allowed(surface, 0x10000100, 1, true), "an explicitly walkable building was excluded");
            }
            Check(!Allowed(RouteSurfaceClass.LargeSearch, 0x100) && !Allowed(RouteSurfaceClass.RestrictedMovement, 0x100),
                "ordinary wall grants a restricted entry");
            Check(Allowed(RouteSurfaceClass.LargeSearch, 0x102) && Allowed(RouteSurfaceClass.RestrictedMovement, 0x102),
                "the native stockpile/no-building exception was lost");
            Check(Allowed(RouteSurfaceClass.LargeSearch, 0x102, 1) && !Allowed(RouteSurfaceClass.RestrictedMovement, 0x102, 1),
                "the movement validator's narrower stockpile exception was merged with LargeSearch");
            Check(Allowed(RouteSurfaceClass.LargeSearch, 0x10000002) && !Allowed(RouteSurfaceClass.RestrictedMovement, 0x10000002),
                "Goodsyard on an elevated surface lost the distinct native rules");
            Check(!MovementSurfacePolicy.Allows(RouteSurfaceClass.LargeSearch, 3) &&
                !MovementSurfacePolicy.Allows(RouteSurfaceClass.RestrictedMovement, 3),
                "unvalidated metadata grants surface permission");
            for (uint combinations = 0; combinations < 8; combinations++)
            for (int building = 0; building < 2; building++)
            for (int whitelist = 0; whitelist < 2; whitelist++)
            {
                uint flags = ((combinations & 1) != 0 ? 0x100u : 0) |
                    ((combinations & 2) != 0 ? 0x10000000u : 0) | ((combinations & 4) != 0 ? 2u : 0);
                Check(!Allowed(RouteSurfaceClass.RestrictedMovement, flags, building, whitelist != 0) ||
                    Allowed(RouteSurfaceClass.LargeSearch, flags, building, whitelist != 0),
                    "RestrictedMovement is not a subset of LargeSearch");
            }
            int[] restrictedTypes = { eChimps.CHIMP_TYPE_KNIGHT, eChimps.CHIMP_TYPE_LADDERMAN,
                eChimps.CHIMP_TYPE_CATAPULT, eChimps.CHIMP_TYPE_TREBUCHET, eChimps.CHIMP_TYPE_SIEGE_TOWER,
                eChimps.CHIMP_TYPE_BATTERING_RAM, eChimps.CHIMP_TYPE_PORTABLE_SHIELD, eChimps.CHIMP_TYPE_ARAB_HORSEMAN,
                eChimps.CHIMP_TYPE_ARAB_BALLISTA, eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER, eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL };
            foreach (int type in restrictedTypes)
            {
                Check(MovementSurfacePolicy.Resolve(type, 0, false) == RouteSurfaceClass.RestrictedMovement &&
                    MovementSurfacePolicy.Resolve(type, 1, false) == RouteSurfaceClass.RestrictedMovement,
                    "a restricted type loses its physical limitation through a changed search profile");
                Check(MovementSurfacePolicy.Resolve(type, 0, true) == RouteSurfaceClass.LargeSearch &&
                    MovementSurfacePolicy.Resolve(type, 1, true) == RouteSurfaceClass.Any,
                    "the independent SE surface bypass was ignored or granted more than the search profile allows");
            }
            Check(MovementSurfacePolicy.Resolve(eChimps.CHIMP_TYPE_MANGONEL, 0, false) == RouteSurfaceClass.LargeSearch &&
                MovementSurfacePolicy.Resolve(eChimps.CHIMP_TYPE_LORD, 1, false) == RouteSurfaceClass.Any,
                "surface resolution incorrectly treats every type as physically restricted");

            const int id = 97;
            GameUnit* unit = GameUnitManagerAPI.Instance.Units + id;
            GameUnit savedUnit = *unit;
            byte* manager = (byte*)nativePathManager;
            byte* path = nativeUnitManager + NativeUnitPathBufferOffset + id * NativeUnitPathBufferStride;
            byte* savedPath = *(byte**)(manager + PathManagerOutputBufferOffset);
            int savedLength = *(int*)(manager + PathManagerOutputLengthOffset);
            uint savedFlags = tileFlags[1013];
            var previousBuilder = originalPathBuilder; var previousReconstruction = originalPathReconstruction;
            bool savedNativeProbe = nativeManualProbe;
            bool savedReportSuppression = suppressNativeBarrierReports;
            try
            {
                suppressNativeBarrierReports = true;
                unit->r_UnitChimp = eChimps.CHIMP_TYPE_KNIGHT;
                unit->r_GlobalId = 90097; unit->r_AliveState = AliveState.IsAlive;
                unit->r_PathPlanStateBitFlags = 0; unit->r_MovementSubstep = 8;
                unit->r_CurrentTilePositionX = 10; unit->r_CurrentTilePositionY = 10;
                *(byte**)(manager + PathManagerOutputBufferOffset) = path;
                tileFlags[1013] = 0x100;
                int Publish()
                {
                    for (int i = 0; i < NativeUnitPathBufferStride; i++) path[i] = 0;
                    path[0] = path[1] = path[2] = 0x22; path[3] = 2;
                    *(int*)(manager + PathManagerOutputLengthOffset) = 7;
                    return 7;
                }
                Publish();
                Check(FilterPublishedMovementSurface(nativePathManager, 7, 0) == 0 &&
                    *(int*)(manager + PathManagerOutputLengthOffset) == 0 && new ReadOnlySpan<byte>(path, NativeUnitPathBufferStride).ToArray().All(b => b == 0),
                    "the final output guard published an unsuitable Vanilla fallback or retained stale output");
                tileFlags[1013] = 0x102;
                Publish();
                Check(FilterPublishedMovementSurface(nativePathManager, 7, 0) == 7,
                    "the output guard blocks the stockpile exception");
                nativeBuildingLayer[1013] = 1;
                *(short*)(movementSurfaceBuildingTypes + WeightedMoatRoutePlanner.NativeBuildingRecordStride) = (short)eStructs.STRUCT_GATE_MAIN;
                movementSurfaceBuildingWhitelist[(int)eStructs.STRUCT_GATE_MAIN] = 1;
                ulong allowedFingerprint = GetMovementSurfaceBuildingPolicyFingerprint();
                Check(IsMovementSurfaceAllowed(RouteSurfaceClass.RestrictedMovement, 1013),
                    "the live building whitelist does not grant its native passage exception");
                movementSurfaceBuildingWhitelist[(int)eStructs.STRUCT_GATE_MAIN] = 0;
                Check(!IsMovementSurfaceAllowed(RouteSurfaceClass.RestrictedMovement, 1013) &&
                    IsMovementSurfaceAllowed(RouteSurfaceClass.LargeSearch, 1013) &&
                    GetMovementSurfaceBuildingPolicyFingerprint() != allowedFingerprint,
                    "a live whitelist change is ignored or merges the two stockpile rules");
                nativeBuildingLayer[1013] = 0;
                *(short*)(movementSurfaceBuildingTypes + WeightedMoatRoutePlanner.NativeBuildingRecordStride) = 0;
                foreach (int malformed in new[] { 0, 1, 2, 3 })
                {
                    Publish();
                    if (malformed == 0) *(int*)(manager + 8) = 11;
                    if (malformed == 1) *(int*)(manager + PathManagerOutputLengthOffset) = 6;
                    if (malformed == 2) *(int*)(manager + 16) = 18;
                    if (malformed == 3) path[0] = 0x2F;
                    Check(FilterPublishedMovementSurface(nativePathManager, 7, 0) == 0,
                        "malformed bound output survives the final surface publication boundary");
                    *(int*)(manager + 8) = 10; *(int*)(manager + 16) = 17;
                }
                tileFlags[1013] = 0x100;
                Publish(); nativeManualProbe = true;
                Check(FilterPublishedMovementSurface(nativePathManager, 7, 0) == 7 && *(int*)(manager + PathManagerOutputLengthOffset) == 7,
                    "a read-only native probe is modified by surface publication validation");
                nativeManualProbe = false; nativeTraversalDisabled = true;
                Check(FilterPublishedMovementSurface(nativePathManager, 7, 0) == 7,
                    "disabled route core changes Vanilla output");
                nativeTraversalDisabled = false;
                *(byte**)(manager + PathManagerOutputBufferOffset) = path + 1;
                Check(FilterPublishedMovementSurface(nativePathManager, 7, 0) == 7 && path[0] == 0x22,
                    "a foreign/misaligned buffer is modified by the unit output guard");
                *(byte**)(manager + PathManagerOutputBufferOffset) = path;
                GamePathingManagerAPI.Instance.SurfaceBypasses[eChimps.CHIMP_TYPE_KNIGHT] = true;
                Publish();
                Check(FilterPublishedMovementSurface(nativePathManager, 7, 1) == 7,
                    "the output guard ignores a permitted profile and surface bypass");
                Publish();
                Check(FilterPublishedMovementSurface(nativePathManager, 7, 0) == 0,
                    "a surface bypass incorrectly bypasses the Large search rule");
                GamePathingManagerAPI.Instance.SurfaceBypasses[eChimps.CHIMP_TYPE_KNIGHT] = false;
                tileFlags[1013] = 0x102;
                tileFlags[1010] = 0x100;
                Publish();
                Check(FilterPublishedMovementSurface(nativePathManager, 7, 0) == 7,
                    "the output guard prevents leaving a restricted start");
                tileFlags[1010] = 0x8000;
                tileFlags[1013] = 0x100;
                int originalCalls = 0;
                nativeManualProbe = false;
                originalPathBuilder = (m, player, profile) => { originalCalls++; builderOriginalEntries++; return lastPathBuilderResult = Publish(); };
                Check(PathBuilderBarrier(nativePathManager, 1, 0) == 0 && originalCalls == 1,
                    "the final guard does not cover the native builder or calls the original twice");
                originalCalls = 0;
                originalPathBuilder = (m, player, profile) =>
                {
                    originalCalls++; builderOriginalEntries++; lastPathBuilderResult = Publish();
                    throw new InvalidOperationException("fixture: failure after original entry");
                };
                Check(PathBuilderBarrier(nativePathManager, 1, 0) == 0 && originalCalls == 1,
                    "an exception after native entry bypasses the guard or invokes native twice");
                originalCalls = 0;
                originalPathReconstruction = m => { originalCalls++; reconstructionOriginalEntries++; return lastPathReconstructionResult = Publish(); };
                Check(PathReconstructionBarrier(nativePathManager) == 0 && originalCalls == 1,
                    "reconstruction output bypasses surface validation or runs twice");
            }
            finally
            {
                *unit = savedUnit; tileFlags[1013] = savedFlags; tileFlags[1010] = 0x8000;
                nativeBuildingLayer[1013] = 0; movementSurfaceBuildingWhitelist[(int)eStructs.STRUCT_GATE_MAIN] = 0;
                *(short*)(movementSurfaceBuildingTypes + WeightedMoatRoutePlanner.NativeBuildingRecordStride) = 0;
                *(int*)(manager + 8) = 10; *(int*)(manager + 16) = 17;
                nativeTraversalDisabled = false;
                GamePathingManagerAPI.Instance.SurfaceBypasses[eChimps.CHIMP_TYPE_KNIGHT] = false;
                suppressNativeBarrierReports = savedReportSuppression;
                nativeManualProbe = savedNativeProbe; originalPathBuilder = previousBuilder; originalPathReconstruction = previousReconstruction;
                *(byte**)(manager + PathManagerOutputBufferOffset) = savedPath;
                *(int*)(manager + PathManagerOutputLengthOffset) = savedLength;
            }
            Console.WriteLine("PASS: actual surface policy, live profile/bypass, stockpile/building exceptions, final output and exception-barrier exactly-once behavior.");
        }

        [DllImport("PathfindingAnchor.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "mma_destroy")]
        private static extern void DestroySurfaceTestContext(IntPtr context);

        private void SurfaceGroupIntegrationTests()
        {
            int[] ids = { 97, 98, 99 };
            var units = GameUnitManagerAPI.Instance;
            var tribes = GameTribeManagerAPI.Instance;
            GameUnit[] saved = ids.Select(id => units.Units[id]).ToArray();
            GameTribe* oldTable = tribes.Table; GameTribe* oldTribe = tribes.Tribe; int oldSize = tribes.TableSize;
            IntPtr oldManager = nativeTribeManager; var oldLookup = getGroupUnitId;
            bool oldSingles = settings.AnchorSingleMoves;
            var profiles = APIShared.ScriptExtenderReplacements.GamePathingManagerAPI.Instance.Profiles;
            PathfindingProfile oldMangonel = profiles[eChimps.CHIMP_TYPE_MANGONEL];
            byte* records = (byte*)NativeMemory.AllocZeroed((nuint)(TribeRecordSize * 4 + 256));
            GameTribe tribe = new GameTribe { r_PlayerIdOwner = 1, r_AliveState = AliveState.IsAlive, r_GlobalId = 7703 };
            try
            {
                nativeTribeManager = (IntPtr)records;
                *(short*)(records + 3 * TribeRecordSize + TribeUnitCountOffset) = 3;
                getGroupUnitId = (manager, tribeId, ordinal) => ordinal < ids.Length ? ids[ordinal] : 0;
                tribes.Table = null; tribes.TableSize = 0; tribes.Tribe = &tribe;
                int[] types = { eChimps.CHIMP_TYPE_LORD, eChimps.CHIMP_TYPE_MANGONEL, eChimps.CHIMP_TYPE_KNIGHT };
                profiles[eChimps.CHIMP_TYPE_MANGONEL] = PathfindingProfile.Large;
                for (int i = 0; i < ids.Length; i++)
                {
                    GameUnit* unit = units.Units + ids[i];
                    unit->r_GlobalId = (uint)(9097 + i); unit->r_AliveState = AliveState.IsAlive;
                    unit->r_UnitChimp = types[i]; unit->r_TribeId = 3; unit->r_ControllableForPlayerId = 1; unit->Digger = false;
                    unit->r_CurrentTilePositionX = 140 + i; unit->r_CurrentTilePositionY = 10;
                    unit->r_MovementSubstep = 8; unit->r_PathPlanStateBitFlags = 0;
                }
                var specs = new AnchorTaskSpec[6];
                TryCaptureAnchorMoveSpecs(3, 160, 10, out var digger, out var ground, out var members, specs);
                Check(members.SequenceEqual(ids) && digger == null && ReferenceEquals(ground, specs[1]),
                    "mixed group capture loses members or their ordinary ground task");
                for (int surface = 0; surface < 3; surface++)
                    Check(specs[surface * 2 + 1] != null && specs[surface * 2 + 1].Surface == (RouteSurfaceClass)surface &&
                        specs[surface * 2 + 1].Starts.SequenceEqual(new[] { 10 * MapWidth + 140 + surface }),
                        "mixed group starts were combined across physical surface classes");
                ObserveAnchorFormation(3, ids, new uint[] { 9097, 9098, 9099 }, new[] { 160, 161, 162 }, new[] { 10, 10, 10 });
                BeginAnchorFrame(3, 0, 0, members, digger, ground, specs);
                try
                {
                    int[] tasks = new int[3];
                    var plans = new PlanScope[3];
                    for (int surface = 0; surface < 3; surface++)
                    {
                        tasks[surface] = ResolveAnchorTask(true, (RouteSurfaceClass)surface);
                        var plan = plans[surface] = new PlanScope(ids[surface], 160 + surface, 10);
                        Check(tasks[surface] > 0 && TryPrepareAnchorGround(plan, units.Units + ids[surface]) &&
                            plan.QualifiedRoute != null && plan.QualifiedRoute.SurfaceClass == (RouteSurfaceClass)surface,
                            "mixed formation member was not routed with its own surface class");
                    }
                    Check(tasks.Distinct().Count() == 3, "mixed surface classes reuse a task identity");
                    int changedTile = GameTileManagerAPI.Instance.GetTileId(155, 10);
                    uint oldFlags = tileFlags[changedTile]; ushort oldBuilding = nativeBuildingLayer[changedTile];
                    var oldRestricted = plans[2].QualifiedRoute;
                    long largeRevision = GetMovementSurfaceRevision(RouteSurfaceClass.LargeSearch);
                    long restrictedRevision = GetMovementSurfaceRevision(RouteSurfaceClass.RestrictedMovement);
                    try
                    {
                        Check(ReferenceEquals(GetReusableQualifiedRoute(plans[2], units.Units + ids[2]), oldRestricted) &&
                            IsAnchorAuditedRoute(oldRestricted), "a current restricted route is not reusable before surface changes");
                        // Goodsyard plus an unlisted building keeps Large allowed while only the
                        // stricter movement entry closes. Keep tick/placement/identity untouched.
                        tileFlags[changedTile] = 0x102; nativeBuildingLayer[changedTile] = 1;
                        MarkMovementSurface(10 * MapWidth + 155); RefreshMovementSurfaces(false);
                        Check(GetMovementSurfaceRevision(RouteSurfaceClass.RestrictedMovement) > restrictedRevision &&
                            GetMovementSurfaceRevision(RouteSurfaceClass.LargeSearch) == largeRevision,
                            "surface update did not invalidate exactly the affected class");
                        Check(GetReusableQualifiedRoute(plans[2], units.Units + ids[2]) == null && !IsAnchorAuditedRoute(oldRestricted),
                            "a route or audit memo is reused after its surface permissions changed in the same tick");
                        Check(ReferenceEquals(GetReusableQualifiedRoute(plans[0], units.Units + ids[0]), plans[0].QualifiedRoute) &&
                            ReferenceEquals(GetReusableQualifiedRoute(plans[1], units.Units + ids[1]), plans[1].QualifiedRoute),
                            "a Restricted-only metadata change invalidates unrelated Any/Large routes");
                    }
                    finally
                    {
                        tileFlags[changedTile] = oldFlags; nativeBuildingLayer[changedTile] = oldBuilding;
                        MarkMovementSurface(10 * MapWidth + 155); RefreshMovementSurfaces(false);
                    }
                }
                finally { EndAnchorFrame(3); }

                // No Any task exists in a cavalry-only group. It must still receive a
                // fast group route even when the independent single-move option is off.
                foreach (int id in ids) units.Units[id].r_UnitChimp = eChimps.CHIMP_TYPE_KNIGHT;
                specs = new AnchorTaskSpec[6];
                TryCaptureAnchorMoveSpecs(3, 160, 10, out digger, out ground, out members, specs);
                Check(digger == null && ground == null && specs[5] != null && specs[5].Starts.Length == 3,
                    "cavalry-only group creates an unrestricted task or loses starts");
                settings.AnchorSingleMoves = false;
                BeginAnchorFrame(3, 0, 0, members, digger, ground, specs);
                try
                {
                    var plan = new PlanScope(ids[0], 160, 10);
                    Check(TryPrepareAnchorGround(plan, units.Units + ids[0]) && plan.QualifiedRoute != null &&
                        plan.QualifiedRoute.SurfaceClass == RouteSurfaceClass.RestrictedMovement,
                        "cavalry-only group depends on an Any task or the single-move option");
                }
                finally { EndAnchorFrame(3); }
            }
            finally
            {
                for (int i = 0; i < ids.Length; i++) units.Units[ids[i]] = saved[i];
                profiles[eChimps.CHIMP_TYPE_MANGONEL] = oldMangonel;
                tribes.Table = oldTable; tribes.TableSize = oldSize; tribes.Tribe = oldTribe;
                nativeTribeManager = oldManager; getGroupUnitId = oldLookup; settings.AnchorSingleMoves = oldSingles;
                NativeMemory.Free(records);
            }
            Console.WriteLine("PASS: actual mixed-group capture, class-specific formation fields and cavalry-only routing with single routes disabled.");
        }

        private void SurfaceHazardTests(ref int ticks)
        {
            const int id = 97, fromX = 150, wallX = 151, targetX = 160;
            GameUnit* unit = GameUnitManagerAPI.Instance.Units + id;
            GameUnit before = *unit;
            int wallTile = GameTileManagerAPI.Instance.GetTileId(wallX, 10);
            uint savedFlags = tileFlags[wallTile];
            var available = new byte[25];
            for (int x = 138; x <= 162; x++) available[x - 138] = movementTargetAvailability[10 * MapWidth + x];
            bool wasBurning = fireTiles.Contains(10 * MapWidth + fromX);
            try
            {
                tileFlags[wallTile] = 0x100;
                MarkMovementSurface(10 * MapWidth + wallX); RefreshMovementSurfaces(false);
                var policy = new APIShared.Pathfinding.RouteReplanPolicy(3, 50, 10, 400);
                replanProbeLeft = ReplanProbeTickSettles;
                Check(CoreRouteProbe(1, fromX, 10, targetX, 10, policy, targetX - fromX, RouteSurfaceClass.Any) == ProbeClear,
                    "the hazard surface fixture lost its ordinary route");
                replanProbeLeft = ReplanProbeTickSettles;
                Check(CoreRouteProbe(1, fromX, 10, targetX, 10, policy, targetX - fromX, RouteSurfaceClass.RestrictedMovement) == ProbeNoRoute,
                    "a resume/return probe claims that a cavalry route across a wall is clear");
                unit->r_TribeId = 3;
                Check(ReplanGroupKey(unit, targetX, 10, RouteSurfaceClass.Any) !=
                    ReplanGroupKey(unit, targetX, 10, RouteSurfaceClass.RestrictedMovement),
                    "a mixed group shares a waiting decision between different surface classes");

                for (int x = 138; x <= 162; x++) movementTargetAvailability[10 * MapWidth + x] = 0;
                movementTargetAvailability[10 * MapWidth + wallX] = 1;
                fireTiles.Set(10 * MapWidth + fromX, true);
                unit->r_UnitChimp = eChimps.CHIMP_TYPE_LORD;
                Check(TryFindFireEscape(id, 1, fromX, 10, out int escapeX, out int escapeY) && escapeX == wallX && escapeY == 10,
                    "the hazard fixture's only unrestricted escape target is not found");
                unit->r_UnitChimp = eChimps.CHIMP_TYPE_KNIGHT;
                Check(!TryFindFireEscape(id, 1, fromX, 10, out _, out _),
                    "fire evacuation selects a target behind an impassable cavalry transition");
            }
            finally
            {
                *unit = before; tileFlags[wallTile] = savedFlags;
                fireTiles.Set(10 * MapWidth + fromX, wasBurning);
                for (int x = 138; x <= 162; x++) movementTargetAvailability[10 * MapWidth + x] = available[x - 138];
                MarkMovementSurface(10 * MapWidth + wallX); RefreshMovementSurfaces(false);
            }
            Console.WriteLine("PASS: production hazard escape and resume/return probes obey the unit surface class; mixed groups do not share incompatible wait decisions.");
        }

        // Executes the real native core over a directed graph with a four-step structure
        // shortcut and an eight-step ground alternative. This is the reported failure class;
        // no duplicate implementation of the native search serves as its oracle.
        private void SurfaceNativeTests()
        {
            const int width = 32, count = width * width, valid = 0x80;
            int Node(int x, int y) => y * width + x;
            int start = Node(10, 10), target = Node(14, 10), wall = Node(12, 10);
            var edges = new byte[count];
            void Corridor((int X, int Y)[] nodes)
            {
                for (int i = 1; i < nodes.Length; i++)
                {
                    var a = nodes[i - 1]; var b = nodes[i];
                    int d;
                    for (d = 0; d < 8; d++)
                        if (FastRouteField.Dx[d] == b.X - a.X && FastRouteField.Dy[d] == b.Y - a.Y) break;
                    Check(d < 8, "surface fixture contains a nonadjacent edge");
                    edges[Node(a.X, a.Y)] |= (byte)(1 << d);
                    edges[Node(b.X, b.Y)] |= (byte)(1 << ((d + 4) & 7));
                }
            }
            Corridor(new[] { (10, 10), (11, 10), (12, 10), (13, 10), (14, 10) });
            Corridor(new[] { (10, 10), (10, 11), (10, 12), (11, 12), (12, 12), (13, 12), (14, 12), (14, 11), (14, 10) });
            var words = new uint[count];
            for (int i = 0; i < count; i++) words[i] = (uint)(edges[i] | (edges[i] << 8));
            byte[] surfaces = Enumerable.Repeat((byte)(valid | 3), count).ToArray();
            surfaces[wall] = valid;
            Check(AnchorNative.mma_create(width, width, 128L * 1024 * 1024, out IntPtr context) == 0,
                "surface fixture context allocation failed");
            try
            {
                Check(AnchorNative.mma_mask_begin(context, 1) == 0 &&
                    AnchorNative.mma_mask_write(context, 1, 0, count, words, out _) == 0 &&
                    AnchorNative.mma_surface_write(context, 0, count, surfaces, out _) == 0,
                    "surface fixture mask or metadata initialization failed");
                var bytes = new byte[2000];
                (AnchorNative.QueryResult Result, byte[] Steps) Single(int surface, int from = -1, int to = -1)
                {
                    Check(AnchorNative.mma_single_route_limited(context, 1, to < 0 ? target : to, 1, 0,
                        (RouteSurfaceClass)surface, from < 0 ? start : from, 500000, bytes, bytes.Length, out var result) == 0,
                        "native surface single query failed");
                    return (result, bytes.Take(result.Length).ToArray());
                }
                bool Enters(byte[] steps, int forbidden, int from = -1)
                {
                    int node = from < 0 ? start : from;
                    foreach (byte direction in steps)
                    {
                        Check(direction < 8, "surface route contains an invalid direction");
                        node += FastRouteField.Dy[direction] * width + FastRouteField.Dx[direction];
                        if (node == forbidden) return true;
                    }
                    return false;
                }
                void CheckRoute(int surface, int length, bool entersWall)
                {
                    var answer = Single(surface);
                    Check(answer.Result.Status == AnchorNative.Ready && answer.Result.Length == length &&
                        Enters(answer.Steps, wall) == entersWall,
                        $"surface class {surface} chose an unsuitable route or lost the ground alternative");
                }
                CheckRoute(0, 4, true);
                CheckRoute(1, 8, false);
                CheckRoute(2, 8, false);

                int Task(int surface)
                {
                    Check(AnchorNative.mma_task_create(context, 1, target, 1, 0, (RouteSurfaceClass)surface, new[] { start }, 1, out int task) == 0 && task > 0,
                        "surface task allocation failed");
                    return task;
                }
                var tasks = new[] { Task(0), Task(1), Task(2) };
                Check(AnchorNative.mma_step(context, 3, 1, out int computed) == 0 && computed == 3,
                    "mixed-class fields did not compute independently");
                byte[][] oneThread = new byte[3][];
                for (int surface = 0; surface < 3; surface++)
                {
                    Check(AnchorNative.mma_query(context, tasks[surface], start, target, 8, bytes, bytes.Length, out var result) == 0 &&
                        result.Status == AnchorNative.Ready && result.Length == (surface == 0 ? 4 : 8) &&
                        Enters(bytes.Take(result.Length).ToArray(), wall) == (surface == 0),
                        "a cached group field leaked a route from another surface class");
                    oneThread[surface] = bytes.Take(result.Length).ToArray();
                }

                // Only LargeSearch permits this metadata. It represents the stricter
                // movement validator's deliberately narrower stockpile exception.
                Check(AnchorNative.mma_surface_update(context, new[] { wall }, new byte[] { valid | 1 }, 1, out int changed) == 0 && changed == 1,
                    "surface metadata update was not observed");
                Check(AnchorNative.mma_task_status(context, tasks[0]) == AnchorNative.Ready &&
                    AnchorNative.mma_task_status(context, tasks[1]) == AnchorNative.Stale &&
                    AnchorNative.mma_task_status(context, tasks[2]) == AnchorNative.Ready,
                    "surface changes did not invalidate their own class or invalidated an unaffected class");
                CheckRoute(0, 4, true); CheckRoute(1, 4, true); CheckRoute(2, 8, false);
                Check(AnchorNative.mma_surface_update(context, new[] { wall }, new byte[] { valid }, 1, out _) == 0,
                    "wall restoration failed");
                foreach (int task in tasks) Check(AnchorNative.mma_task_restart(context, task) == 0, "surface task restart failed");
                Check(AnchorNative.mma_step(context, 3, 4, out computed) == 0 && tasks.All(task => AnchorNative.mma_task_status(context, task) == AnchorNative.Ready),
                    "surface fields did not recompute with multiple workers");
                for (int surface = 0; surface < 3; surface++)
                {
                    var results = new AnchorNative.QueryResult[2]; var batch = new byte[4000];
                    Check(AnchorNative.mma_query_batch(context, tasks[surface], new[] { start, start }, new[] { target, target }, 2,
                        8, 4, batch, 2000, results) == 0 && results.All(r => r.Status == AnchorNative.Ready),
                        "surface batch query failed");
                    for (int i = 0; i < 2; i++)
                        Check(batch.Skip(i * 2000).Take(results[i].Length).SequenceEqual(oneThread[surface]),
                            "surface route depends on worker count or batch position");
                }

                // A forbidden start has outgoing edges to admissible tiles. Filtering
                // the start itself would trap units placed on structures by a map/editor.
                Check(AnchorNative.mma_surface_update(context, new[] { start }, new byte[] { valid }, 1, out _) == 0,
                    "restricted start setup failed");
                CheckRoute(2, 8, false);
                Check(AnchorNative.mma_surface_update(context, new[] { target }, new byte[] { valid }, 1, out _) == 0 &&
                    Single(2).Result.Status == AnchorNative.Unreachable,
                    "a restricted target can still be entered");
                Check(AnchorNative.mma_surface_update(context, new[] { target }, new byte[] { 3 }, 1, out _) == 0 &&
                    Single(1).Result.Status == AnchorNative.Unreachable && Single(2).Result.Status == AnchorNative.Unreachable,
                    "unvalidated metadata grants a restricted transition");
                CheckRoute(0, 4, true);

                // Restore the target and sever the only ground detour. The restricted
                // search must report no route rather than return the structure shortcut.
                Check(AnchorNative.mma_surface_update(context, new[] { target }, new byte[] { valid | 3 }, 1, out _) == 0,
                    "surface target restoration failed");
                int severed = Node(12, 12);
                Check(AnchorNative.mma_mask_update(context, 1, new[] { severed }, new uint[] { 0 }, 1, out _) == 0,
                    "surface ground detour removal failed");
                Check(Single(2).Result.Status == AnchorNative.Unreachable, "missing ground route falls back to the forbidden shortcut");
                CheckRoute(0, 4, true);
                foreach (int task in tasks) Check(AnchorNative.mma_task_release(context, task) == 0, "surface task release failed");
            }
            finally { DestroySurfaceTestContext(context); }
            Console.WriteLine("PASS: actual native surface search, separate group fields/cache, directed start escape, metadata invalidation, no-route and worker/batch determinism.");
        }
    }
}
