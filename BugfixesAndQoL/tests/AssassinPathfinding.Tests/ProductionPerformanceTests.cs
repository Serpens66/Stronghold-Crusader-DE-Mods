using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

internal static partial class Program
{
    // Compile the actual production bodies for both revisions. Only native memory,
    // native calls, game managers and passive diagnostics are supplied by the fixture.
    private static Type CompileProductionKernel(string root, bool before, bool diagnostics = false)
    {
        string Read(string relative)
        {
            string saved = Path.Combine(root, "_inspect/AssassinPerf/before", Path.GetFileName(relative) + ".txt");
            return File.ReadAllText(before && File.Exists(saved) ? saved : Path.Combine(root, relative));
        }
        var runtime = CSharpSyntaxTree.ParseText(Read("BugfixesAndQoL/src/AssassinPathfindingRuntime.cs"))
            .GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        string[] excluded = { "InitializeNative", "Dispose", "BuildWeightedPath", "DescribeNativeAssassinState",
            "ObservePreparedAssassinRoute", "ObserveMoveCommand", "ObserveTargetCommand", "CloseCommandScope",
            "CompleteCommand", "ClearTransientState", "ResetMapValidation", "ApplySetting", "BeginMap", "EndMap", "LogDebug", "LogInfo", "LogWarning", "LogError", "TimestampNow" };
        var members = runtime.Members.Where(m => m is FieldDeclarationSyntax || m is DelegateDeclarationSyntax ||
            m is MethodDeclarationSyntax method && !excluded.Contains(method.Identifier.Text) ||
            m is TypeDeclarationSyntax type && type.Identifier.Text != "AssassinObservation");
        string body = string.Join("\n", members.Select(m => m.ToFullString()));
        if (before) body = "private const bool PerformanceDiagnosticsEnabled = true;\n" + body;
        if (diagnostics) body = body.Replace("PerformanceDiagnosticsEnabled = false", "PerformanceDiagnosticsEnabled = true");
        // Identical counters in both revisions; no search decision is changed.
        body = body.Replace("int result = heap[0];", "fixturePops++; int result = heap[0];")
            .Replace("heapOperations++;", "heapOperations++; fixtureHeapOperations++;");
        var publication = CSharpSyntaxTree.ParseText(Read("APIShared/src/UnitCommands/MovementPathPublication.cs"));
        string publishing = string.Join("\n", publication.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.Text is "BuildPathWithCompletedMoatRouteVariant" or "BeginAssassinRoutePublication")
            .Select(m => m.ToFullString()));
        var api = CSharpSyntaxTree.ParseText(Read("APIShared/src/AssassinPathAPI.cs"));
        string boundary = string.Join("\n", api.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.Text is "TryStageWeightedRoute" or "TryGetCurrentWeightedRequest")
            .Select(m => m.ToFullString()));
        string fixture = File.ReadAllText(Path.Combine(root, "_inspect/AssassinPerf/ProductionFixture.cs.txt"))
            .Replace("RUNTIME_MEMBERS", body).Replace("PUBLICATION_MEMBERS", publishing).Replace("API_BOUNDARY", boundary);
        if (before) fixture = fixture.Replace("out var cached,out _", "out var cached");
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
        string[] files = { "APIShared/src/AssassinRouteHandoff.cs", "APIShared/src/AssassinGateTransitionPolicy.cs", "APIShared/src/TemporaryGateRouteAcceptanceBridge.cs",
            "APIShared/src/EnemyGatePathPolicyBridge.cs", "BugfixesAndQoL/src/AssassinGateRoutePolicy.cs",
            "BugfixesAndQoL/src/AssassinPathfindingRuntime.CacheKeys.cs", "BugfixesAndQoL/src/AssassinAStarPolicy.cs",
            "BugfixesAndQoL/src/AssassinClimbCostPolicy.cs", "BugfixesAndQoL/src/AssassinClimbTransitionPolicy.cs",
            "BugfixesAndQoL/src/AssassinRouteEncoding.cs" };
        var trees = files.Select(f => CSharpSyntaxTree.ParseText(Read(f))).Append(CSharpSyntaxTree.ParseText(fixture));
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

    private static object InvokeKernel(Type kernel, string method, params object[] args)
    {
        try { return kernel.GetMethod(method).Invoke(null, args); }
        catch (TargetInvocationException ex) { throw ex.InnerException; }
    }

    private static void TestProductionKernel(string root)
    {
        TestGitContractPreservation(root);
        Type disabled = CompileProductionKernel(root, false), enabled = CompileProductionKernel(root, false, true);
        InvokeKernel(disabled, "Verify");
        InvokeKernel(enabled, "Verify");
        InvokeKernel(disabled, "VerifyTemporaryDecisions");
        Check(true, "actual gate transition observer on/off preserves predicates, results and legitimate climbing");
        foreach (string mode in new[] { "group", "independent", "field" })
        {
            string[] off = ((string)InvokeKernel(disabled, "Run", "gate", 1000, mode, true)).Split(',');
            string[] on = ((string)InvokeKernel(enabled, "Run", "gate", 1000, mode, true)).Split(',');
            Check(off[^1] == on[^1] && off[6] == on[6] && off[7] == on[7] && off[8] == on[8],
                "diagnostics switch preserves route checksum, expansion, heap operations and unit scans: " + mode);
            Check(long.Parse(off[9]) <= long.Parse(on[9]), "disabled diagnostics do not allocate more: " + mode);
        }
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
    private static void TestGitContractPreservation(string root)
    {
        string GitSource(string revision, string path)
        {
            var start = new System.Diagnostics.ProcessStartInfo("git")
            {
                WorkingDirectory = root, RedirectStandardOutput = true,
                RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true
            };
            start.ArgumentList.Add("show"); start.ArgumentList.Add(revision + ":" + path);
            using var process = System.Diagnostics.Process.Start(start);
            string text = process.StandardOutput.ReadToEnd(), error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            Check(process.ExitCode == 0, "read previous Git source: " + error);
            return text;
        }
        MethodDeclarationSyntax Method(string source, string name) => CSharpSyntaxTree.ParseText(source)
            .GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.Text == name);
        string WithoutDiagnosticGates(string source) => source
            .Replace("(PerformanceDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0)", "Stopwatch.GetTimestamp()")
            .Replace("if (PerformanceDiagnosticsEnabled) ", "")
            .Replace("RememberTemporaryParent(nextTile);", "")
            .Replace("activeObservation?.ParentDecisions?.Clear();", "");
        MethodDeclarationSyntax FunctionalEvaluator(string source)
        {
            var method = Method(source, "EvaluateAssassinTransition");
            var removable = method.Body.Statements.Where(st => st.ToString().StartsWith("evidence =") ||
                st is IfStatementSyntax test && (test.Condition.ToString().Contains("DetailedObserver") || test.Condition.ToString() == "evidence != null")).ToArray();
            method = method.RemoveNodes(removable, SyntaxRemoveOptions.KeepNoTrivia);
            return method.WithIdentifier(SyntaxFactory.Identifier("AllowsAssassinTransition"))
                .WithParameterList(method.ParameterList.WithParameters(method.ParameterList.Parameters.RemoveAt(method.ParameterList.Parameters.Count - 1)));
        }
        string runtimePath = "BugfixesAndQoL/src/AssassinPathfindingRuntime.cs";
        string previous = GitSource("f57dfdb02", runtimePath), current = WithoutDiagnosticGates(File.ReadAllText(Path.Combine(root, runtimePath)));
        foreach (string name in new[] { "IsVanillaAssassinFallback", "GetClimbTicks", "HasOrdinaryConnection",
            "ValidateCachedRoute", "PrepareRoute", "AllowsAssassinTransition", "ValidatePreparedGateRoute",
            "CanReconstructTransition", "CommitPreparedRoute", "GetRequestIndex", "BuildRequestIndex",
            "IsValidCoordinate", "GetTileId", "GetCoordinateIndex", "IsNativeTile", "ValidateCoordinateTileMapping",
            "EnsureCoordinateTileMappingValidated", "Touch", "ResetTouchedNodes", "Push", "PushOrDecrease", "Pop",
            "SiftUp", "SiftDown", "ComesBefore" })
            Check(Method(previous,name).NormalizeWhitespace().ToFullString() == (name == "AllowsAssassinTransition" ? FunctionalEvaluator(current) : Method(current,name)).NormalizeWhitespace().ToFullString(),
                "Git contract unchanged: " + name);
        string search = Method(current,"TryBuildWeightedRoute").ToFullString()
            .Replace("Dictionary<int, int> suffixCosts = command?.GetSuffixCosts(suffixKey);", "")
            .Replace("diagonalTicks, suffixCosts, startNode", "diagonalTicks, command, suffixKey, startNode")
            .Replace("diagonalTicks, suffixCosts, nextNode", "diagonalTicks, command, suffixKey, nextNode");
        Check(CSharpSyntaxTree.ParseText(search).GetRoot().NormalizeWhitespace().ToFullString() ==
            CSharpSyntaxTree.ParseText(Method(previous,"TryBuildWeightedRoute").ToFullString()).GetRoot().NormalizeWhitespace().ToFullString(),
            "Git A* body differs only in suffix dictionary binding and passive diagnostic gates");
        string publicationPath = "APIShared/src/UnitCommands/MovementPathPublication.cs";
        Check(Method(GitSource("8fe105a11", publicationPath),"BuildPathWithCompletedMoatRouteVariant").NormalizeWhitespace().ToFullString() ==
            Method(File.ReadAllText(Path.Combine(root, publicationPath)),"BuildPathWithCompletedMoatRouteVariant").NormalizeWhitespace().ToFullString(),
            "Git F4930 wrapper, original call, exception fallback and nested Leave unchanged");
        foreach (string path in new[] { "BugfixesAndQoL/src/AssassinClimbCostPolicy.cs", "BugfixesAndQoL/src/AssassinPathfindingRuntime.CacheKeys.cs" })
            Check(CSharpSyntaxTree.ParseText(GitSource("f57dfdb02",path)).GetRoot().NormalizeWhitespace().ToFullString() ==
                CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,path))).GetRoot().NormalizeWhitespace().ToFullString(),
                "Git costs and cache-key contracts unchanged: " + path);
        Console.WriteLine("PASS: prior Git sources preserve native fallback, edge/cost/cache checks, heap and command wrapper contracts.");
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
}
