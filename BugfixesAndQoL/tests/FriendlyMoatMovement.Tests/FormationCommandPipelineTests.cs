using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

internal static class FormationCommandPipelineTests
{
    internal static void Validate(string root)
    {
        var runtime = CSharpSyntaxTree.ParseText(FeatureSourceModel.Read(root,
            "FormationRuntime", "BugfixesAndQoL/src/Formations")).GetRoot();
        string[] methods = { "TryCreatePacket", "ValidatePacket", "ApplyPacket", "ApplyPacketLocked", "TryIssuePacketVanillaFallback",
            "OnTribeIssueOrderMoveHere", "PublishFormationDestinations", "OnUnitMoveHere", "PruneUnitAssignmentFrames", "ClearUnitAssignmentFrames",
            "FinishUnitAssignmentFrame", "TryGetMatchingUnit", "RunUnitFallbacks", "LogFormationOrderResult",
            "BuildManagedDestinations", "BuildDirectionIndicator", "BuildVanillaSlotMetadata", "SnapSlots",
            "CaptureVanillaDestinations", "CaptureGroundSlots", "RentVisited", "ReturnVisited", "TryEnqueueVanillaCandidate",
            "CaptureReachableCandidates", "TryEnqueueCandidate", "IsAssassinOnly", "ComputeSelectionHash",
            "ComputePlanHash", "CombineAdmissionPlanHash" };
        string[] types = { "ActiveDrag", "GroundTarget", "SelectionIdentity", "PendingFormationCommand",
            "ActiveFormationCommand", "UnitAssignmentFrame", "NativeDestination", "UnitFallbackAttempt",
            "UnitFallbackRequest", "UnitFallbackSummary", "VanillaSearchNode" };
        string[] fields = { "ProtocolVersion", "MapWidth", "MaximumUnitCount", "MaximumTribeCount",
            "MaximumPreviewCandidates", "MaximumVanillaSelectorCandidates", "visitedScratch", "vanillaQueueScratch" };
        string members = string.Join("\n", runtime.DescendantNodes().Where(n =>
            n is MethodDeclarationSyntax m && m.ExplicitInterfaceSpecifier == null && methods.Contains(m.Identifier.Text) ||
            n is BaseTypeDeclarationSyntax t && types.Contains(t.Identifier.Text) ||
            n is FieldDeclarationSyntax f && f.Declaration.Variables.Any(v => fields.Contains(v.Identifier.Text)))
            .Select(n => n.ToFullString()));
        string fixture = File.ReadAllText(Path.Combine(root,
            "BugfixesAndQoL/tests/FriendlyMoatMovement.Tests/FormationCommandPipelineFixture.cs.txt"))
            .Replace("__MEMBERS__", members);
        string[] files = { "Formations/FormationModel.cs", "Formations/FormationOrderPacket.cs",
            "Formations/FormationPresentation.cs", "Formations/FormationReleaseStateModel.cs",
            "UnitCommands/Runtime/ManualRegionAdmissionBatch.cs" };
        var trees = files.Select(p => CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,
            "BugfixesAndQoL/src", p)))).Append(CSharpSyntaxTree.ParseText(fixture));
        var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p)).ToList();
        refs.Add(MetadataReference.CreateFromFile(typeof(MessagePack.MessagePackWriter).Assembly.Location));
        var compilation = CSharpCompilation.Create("ProductionFormationCommandPipeline", trees, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        using var output = new MemoryStream(); var result = compilation.Emit(output);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        try { Assembly.Load(output.ToArray()).GetType("BugfixesAndQoL.UnitCommands.PipelineFixture")!.GetMethod("Run")!.Invoke(null, null); }
        catch (TargetInvocationException e) { throw e.InnerException ?? e; }
        Console.WriteLine("PASS: production gesture/packet/formatter/validation/dispatch/admission/slot/event pipeline; six shapes through 1300, Line 4001/10000, zero/one/all admitted, current identities and bounded shared search.");
    }
}
