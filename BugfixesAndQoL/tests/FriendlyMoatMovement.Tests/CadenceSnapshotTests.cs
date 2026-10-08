using System.Reflection;
using Iced.Intel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class CadenceSnapshotTests
{
    internal static void Validate(string root)
    {
        string resolver = File.ReadAllText(Path.Combine(root, "APIShared/src/UnitCommands/NativeMovementCadenceResolver.cs"));
        string patterns = File.ReadAllText(Path.Combine(root, "Shared/NativePatternResolver.cs"));
        var runtime = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,
            "APIShared/src/UnitCommands/UnitCommandPathRuntime.cs"))).GetRoot();
        var optional = runtime.DescendantNodes().OfType<TryStatementSyntax>()
            .Single(t => t.Block.ToString().Contains("nativeMovementCadenceResolver = new NativeMovementCadenceResolver"));
        var publication = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,
            "APIShared/src/UnitCommands/WeightedMoatPublication.cs"))).GetRoot().DescendantNodes()
            .OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.Text == "TryPublishSafelyFasterWeightedRoute");
        var guard = publication.WithBody(publication.Body!.WithStatements(SyntaxFactory.List(
            publication.Body.Statements.TakeWhile(s => !s.ToString().Contains("TryGetPlausibleSpeedBonuses"))
                .Append(SyntaxFactory.ParseStatement("return true;")))));
        string fixture = @"
