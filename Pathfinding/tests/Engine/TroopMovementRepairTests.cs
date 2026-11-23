using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class TroopMovementRepairTests
{
    internal static void Run(string root)
    {
        string source = File.ReadAllText(Path.Combine(root, "Pathfinding/src/TroopMovement/TroopMovementFixRuntime.cs"));
        var declarations = CSharpSyntaxTree.ParseText(source).GetRoot().DescendantNodes();
        string[] methods = { "CaptureRepair", "PrepareRepair", "CommitRepair", "MatchesLiveTribe" };
        var production = declarations.OfType<MethodDeclarationSyntax>()
            .Where(method => methods.Contains(method.Identifier.Text)).Select(method => method.ToFullString())
            .Concat(declarations.OfType<FieldDeclarationSyntax>().Where(field =>
                field.Declaration.Variables.Any(variable => variable.Identifier.Text is "RepairEntryBytes" or "RepairRecordBytes"))
                .Select(field => field.ToFullString()))
            .Append(declarations.OfType<StructDeclarationSyntax>().Single(type => type.Identifier.Text == "TribeSynchronization").ToFullString());

        string fixture = """
using System;
using System.Collections.Generic;
using System.IO;

public static unsafe class TroopRepairFixture
{
    private enum SynchronizedMovementCadence : byte { Walking, Running }
    private enum AliveState { None, IsAlive }
    private struct GameTribe { internal uint r_GlobalId; internal ushort r_PlayerIdOwner; internal AliveState r_AliveState; }
    private static class GamePlayerManagerAPI { internal const int MAX_PLAYERS = 8; }
    private sealed class SynchronizedMovementCadencePatch
    {
        internal const int MaximumTrackedTribeId = 4500;
        internal readonly Dictionary<int, ushort> Entries = new();
        internal void ClearAllSynchronization() => Entries.Clear();
        internal void SetSynchronization(int slot, SynchronizedMovementCadence mode, ushort bonus) => Entries.Add(slot, bonus);
    }

    private sealed class Runtime
    {
        private readonly GameTribe* live;
        private Dictionary<int, TribeSynchronization> synchronizationByTribeId = new();
        private readonly SynchronizedMovementCadencePatch cadencePatch = new();
        private bool IsFeatureEnabled => true;
        private Runtime(GameTribe* live) { this.live = live; }
        private static GameTribe* current;
        private static bool TryGetTribe(int slot, out GameTribe* tribe)
        {
            tribe = slot is 1 or 2 ? current + slot : null;
            return tribe != null;
        }
        PRODUCTION_MEMBERS

        internal static void Run(GameTribe* nativeTribes)
        {
            current = nativeTribes;
            var host = new Runtime(nativeTribes);
            var client = new Runtime(nativeTribes);
            host.live[1] = new GameTribe { r_GlobalId = 71, r_PlayerIdOwner = 2, r_AliveState = AliveState.IsAlive };
            host.live[2] = new GameTribe { r_GlobalId = 72, r_PlayerIdOwner = 2, r_AliveState = AliveState.IsAlive };
            host.synchronizationByTribeId.Add(2, new TribeSynchronization(72, 2, SynchronizedMovementCadence.Running, 9, 10, 11));
            host.synchronizationByTribeId.Add(1, new TribeSynchronization(71, 2, SynchronizedMovementCadence.Running, 5, 6, 7));
            byte[] hostBytes = host.CaptureRepair();
            Check(hostBytes.Length == sizeof(ushort) + 2 * RepairEntryBytes, "canonical record size");
            // Slot 2 was repaired to a different object before validating the host data.
            client.live[2].r_GlobalId = 82;
            client.cadencePatch.Entries[2] = 99;
            var candidate = client.PrepareRepair(hostBytes);
            Check(client.cadencePatch.Entries[2] == 99 && client.synchronizationByTribeId.Count == 0, "prepare is read-only");
            client.CommitRepair(candidate);
            Check(client.cadencePatch.Entries.Count == 1 && client.cadencePatch.Entries[1] == 5, "replace native cadence cache");
            Check(client.synchronizationByTribeId[1].PreviousMovementSpeed == 6 &&
                client.synchronizationByTribeId[1].AppliedMovementSpeed == 7, "live tribe recovers both speed values");
            Check(!client.synchronizationByTribeId.ContainsKey(2), "reused slot did not receive cadence");
            client.live[1].r_PlayerIdOwner = 3;
            Check(client.PrepareRepair(hostBytes).Count == 0, "owner mismatch must discard even when global ID matches");
            hostBytes[0]++;
            try { client.PrepareRepair(hostBytes); throw new Exception("partial record accepted"); }
            catch (InvalidDataException) { }
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void Run()
    {
        GameTribe* tribes = stackalloc GameTribe[3];
        Runtime.Run(tribes);
    }
}
""".Replace("PRODUCTION_MEMBERS", string.Join("\n", production));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!.Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("TroopRepairFixture", new[] { CSharpSyntaxTree.ParseText(fixture) },
            references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        using var output = new MemoryStream();
        var result = compilation.Emit(output);
        if (!result.Success) throw new Exception(string.Join(Environment.NewLine, result.Diagnostics));
        Assembly.Load(output.ToArray()).GetType("TroopRepairFixture")!.GetMethod("Run")!.Invoke(null, null);
        Console.WriteLine("PASS: actual troop repair preserves live cadence and speed metadata and rejects replaced identities.");
    }
}
