using MessagePack;
using StartConditions;
using System;
using System.IO;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            string workspaceRoot = args != null && args.Length == 1
                ? Path.GetFullPath(args[0])
                : Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));

            TestSaveRoundTrip();
            TestSaveValidation();
            TestRepairFailureLogState();
            TestSourceContracts(workspaceRoot);
            Console.WriteLine("PASS: StartConditions AI start-troop isolation codec and source contracts.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + ex);
            return 1;
        }
    }

    private static void TestRepairFailureLogState()
    {
        var state = new RepairFailureLogState();
        Require(state.ShouldLog("failure-a"), "the first repair failure must be logged");
        Require(!state.ShouldLog("failure-a"), "an identical repair failure must be deduplicated");
        Require(state.ShouldLog("failure-b"), "a changed repair failure must be logged");
        Require(!state.ShouldLog("failure-b"), "the changed failure must then be deduplicated");
        state.MarkRecovered();
        Require(state.ShouldLog("failure-b"), "a failure after recovery must be logged again");
    }

    private static void TestSaveRoundTrip()
    {
        var records = new[]
        {
            new AIStartTroopIsolationSaveRecord(101, 2, 201),
            new AIStartTroopIsolationSaveRecord(102, 8, 202),
            new AIStartTroopIsolationSaveRecord(103, 4, 0),
        };

        byte[] bytes = AIStartTroopIsolationSaveState.Encode(records);
        AIStartTroopIsolationSaveState decoded = AIStartTroopIsolationSaveState.Decode(bytes);
        Require(decoded.SchemaVersion == AIStartTroopIsolationSaveState.SchemaVersionCurrent, "schema did not round-trip");
        Require(decoded.Records.Length == records.Length, "record count did not round-trip");
        for (int index = 0; index < records.Length; index++)
        {
            Require(decoded.Records[index].UnitGlobalId == records[index].UnitGlobalId, "unit global id did not round-trip");
            Require(decoded.Records[index].OwnerPlayerId == records[index].OwnerPlayerId, "owner did not round-trip");
            Require(decoded.Records[index].PrivateTribeGlobalId == records[index].PrivateTribeGlobalId, "tribe global id did not round-trip");
        }
    }

    private static void TestSaveValidation()
    {
        ExpectFailure(() => AIStartTroopIsolationSaveState.Encode(new[]
        {
            new AIStartTroopIsolationSaveRecord(0, 2, 1),
        }), "zero unit global id");
        ExpectFailure(() => AIStartTroopIsolationSaveState.Encode(new[]
        {
            new AIStartTroopIsolationSaveRecord(1, 0, 1),
        }), "invalid owner");
        ExpectFailure(() => AIStartTroopIsolationSaveState.Encode(new[]
        {
            new AIStartTroopIsolationSaveRecord(1, 2, 1),
            new AIStartTroopIsolationSaveRecord(1, 3, 2),
        }), "duplicate unit global id");
        ExpectFailure(() => AIStartTroopIsolationSaveState.Encode(new[]
        {
            new AIStartTroopIsolationSaveRecord(1, 2, 9),
            new AIStartTroopIsolationSaveRecord(2, 3, 9),
        }), "duplicate tribe global id");

        byte[] valid = AIStartTroopIsolationSaveState.Encode(new[]
        {
            new AIStartTroopIsolationSaveRecord(1, 2, 0),
            new AIStartTroopIsolationSaveRecord(2, 3, 0),
        });
        Require(AIStartTroopIsolationSaveState.Decode(valid).Records.Length == 2, "zero tribe ids must be repeatable while repair is pending");

        ExpectFailure(
            () => AIStartTroopIsolationSaveState.Decode(MessagePackSerializer.Serialize(new[] { 2 })),
            "unsupported schema");

        var trailing = new byte[valid.Length + 1];
        Buffer.BlockCopy(valid, 0, trailing, 0, valid.Length);
        trailing[trailing.Length - 1] = 0xc0;
        ExpectFailure(() => AIStartTroopIsolationSaveState.Decode(trailing), "trailing data");
        ExpectFailure(
            () => AIStartTroopIsolationSaveState.Decode(new byte[AIStartTroopIsolationSaveState.MaximumPayloadBytes + 1]),
            "oversized payload");

        var tooMany = new AIStartTroopIsolationSaveRecord[AIStartTroopIsolationSaveState.MaximumRecords + 1];
        for (int index = 0; index < tooMany.Length; index++)
            tooMany[index] = new AIStartTroopIsolationSaveRecord((uint)(index + 1), 2, 0);
        ExpectFailure(() => AIStartTroopIsolationSaveState.Encode(tooMany), "too many records");
    }

    private static void TestSourceContracts(string workspaceRoot)
    {
        string startTroops = File.ReadAllText(Path.Combine(
            workspaceRoot,
            "StartConditions",
            "src",
            "StartConditionsRuntime.StartTroops.cs"));
        string isolation = File.ReadAllText(Path.Combine(
            workspaceRoot,
            "StartConditions",
            "src",
            "StartConditionsRuntime.AIStartTroopIsolation.cs"));
        string diagnostics = File.ReadAllText(Path.Combine(
            workspaceRoot,
            "StartConditions",
            "src",
            "StartConditionsRuntime.AIStartTroopDiagnostics.cs"));
        string plugin = File.ReadAllText(Path.Combine(
            workspaceRoot,
            "StartConditions",
            "src",
            "StartConditionsPlugin.cs"));
        string mapLifecycle = File.ReadAllText(Path.Combine(
            workspaceRoot,
            "StartConditions",
            "src",
            "StartConditionsRuntime.MapLifecycle.cs"));
        string presetSupport = File.ReadAllText(Path.Combine(
            workspaceRoot,
            "APIShared",
            "src",
            "PresetLobbyModSettingsViewModel.cs"));
        string activationGate = File.ReadAllText(Path.Combine(
            workspaceRoot,
            "Shared",
            "GameplayModActivationGate.cs"));
        string normalizedPresetSupport = presetSupport.Replace(Environment.NewLine, "\n");

        RequireContains(startTroops, "long createdId = GameUnitManagerAPI.Instance.CreateUnitLocal(");
        RequireContains(startTroops, "bool isolateFromAI = GamePlayerManagerAPI.Instance.IsAIPlayer(playerId);");
        RequireContains(startTroops, "TryProtectSpawnedAIStartTroop(createdId, playerId, unitType)");
        RequireContains(startTroops, "RecordAIStartTroopDiagnosticBatch(");
        RequireContains(isolation, "private const ushort ProtectedAIBehaviourType = ushort.MaxValue;");
        RequireContains(isolation, "private const ushort ProtectedAIBehaviourRelatedValue = 0;");
        RequireContains(isolation, "GameTribeManagerAPI.Instance.UnassignUnit(tribeId, unitId)");
        RequireContains(isolation, "assignmentIssued = tribeApi.AssignUnit(privateTribeId, protectedTroop.UnitId);");
        RequireContains(isolation, "TribeStance.Aggressive");
        RequireContains(isolation, "OnTribeAssignUnit.Observable");
        RequireContains(isolation, "OnTribeDelete.Observable");
        RequireContains(isolation, "OnUnitDelete.Observable");
        RequireContains(isolation, "GameTimeManagerAPI.Instance.OnTick += OnAIStartTroopValidationTick;");
        RequireContains(isolation, "failed.PendingDeletion = true;");
        RequireContains(isolation, "if (protectedTroop.PendingDeletion)");
        RequireContains(isolation, "unit->r_GlobalId > int.MaxValue");
        RequireContains(isolation, "privateTribe->r_GlobalId > int.MaxValue");
        RequireContains(isolation, "DeleteExactProtectedAIStartTroopTribe(protectedTroop);");
        RequireContains(isolation, "TribeContainsOnlyProtectedUnit(tribe, protectedTroop.UnitId)");
        RequireContains(isolation, "DoesTribeMembershipIncludeUnit(tribe, unitId)");
        RequireContains(isolation, "tribe->r_UnitsInGroup < membersBefore");
        RequireContains(isolation, "unit->r_AITribeRole != ProtectedAIBehaviourType");
        RequireContains(isolation, "saved private tribe {saved.PrivateTribeGlobalId} has conflicting live state");
        RequireContains(isolation, "belongs to conflicting live tribe");
        RequireContains(isolation, "RepairFailureLog.ShouldLog(failureSignature)");
        RequireContains(isolation, "RepairFailureLog.MarkRecovered()");
        RequireContains(diagnostics, "AIStartTroopDiagnosticOffsets = { 0, 1, 10, 250 }");
        RequireContains(diagnostics, "int unitId = checked(spanIndex + 1);");
        RequireContains(diagnostics, "defensiveTotal={resources->N00004014}");
        RequireContains(diagnostics, "armySizeLimit={resources->N00003EF1}");
        RequireContains(diagnostics, "roleBuckets=[{units.RoleBuckets}]");
        RequireContains(diagnostics, "markerCorrect={units.MarkerCorrect}");
        RequireContains(diagnostics, "privateTribeCorrect={units.PrivateTribeCorrect}");
        RequireContains(diagnostics, "Shared.DebugLogHelper.IsCurrentNativeLibraryVersion()");
        RequireContains(startTroops, "LogError(\"AddStartTroops failed:\"");
        RequireContains(startTroops, "LogError(\"RunDelayedStartTroopProcessing failed:\"");
        RequireContains(mapLifecycle, "LogError(\"OnStartMap failed:\"");
        RequireContains(plugin, "logSuccess: false");
        RequireContains(plugin, "logRoutineActivity: false");
        RequireContains(plugin, "SerpLocalization.SetRoutineLoggingEnabled(false)");
        RequireContains(presetSupport, "internal static void Register(");
        RequireContains(normalizedPresetSupport, "xamlSourceFile,\n                true);");
        RequireContains(activationGate, "bool logRoutineActivity = true");
        Require(isolation.IndexOf("Protected spawned AI start troop", StringComparison.Ordinal) < 0,
            "per-unit protection success logs must remain disabled");
        Require(isolation.IndexOf("Created private aggressive AI start-troop tribe", StringComparison.Ordinal) < 0,
            "private-tribe success logs must remain disabled");
        Require(plugin.IndexOf("Goods localization diagnostics", StringComparison.Ordinal) < 0,
            "goods localization diagnostics must not be wired into the plugin logger");
        Require(isolation.IndexOf("OnUnitMoveHere", StringComparison.Ordinal) < 0, "movement commands must not be blocked");
        Require(isolation.IndexOf("OnTribeIssueOrder", StringComparison.Ordinal) < 0, "tribe orders must not be blocked");
        Require(isolation.IndexOf("r_TotalArmy", StringComparison.Ordinal) < 0, "total-army accounting must remain untouched");
    }

    private static void RequireContains(string source, string expected)
    {
        Require(source.IndexOf(expected, StringComparison.Ordinal) >= 0, "missing source contract: " + expected);
    }

    private static void ExpectFailure(Action action, string scenario)
    {
        try
        {
            action();
        }
        catch
        {
            return;
        }

        throw new InvalidOperationException("Expected failure for " + scenario + ".");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