using System; using System.Reflection; using System.Collections.Generic; using System.Runtime.InteropServices;
using BepInEx.Logging; using SHCDESE.API.LowLevel;
namespace BepInEx.Logging { public class ManualLogSource {} }
namespace SHCDESE.API.LowLevel {
    public sealed class CrusaderLibraryLoadContext {
        private readonly byte[] snapshot;
        public CrusaderLibraryLoadContext(byte[] data, IntPtr address) { snapshot=data; ModuleHandle=address; }
        public IntPtr ModuleHandle { get; }
        public ReadOnlySpan<byte> Memory => snapshot;
    }
}
namespace Shared {
    internal static class DebugLogHelper {
        public static void LogDebug(ManualLogSource log,string message) {}
        public static void LogWarning(ManualLogSource log,string message) {}
    }
}
namespace APIShared.UnitCommands {
    internal sealed unsafe class OptionalFixture {
        private NativeMovementCadenceResolver nativeMovementCadenceResolver;
        private ulong nativeUnitManager=1;
        private ManualLogSource log=new ManualLogSource();
        private bool weightedShadowBusy=false;
        private IntPtr nativePathManager=new IntPtr(1);
        internal struct WeightedMoatRouteSummary { public int StructuralEdges; }
        internal sealed class BuilderWeightedScope {}
        internal static class WeightedMoatRoutePlanner { internal const int MaximumRouteEdges=2000; }
        internal void Prepare(CrusaderLibraryLoadContext context) {
" + optional.ToFullString() + @"
        }
" + guard.ToFullString() + @"
        internal static unsafe void CheckFailure(CrusaderLibraryLoadContext context) {
            var f=new OptionalFixture(); f.Prepare(context);
            if(f.nativeMovementCadenceResolver!=null) throw new Exception(""Invalid resolver remained available"");
            byte value=0;
            if(f.TryPublishSafelyFasterWeightedRoute(new IntPtr(1),&value,1,new BuilderWeightedScope(),default,
                out _,out _,out _,out string reason) || reason!=""native-cadence-resolver-unavailable"")
                throw new Exception(""Missing resolver did not reject weighted publication safely"");
        }
    }
    public static class SnapshotFixture {
        private const int Dispatch=0x18410C,Table=0x321CB0,Handler=0x200,Map=0x1000,Jump=0x1100;
        public static void Run() {
            int count=(int)SHCDESE.Interop.eChimps.CHIMP_NUM_TYPES;
            int size=Table+count*8+128;
            IntPtr live=Marshal.AllocHGlobal(size);
            try {
                ulong address=unchecked((ulong)live.ToInt64());
                var snapshot=new byte[size];
                byte[] call={0x41,0xFF,0x94,0xC6,0xB0,0x1C,0x32,0,0x8B,0x15,0,0,0,0,0x48,0x63,0xC2,0x48,0x69,0xC8,0x90,4,0,0};
                Array.Copy(call,0,snapshot,Dispatch,call.Length);
                byte[] body={0x4C,0x8D,0x35,0xF9,0xFD,0xFF,0xFF,0x0F,0xB7,0x81,0x18,9,0,0,
                    0x41,0x0F,0xB6,0x84,0x06,0,0x10,0,0,0x41,0x8B,0x84,0x86,0,0x11,0,0,0x49,3,0xC6,0xFF,0xE0};
                Array.Copy(body,0,snapshot,Handler,body.Length);
                for(int i=Handler+body.Length;i<0x240;i++)snapshot[i]=0x90;
                byte[] bonus={0x66,0xC7,0x81,0x16,9,0,0,3,0,0xC3};
                Array.Copy(bonus,0,snapshot,0x240,bonus.Length);
                snapshot[Map+101]=1;
                Array.Copy(BitConverter.GetBytes(0x240),0,snapshot,Jump+4,4);
                Array.Copy(BitConverter.GetBytes(address+Handler),0,snapshot,Table+5*8,8);
                for(int scenario=0;scenario<6;scenario++) {
                    Marshal.Copy(snapshot,0,live,size);
                    if(scenario==0 || scenario==5) for(int i=0;i<14;i++)Marshal.WriteByte(live,Dispatch+i,0xCC);
                    if(scenario==1 || scenario==5) Marshal.WriteInt64(live,Table+5*8,0);
                    if(scenario==2 || scenario==5) for(int i=0;i<0x5000;i++)Marshal.WriteByte(live,Handler+i,0xCC);
                    if(scenario==3 || scenario==5) Marshal.WriteByte(live,Map+101,2);
                    if(scenario==4 || scenario==5) Marshal.WriteInt32(live,Jump+4,-1);
                    var context=new CrusaderLibraryLoadContext(snapshot,live);
                    var resolver=new NativeMovementCadenceResolver(context,1,new ManualLogSource());
                    if(!resolver.TryGetPlausibleSpeedBonuses(5,0,out int[] values,out ulong rva,out _) ||
                        rva!=Handler || values.Length!=2 || values[0]!=0 || values[1]!=3)
                        throw new Exception(""Snapshot diverged from patched live image: scenario=""+scenario);
                    var read=typeof(NativeMovementCadenceResolver).GetMethod(""ReadUInt32"",BindingFlags.Instance|BindingFlags.NonPublic);
                    foreach(ulong bad in new[]{address-1,address+(uint)size-3,address+(uint)size,ulong.MaxValue}) {
                        try {read.Invoke(resolver,new object[]{bad}); throw new Exception(""Out-of-range read accepted"");}
                        catch(TargetInvocationException e) when(e.InnerException is InvalidOperationException) {}
                    }
                }
                var invalid=(byte[])snapshot.Clone();
                Array.Copy(BitConverter.GetBytes(uint.MaxValue),0,invalid,Dispatch+4,4);
                OptionalFixture.CheckFailure(new CrusaderLibraryLoadContext(invalid,live));
                var badHandler=(byte[])snapshot.Clone();
                Array.Copy(BitConverter.GetBytes(address-1),0,badHandler,Table+5*8,8);
                var unresolved=new NativeMovementCadenceResolver(new CrusaderLibraryLoadContext(badHandler,live),1,new ManualLogSource());
                if(unresolved.TryGetPlausibleSpeedBonuses(5,0,out _,out _,out _))throw new Exception(""Invalid handler accepted"");
            } finally { Marshal.FreeHGlobal(live); }
        }
    }
}";
        string extender = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\000shcdese\SHCDESE.dll";
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(Assembler).Assembly.Location).Append(extender).Distinct().Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("ActualCadenceSnapshotFixture",
            new[] { CSharpSyntaxTree.ParseText(resolver), CSharpSyntaxTree.ParseText(patterns), CSharpSyntaxTree.ParseText(fixture) },
            references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        using var output = new MemoryStream(); var result = compilation.Emit(output);
        if (!result.Success) throw new Exception(string.Join("\n", result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)));
        try { Assembly.Load(output.ToArray()).GetType("APIShared.UnitCommands.SnapshotFixture")!.GetMethod("Run")!.Invoke(null, null); }
        catch (TargetInvocationException e) { throw e.InnerException ?? e; }
        Console.WriteLine("PASS: production cadence resolver uses the load snapshot despite live hook/code/table mutations; bounded reads and optional-publication failure executed.");
    }
}
