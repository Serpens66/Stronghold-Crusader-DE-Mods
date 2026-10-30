using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

internal static partial class Program
{
    // Compile the actual production bodies for both revisions. Only native memory,
    // native calls, game managers and passive diagnostics are supplied by the fixture.
    // revision: a snapshot directory (<file name>.txt per replaced source) compiled with the
    // current fixture contract, for comparisons between two current-API revisions.
    private static Type CompileProductionKernel(string root, bool before, bool diagnostics = false, string revision = null, bool development = false)
    {
        string Read(string relative)
        {
            if (revision != null)
            {
                string snapshot = Path.Combine(revision, Path.GetFileName(relative) + ".txt");
                if (File.Exists(snapshot)) return File.ReadAllText(snapshot);
            }
            string saved = Path.Combine(root, "_inspect/AssassinPerf/before", Path.GetFileName(relative) + ".txt");
            return File.ReadAllText(before && File.Exists(saved) ? saved : Path.Combine(root, relative));
        }
        var parseOptions = development ? CSharpParseOptions.Default.WithPreprocessorSymbols("SERP_DEVDIAG") : CSharpParseOptions.Default;
        var runtime = CSharpSyntaxTree.ParseText(Read("BugfixesAndQoL/src/AssassinPathfindingRuntime.cs"), parseOptions)
            .GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        string[] excluded = { "InitializeNative", "Dispose", "BuildWeightedPath", "DescribeNativeAssassinState",
            "ObservePreparedAssassinRoute", "ObserveMoveCommand", "ObserveTargetCommand", "CloseCommandScope",
            "CompleteCommand", "ClearTransientState", "ResetMapValidation", "ApplySetting", "BeginMap", "EndMap", "LogDebug", "LogInfo", "LogWarning", "LogError", "TimestampNow" };
        var members = runtime.Members.Where(m => m is FieldDeclarationSyntax || m is DelegateDeclarationSyntax ||
            m is MethodDeclarationSyntax method && !excluded.Contains(method.Identifier.Text) ||
            m is BaseTypeDeclarationSyntax type && type.Identifier.Text != "AssassinObservation" ||
            m is PropertyDeclarationSyntax property && property.Identifier.Text is "PerformanceDiagnosticsEnabled" or "DetailedDiagnosticsEnabled");
        string body = string.Join("\n", members.Select(m => ActiveDiagnosticFixtureCode(m)));
        if (before) body = "private const bool PerformanceDiagnosticsEnabled = true;\n" + body;
        body = System.Text.RegularExpressions.Regex.Replace(body, @"(?:const bool PerformanceDiagnosticsEnabled = false|static bool PerformanceDiagnosticsEnabled => Shared.DebugLogHelper.DiagnosticsEnabled)", diagnostics ? "const bool PerformanceDiagnosticsEnabled = true" : "const bool PerformanceDiagnosticsEnabled = false");
        // Identical counters in both revisions; no search decision is changed.
        body = body.Replace("int result = heap[0];", "fixturePops++; int result = heap[0];")
            .Replace("heapOperations++;", "heapOperations++; fixtureHeapOperations++;");
        var publication = CSharpSyntaxTree.ParseText(Read("BugfixesAndQoL/src/UnitCommands/Movement/MovementPathPublication.cs"), parseOptions);
        string publishing = string.Join("\n", publication.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.Text is "BuildPathWithCompletedMoatRouteVariant" or "BeginAssassinRoutePublication")
            .Select(m => ActiveDiagnosticFixtureCode(m)));
        // This kernel fixture has no active cursor proof scope; the observation
        // boundary itself is exercised with real snapshots by ManualHarness.
        publishing += "\nprivate void ObserveCursorGroundBuilder(IntPtr manager,int player,int profile,int result){}\n";
        // Passive timing is supplied at the fixture boundary. Its implementation and
        // exception isolation are covered by the production EnginePresentation fixture.
        if (development) publishing += "\nprivate long FreezeBuilder(IntPtr manager)=>0; private static class GateAttackFreezeCapture { internal static void Leave(long token,int result,bool completed){} }\n";
        if (!before && development)
        {
            var reporting = CSharpSyntaxTree.ParseText(Read("BugfixesAndQoL/src/UnitCommands/Movement/TemporaryGateRouteReporting.cs"), parseOptions);
            publishing += reporting.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Where(m => m.Identifier.Text == "TemporaryAssassinPublicationRejected").Select(ActiveDiagnosticFixtureCode).Single();
        }
        var api = CSharpSyntaxTree.ParseText(Read("APIShared/src/Pathfinding/Assassin/AssassinPathAPI.cs"));
        string boundary = string.Join("\n", api.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.Text is "TryStageWeightedRoute" or "TryGetCurrentWeightedRequest" or "BeginRouteOutput")
            .Select(m => ActiveDiagnosticFixtureCode(m)));
        string fixture = File.ReadAllText(Path.Combine(root, "_inspect/AssassinPerf/ProductionFixture.cs.txt"))
            .Replace("RUNTIME_MEMBERS", body).Replace("PUBLICATION_MEMBERS", publishing).Replace("API_BOUNDARY", boundary);
        if (before) fixture = fixture.Replace("out var cached,out _", "out var cached");
        // Existence-only answer (group/field requests) must equal the production A* outcome on every
        // terrain, climbing state and gate mask; the A* result above is the reference.
        fixture = fixture.Replace("VERIFY_EXISTENCE", before ? "" : """
      var existence=r.FindAnyWeightedRoute(sx,sy,tx,ty,4,r.climbRuntime.Allowed,true,null);
      Assert(existence==(found?RouteExistence.Exists:RouteExistence.None),"existence search agrees with production A* "+terrain);
      IEnemyGateRoutePolicySnapshot gateMask=i%3==0?new ValidationMask{Allowed=false}:new StripeMask();
      bool maskedFound=r.TryBuildWeightedRoute(sx,sy,tx,ty,TileCount,4,r.climbRuntime.Allowed,true,null,gateMask,out _);
      Assert(r.FindAnyWeightedRoute(sx,sy,tx,ty,4,r.climbRuntime.Allowed,true,gateMask)==(maskedFound?RouteExistence.Exists:RouteExistence.None),
       "existence search applies the same gate mask "+terrain);
      // Backward unreachability proof: never for a reachable target (oracle), with or without a gate
      // mask. The fixture's separated far side (1488 tiles) exceeds the budget and stays undecided.
      Assert(oracle<0||!r.ProvesTargetUnreachable(sx,sy,tx,ty,r.climbRuntime.Allowed,true,null),"backward proof keeps reachable target "+terrain);
      Assert(!maskedFound||!r.ProvesTargetUnreachable(sx,sy,tx,ty,r.climbRuntime.Allowed,true,gateMask),"backward proof keeps masked reachable target "+terrain);
      if(terrain=="unreachable")Assert(!r.ProvesTargetUnreachable(sx,sy,tx,ty,r.climbRuntime.Allowed,true,null),"budget leaves a large separated region to the forward search");
      if(terrain=="open"&&i==0){
       // A 5x5 interior behind a complete ring of invalid coordinates: proof inside the budget, and the
       // forward searches agree that no route exists. The ring is restored afterwards.
       var ring=new List<int>();
       for(int k=-3;k<=3;k++)foreach(var(px,py) in new[]{(50+k,7),(50+k,13),(47,10+k),(53,10+k)})ring.Add(py*800+px);
       foreach(int cell in ring)r.validCoordinates[cell]=0;
       Assert(r.ProvesTargetUnreachable(5,5,50,10,true,true,null),"backward proof decides an enclosed target");
       Assert(!r.TryBuildWeightedRoute(5,5,50,10,TileCount,4,true,true,null,null,out _)&&r.Oracle(5,5,50,10)<0,"enclosed target has no route");
       Assert(r.FindAnyWeightedRoute(5,5,50,10,4,true,true,null)==RouteExistence.None,"existence search reports the enclosed target as unreachable");
       Assert(!r.ProvesTargetUnreachable(50,10,51,11,true,true,null)&&r.TryBuildWeightedRoute(50,10,51,11,TileCount,4,true,true,null,null,out _),"route inside the enclosure stays available");
       foreach(int cell in ring)r.validCoordinates[cell]=1;
      }
""");
        fixture = fixture.Replace("VERIFY_BOUND_PROFILE", before ? "" : """
   var direct=new AssassinPathfindingRuntime("open");
   try {
    var c=direct.NewCommand();var context=new IntPtr(123);bool identity=true;
    Assert(direct.TryBuildWeightedRoute(5,5,10,5,TileCount,4,true,true,c,null,out var routeSummary),"route to stage");
    var key=new RouteCacheKey(5,5,10,5,TileCount,4,2,true,true,null,true);
    var prepared=direct.CachePreparedRoute(c,key,routeSummary);
    direct.TryStagePreparedRoute(context,key,routeSummary,prepared,c);
    long allocated=GC.GetAllocatedBytesForCurrentThread();
    Assert(!direct.TryStagePreparedRoute(context,key,routeSummary,prepared,c),"no exact frame cannot stage");
    long stagingBytes=GC.GetAllocatedBytesForCurrentThread()-allocated;
    Assert(stagingBytes==0,"unqualified staging allocates no route or encoding: "+stagingBytes);
    Assert(ReferenceEquals(prepared.GetPackedDirections(),prepared.GetPackedDirections()),"cached encoding reused");
    Assert(ReferenceEquals(prepared.GetValidator(),prepared.GetValidator()),"bound validator reused");
    Assert(prepared.GetValidator()(),"cached validator proves current route");
    // SPLIT_SETTINGS_BEGIN separate host settings for human players and AI lords (player table kind).
    direct.settings.EnableImprovedAssassinPathfindingAI=false;
    Assert(direct.IsWeightedEnabledFor(false)&&!direct.IsWeightedEnabledFor(true),"AI setting disables only AI lords");
    var split=new AssassinCommandScope(null,2,0,0,1,1,0,0,"split",direct.IsWeightedEnabledFor(false),direct.IsWeightedEnabledFor(true));
    Assert(split.Enabled&&split.IsEnabledFor(false)&&!split.IsEnabledFor(true),"command snapshot keeps both player kinds");
    GamePlayerManagerAPI.Instance.AI=true;
    Assert(!prepared.GetValidator()(),"cached route of an AI lord is rejected once the AI setting is off");
    GamePlayerManagerAPI.Instance.AI=false;
    Assert(prepared.GetValidator()(),"cached route of a human player stays valid");
    direct.settings.EnableImprovedAssassinPathfindingAI=true;direct.settings.EnableImprovedAssassinPathfinding=false;
    Assert(!direct.IsWeightedEnabledFor(false)&&direct.IsWeightedEnabledFor(true)&&!prepared.GetValidator()(),"human setting disables only human players");
    direct.settings.EnableImprovedAssassinPathfinding=true;
    Assert(new AssassinCommandScope(null,3,0,0,1,1,0,0,"legacy",true).IsEnabledFor(true),"single-flag scope enables both kinds");
    // SPLIT_SETTINGS_END
    direct.climbRuntime.Allowed=false;
    Assert(!prepared.GetValidator()(),"reused validator rechecks climbing policy");
    direct.climbRuntime.Allowed=true;direct.mapEpoch++;
    Assert(!prepared.GetValidator()(),"reused validator rejects changed map epoch");
    var frame=new AssassinRouteHandoff(context,1,2,3,4,2,()=>identity,(bytes,length)=>length,258,9);
    try {
     Assert(!AssassinPathAPI.TryGetCurrentWeightedRequest("other",context,1,2,3,4,out _,out _),"owner bound API");
     int scans=GameUnitManagerAPI.Instance.Scans;
     Assert(direct.TryResolveAssassinRequest(c,context,1,2,3,4,out int player,out int speed)&&player==2&&speed==9,"direct exact speed and low control byte");
     Assert(GameUnitManagerAPI.Instance.Scans==scans&&c.DirectResolutions==(PerformanceDiagnosticsEnabled ? 1 : 0),"direct resolution does not scan units");
     var mask=new FixtureMask();EnemyGatePathPolicyBridge.TryRegister(mask);mask.Active=true;
     Assert(!direct.TryResolveAssassinRequest(c,context,1,2,3,4,out _,out _),"invalid complete control player with gate mask rejected");
     mask.Active=false;identity=false;
     Assert(!AssassinPathAPI.TryGetCurrentWeightedRequest("fixture",context,1,2,3,4,out _,out _),"stale unit identity rejected");
    }finally{frame.Leave();}
   }finally{direct.Release();}
""");
        // A compared snapshot revision without the backward proof or the split player-kind settings
        // keeps the remaining fixture contract.
        if (!body.Contains("bool ProvesTargetUnreachable("))
            fixture = string.Join("\n", fixture.Split('\n').Where(line => !line.Contains("ProvesTargetUnreachable(")));
        if (!body.Contains("bool IsWeightedEnabledFor("))
        {
            int begin = fixture.IndexOf("// SPLIT_SETTINGS_BEGIN", StringComparison.Ordinal);
            int end = fixture.IndexOf("// SPLIT_SETTINGS_END", StringComparison.Ordinal);
            if (begin >= 0 && end > begin) fixture = fixture.Remove(begin, end - begin);
        }
        string[] files = { "APIShared/src/Pathfinding/Assassin/AssassinRouteHandoff.cs", "APIShared/src/Pathfinding/Assassin/AssassinGateTransitionPolicy.cs", "APIShared/src/Pathfinding/GateRoutes/TemporaryGateRouteAcceptanceBridge.cs",
            "APIShared/src/Pathfinding/GateRoutes/EnemyGatePathPolicyBridge.cs", "BugfixesAndQoL/src/AssassinGateRoutePolicy.cs",
            "BugfixesAndQoL/src/AssassinPathfindingRuntime.CacheKeys.cs", "BugfixesAndQoL/src/AssassinAStarPolicy.cs",
            "BugfixesAndQoL/src/AssassinClimbCostPolicy.cs", "BugfixesAndQoL/src/AssassinClimbTransitionPolicy.cs",
            "BugfixesAndQoL/src/AssassinRouteEncoding.cs", "BugfixesAndQoL/src/AssassinNativeSearch.cs",
            // Real development-diagnostics facade: without SERP_DEVDIAG every call compiles away.
            "Shared/Runtime/Diagnostics/DevDiag.cs" };
        // The policy byte-mask view lives with BugfixesAndQoL's gate policy publication (not compiled here).
        const string maskSource = "namespace BugfixesAndQoL.GateRouting { internal interface IAssassinDirectionMaskSource { byte[] DirectionMask { get; } } }";
        var trees = files.Select(f => CSharpSyntaxTree.ParseText(Read(f), parseOptions)).Append(CSharpSyntaxTree.ParseText(fixture, parseOptions))
            .Append(CSharpSyntaxTree.ParseText(maskSource, parseOptions));
        var refs = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator)
            .Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("ProductionAssassin" + (before ? "Before" : "After"), trees, refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true,
                optimizationLevel: OptimizationLevel.Release));
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream);
        Check(result.Success, "actual production kernel compiles: " + string.Join("\n", result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)));
        return Assembly.Load(stream.ToArray()).GetType("BugfixesAndQoL.AssassinPathfindingRuntime");
    }

    // Retain only active tokens; member-level #if directives belong to the source file.
    private static string ActiveDiagnosticFixtureCode(SyntaxNode node) =>
        node.ReplaceTrivia(node.DescendantTrivia(descendIntoTrivia: true), (_, _) => default(SyntaxTrivia))
            .NormalizeWhitespace().ToFullString();

    private static object InvokeKernel(Type kernel, string method, params object[] args)
    {
        try { return kernel.GetMethod(method).Invoke(null, args); }
        catch (TargetInvocationException ex) { throw ex.InnerException; }
    }

    private static void TestProductionKernel(string root)
    {
        Type disabled = CompileProductionKernel(root, false), enabled = CompileProductionKernel(root, false, true);
        InvokeKernel(disabled, "Verify");
        InvokeKernel(enabled, "Verify");
        // The packaged native kernel (build.bat compiles it before the tests) must equal the managed search.
        string nativeDirectory = Environment.GetEnvironmentVariable("BFQOL_ASSASSIN_NATIVE_DIR") ?? Path.Combine(root, "BugfixesAndQoL", "obj", "native");
        Console.WriteLine("PASS: native Assassin search equals managed: " + InvokeKernel(disabled, "VerifyNative", nativeDirectory));
        Type developmentKernel = CompileProductionKernel(root, false, development: true);
        InvokeKernel(developmentKernel, "VerifyTemporaryDecisions");
        Check(true, "actual gate transition observer on/off preserves predicates, results and legitimate climbing");
        foreach (string mode in new[] { "group", "independent", "field" })
        {
            string[] off = ((string)InvokeKernel(disabled, "Run", "gate", 1000, mode, true)).Split(',');
            string[] on = ((string)InvokeKernel(enabled, "Run", "gate", 1000, mode, true)).Split(',');
            Check(off[^1] == on[^1] && off[6] == on[6] && off[7] == on[7] && off[8] == on[8],
                "diagnostics switch preserves route checksum, expansion, heap operations and unit scans: " + mode);
            Check(long.Parse(off[9]) <= long.Parse(on[9]), "disabled diagnostics do not allocate more: " + mode);
        }
        InvokeKernel(developmentKernel, "VerifyTemporaryRouteValidation");
        CheckDisabledDiagnosticIL(disabled);
        Check(true, "production search, cache, resolution, final publication and Dijkstra agree");
    }

    private static void CheckDisabledDiagnosticIL(Type kernel)
    {
        var codes = typeof(System.Reflection.Emit.OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(System.Reflection.Emit.OpCode))
            .Select(f => (System.Reflection.Emit.OpCode)f.GetValue(null)).ToDictionary(c => unchecked((ushort)c.Value));
        foreach (var method in kernel.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(m => m.Name is "BuildWeightedPathCore" or "TryBuildWeightedRoute" or "PrepareRoute" or
                "GetRequestIndex" or "TryStagePreparedRoute" or "StagePreparedRouteCore" or "Push" or "Pop"))
        {
            byte[] il = method.GetMethodBody().GetILAsByteArray();
            for (int i = 0; i < il.Length;)
            {
                ushort value = il[i++];
                if (value == 0xfe) value = (ushort)(0xfe00 | il[i++]);
                var code = codes[value];
                if (code.OperandType == System.Reflection.Emit.OperandType.InlineMethod)
                {
                    var called = method.Module.ResolveMethod(BitConverter.ToInt32(il, i));
                    Check(called.DeclaringType != typeof(System.Diagnostics.Stopwatch), "disabled IL has no clock call: " + method.Name);
                    Check(!called.Name.StartsWith("Record") && called.Name != "StagePreparedRouteWithDiagnostics",
                        "disabled IL has no statistics or diagnostic closure call: " + method.Name);
                }
                i += code.OperandType switch
                {
                    System.Reflection.Emit.OperandType.InlineNone => 0,
                    System.Reflection.Emit.OperandType.ShortInlineBrTarget or System.Reflection.Emit.OperandType.ShortInlineI or System.Reflection.Emit.OperandType.ShortInlineVar => 1,
                    System.Reflection.Emit.OperandType.InlineVar => 2,
                    System.Reflection.Emit.OperandType.InlineI8 or System.Reflection.Emit.OperandType.InlineR => 8,
                    System.Reflection.Emit.OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, i),
                    _ => 4
                };
            }
        }
    }

    private static void BenchmarkDiagnosticSwitch(string root)
    {
        Type disabled = CompileProductionKernel(root, false), enabled = CompileProductionKernel(root, false, true);
        foreach (Type type in new[] { disabled, enabled }) InvokeKernel(type, "Run", "gate", 64, "group", true);
        Console.WriteLine("diagnostics,sample,terrain,requests,mode,warm,total_ms,us_request,expanded,heap_operations,full_unit_scans,allocated_bytes,gc0,gc1,gc2,diagnostic_cache_hits,publications,native_stub_ms,native_calls,checksum");
        foreach (int count in new[] { 1000, 5000, 10000 })
        foreach (string mode in new[] { "group", "independent", "field" })
        for (int sample = 1; sample <= 2; sample++)
        {
            string off, on;
            if (sample == 1) { off = (string)InvokeKernel(disabled,"Run","gate",count,mode,true); on = (string)InvokeKernel(enabled,"Run","gate",count,mode,true); }
            else { on = (string)InvokeKernel(enabled,"Run","gate",count,mode,true); off = (string)InvokeKernel(disabled,"Run","gate",count,mode,true); }
            Check(off.Split(',')[^1] == on.Split(',')[^1], "diagnostic benchmark route checksum");
            Console.WriteLine("off,"+sample+","+off); Console.WriteLine("on,"+sample+","+on); Console.Out.Flush();
        }
    }
    private static void BenchmarkProduction(string root, bool spreadOnly = false)
    {
        Type before = CompileProductionKernel(root, true), after = CompileProductionKernel(root, false);
        // JIT both revisions before measuring. Each measured run gets identical fresh fixtures.
        foreach (Type type in new[] { before, after }) InvokeKernel(type, "Run", "open", 64, "group", true);
        Console.WriteLine("revision,sample,terrain,requests,mode,warm,total_ms,us_request,expanded,heap_operations,full_unit_scans,allocated_bytes,gc0,gc1,gc2,cache_hits,publications,native_stub_ms,native_calls,checksum");
        foreach (int count in new[] { 1000, 5000, 10000 })
        foreach (string terrain in new[] { "open", "gate", "detour", "unreachable" })
        foreach (string mode in spreadOnly ? new[] { "spread" } : new[] { "group", "different", "independent", "field" })
        foreach (bool warm in new[] { false, true })
        for (int sample = 1; sample <= 2; sample++)
        {
            string left, right;
            if (sample == 1)
            {
                left = (string)InvokeKernel(before, "Run", terrain, count, mode, warm);
                right = (string)InvokeKernel(after, "Run", terrain, count, mode, warm);
            }
            else
            {
                right = (string)InvokeKernel(after, "Run", terrain, count, mode, warm);
                left = (string)InvokeKernel(before, "Run", terrain, count, mode, warm);
            }
            Check(left.Split(',')[^1] == right.Split(',')[^1], "before/after route checksum " + terrain + mode);
            Check(left.Split(',')[6] == right.Split(',')[6] && left.Split(',')[7] == right.Split(',')[7],
                "unchanged productive expansion and heap operations " + terrain + mode);
            Console.WriteLine("before," + sample + "," + left);
            Console.WriteLine("after," + sample + "," + right);
            Console.Out.Flush();
        }
    }

    // --compare <snapshot directory>: the snapshot revision (A) against the current sources (B),
    // both with the current fixture contract. Each pair must publish identical routes with
    // identical expansions and heap operations; samples alternate order to spread drift.
    private static void CompareRevisions(string root, string snapshot)
    {
        Type a = CompileProductionKernel(root, false, revision: snapshot), b = CompileProductionKernel(root, false);
        foreach (Type type in new[] { a, b })
        {
            InvokeKernel(type, "Verify");
            InvokeKernel(type, "VerifyCastle");
            InvokeKernel(type, "Run", "castle", 8, "independent", true);
        }
        Check(true, "both revisions agree with Dijkstra on all terrains including castle scale");
        Console.WriteLine("revision,sample,terrain,requests,mode,warm,total_ms,us_request,expanded,heap_operations,full_unit_scans,allocated_bytes,gc0,gc1,gc2,cache_hits,publications,native_stub_ms,native_calls,checksum");
        var totals = new Dictionary<string, double[]>();
        foreach (var (terrain, count) in new[] { ("castle", 40), ("gate", 1000), ("detour", 1000), ("open", 1000) })
        foreach (string mode in new[] { "independent", "group", "field" })
        for (int sample = 1; sample <= 3; sample++)
        {
            string left, right;
            if (sample % 2 == 1)
            {
                left = (string)InvokeKernel(a, "Run", terrain, count, mode, true);
                right = (string)InvokeKernel(b, "Run", terrain, count, mode, true);
            }
            else
            {
                right = (string)InvokeKernel(b, "Run", terrain, count, mode, true);
                left = (string)InvokeKernel(a, "Run", terrain, count, mode, true);
            }
            string[] l = left.Split(','), r = right.Split(',');
            Check(l[^1] == r[^1] && l[14] == r[14], "identical published routes " + terrain + "/" + mode);
            Check(l[6] == r[6] && l[7] == r[7], "identical expansions and heap operations " + terrain + "/" + mode);
            Console.WriteLine("A," + sample + "," + left);
            Console.WriteLine("B," + sample + "," + right);
            string key = terrain + "/" + mode;
            if (!totals.ContainsKey(key)) totals[key] = new double[2];
            totals[key][0] += double.Parse(l[4], System.Globalization.CultureInfo.InvariantCulture);
            totals[key][1] += double.Parse(r[4], System.Globalization.CultureInfo.InvariantCulture);
            Console.Out.Flush();
        }
        foreach (var entry in totals)
            Console.WriteLine($"SUMMARY {entry.Key}: A={entry.Value[0]:F1} ms, B={entry.Value[1]:F1} ms, B/A={entry.Value[1] / entry.Value[0]:P1}");
    }
}
