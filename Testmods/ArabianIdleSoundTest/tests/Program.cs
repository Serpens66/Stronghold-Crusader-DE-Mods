using ArabianIdleSoundTest;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using SHCDESE.API;
using SHCDESE.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal static unsafe class Program
{
    private static string game;
    private static readonly List<object> roots = new List<object>();
    private static int checks;
    private static int Main(string[] args)
    {
        game = args[0];
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        try { Run(); Console.WriteLine("PASS: " + checks + " checks; installed public members, marker transport, actual IL and NativeX64/Absolute copies."); return 0; }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static Assembly Resolve(object sender, ResolveEventArgs args)
    {
        string name = new AssemblyName(args.Name).Name + ".dll";
        if (name == "APIShared.dll")
            return Assembly.LoadFrom(Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "../../../../../APIShared/BepInEx/plugins/APIShared_Serp/APIShared.dll")));
        foreach (string directory in new[] { "BepInEx/core", "BepInEx/plugins/000shcdese", "Stronghold Crusader Definitive Edition_Data/Managed" })
        {
            string path = Path.Combine(game, directory, name);
            if (File.Exists(path)) return Assembly.LoadFrom(path);
        }
        return null;
    }
    private static void Check(bool value, string message)
    {
        checks++;
        if (!value) throw new InvalidOperationException(message);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Run()
    {
        var files = new InstalledAudioFiles(game);
        files.Validate();
        Check(NativeContract.HashFile(files.Native) == NativeContract.NativeHash, "native identity");
        Check(NativeContract.HashFile(files.Managed) == NativeContract.ManagedHash, "managed identity");
        Check(NativeContract.HashFile(files.Backend) == NativeContract.BackendHash, "installed backend identity");
        Paths(files);
        Members();
        Policy();
        Il();
        byte[] image = File.ReadAllBytes(files.Native);
        Probe(image, NativeContract.ArabianRva, 3879, NativeContract.ArabianBytes);
        Probe(image, NativeContract.EmitRva, 137, NativeContract.EmitBytes);
    }
    private static void Paths(InstalledAudioFiles files)
    {
        SoundPolicy.SetEnabled(true); SoundPolicy.SetReady(false);
        var fromBytes = Assembly.Load(File.ReadAllBytes(files.Managed));
        Check(fromBytes.Location == "", "real byte-loaded assembly has empty Location like Unity");
        files.Validate();
        Check(File.Exists(files.Managed) && File.Exists(files.Native) && File.Exists(files.Backend), "canonical files resolve independently of empty Location");
        bool missing = false;
        try { InstalledAudioFiles.ValidateFile(files.Managed + ".missing-unit-audio", NativeContract.ManagedHash); }
        catch (FileNotFoundException) { missing = true; }
        Check(missing && !SoundPolicy.Active, "missing file rejected before activation");
        bool changed = false;
        try { InstalledAudioFiles.ValidateFile(files.Managed, new string('0', 64)); }
        catch (InvalidOperationException) { changed = true; }
        Check(changed && !SoundPolicy.Active, "hash mismatch rejected before activation");
        bool empty = false;
        try { new InstalledAudioFiles(""); }
        catch (ArgumentException) { empty = true; }
        Check(empty, "missing game root fails clearly");
    }
    private static void Members()
    {
        foreach (var spec in new[] { Tuple.Create("r_UnitChimp", 0x8A, "eChimps"), Tuple.Create("r_AIState", 0x2BC, "UInt16"),
            Tuple.Create("r_CurrentTilePositionX", 0xC0, "UInt16"), Tuple.Create("r_CurrentTilePositionY", 0xC2, "UInt16") })
        {
            var field = typeof(GameUnit).GetField(spec.Item1);
            Check(field != null && field.IsPublic && field.FieldType.Name == spec.Item3 &&
                Marshal.OffsetOf(typeof(GameUnit), spec.Item1).ToInt32() == spec.Item2, "installed GameUnit member " + spec.Item1);
        }
        Check(typeof(GameUnit).StructLayoutAttribute.Size == 0x490, "unit stride");
        var lookup = typeof(GameUnitManagerAPI).GetMethod("TryGetUnitById", new[] { typeof(int), typeof(GameUnit*).MakeByRefType() });
        Check(lookup != null && lookup.IsPublic && lookup.ReturnType == typeof(bool), "installed pointer lookup");
        Check(typeof(GameUnitManagerAPI).GetProperty("Instance").GetMethod.IsPublic, "public manager singleton");
        var access = typeof(APIShared.UnitAccess).GetMethod("TryGetById", new[] { typeof(int), typeof(GameUnit*).MakeByRefType(), typeof(APIShared.UnitLookupFailure).MakeByRefType(), typeof(string), typeof(string), typeof(int) });
        Check(access != null && access.IsPublic && access.IsStatic && access.ReturnType == typeof(bool), "public UnitAccess signature from fresh package");
        Check(!APIShared.UnitAccess.TryGetById(null, 1, out GameUnit* absent, out var failure) && absent == null && failure == APIShared.UnitLookupFailure.ManagerUnavailable, "central manager-unavailable rejection");
        Check(typeof(BepInEx.Paths).GetProperty("GameRootPath").GetMethod.IsPublic, "public BepInEx game root");
        var play = typeof(SFXManager).GetMethod("playSound", new[] { typeof(int), typeof(float), typeof(float), typeof(bool) });
        Check(play != null && play.IsPublic && !play.IsStatic && play.ReturnType == typeof(void), "real playSound access");
    }
    private static short[] Record()
    {
        var record = new short[21]; record[0] = 16; record[3] = 180; record[11] = record[12] = -1; return record;
    }
    private static void Policy()
    {
        SoundPolicy.SetEnabled(true); SoundPolicy.SetReady(true);
        Check(SoundPolicy.Eligible(13, 13, 75, 1, 180, 0), "target eligibility");
        foreach (var args in new[] { new[] {0,13,75,1,180,0}, new[] {13,14,75,1,180,0}, new[] {13,13,27,1,180,0},
            new[] {13,13,75,3,180,0}, new[] {13,13,75,1,179,0}, new[] {13,13,75,1,180,1} })
            Check(!SoundPolicy.Eligible(args[0],args[1],args[2],args[3],args[4],args[5]), "unrelated context");
        var record = Record(); var original = (short[])record.Clone();
        Check(SoundPolicy.TryMark(record, true), "mark eligible record");
        Check(record[11] < 0 && record[12] < 0, "Vanilla spatial branch retained");
        for (int i=0;i<21;i++) Check(i==11 || record[i]==original[i], "only volume marker changed");
        Check(SoundPolicy.Scale(record, 0, 180, 0.8f)==0.2f, "25 percent of spatial volume");
        Check(SoundPolicy.Scale(original, 0, 180, 0.8f)==0.8f, "same ID unrelated context unchanged");
        Check(SoundPolicy.Scale(record, 0, 179, 0.8f)==0.8f, "sword sheath-in unchanged");
        var combined=new short[63]; Array.Copy(original,0,combined,0,21); Array.Copy(record,0,combined,21,21); Array.Copy(original,0,combined,42,21);
        Check(SoundPolicy.Scale(combined,0,180,1)==1 && SoundPolicy.Scale(combined,1,180,1)==0.25f && SoundPolicy.Scale(combined,2,180,1)==1,"mixed records");
        for(int i=0;i<6;i++) { var buffer=(short[])combined.Clone(); Check(SoundPolicy.Scale(buffer,1,180,1)==0.25f,"native/managed copy and pool transport"); }
        Array.Copy(original,0,combined,21,21); Check(SoundPolicy.Scale(combined,1,180,1)==1,"reused slot overwrites marker");
        Check(SoundPolicy.Scale(record,-1,180,1)==1 && SoundPolicy.Scale(record,int.MaxValue,180,1)==1,"bounds");
        foreach(int field in new[]{0,3,11,12,13}) { var bad=Record();bad[field]=9;Check(!SoundPolicy.TryMark(bad,true),"malformed record "+field); }
        SoundPolicy.SetEnabled(false); Check(!SoundPolicy.TryMark(Record(),SoundPolicy.Active),"disabled capture");
        Check(SoundPolicy.Scale(record,0,180,1)==1,"disable applies to pending tagged records");
        SoundPolicy.SetEnabled(true); SoundPolicy.SetReady(false);Check(SoundPolicy.Scale(record,0,180,1)==1,"partial installation inert");
        SoundPolicy.SetReady(true);
    }
    private static void Il()
    {
        var method=typeof(GameMap).GetMethod("processTestMap",new[]{typeof(short[]),typeof(int),typeof(EngineInterface.PlayState),typeof(byte[])});
        Check(method!=null && method.IsPublic && method.ReturnType==typeof(void),"real processTestMap access");
        SoundIlContract.Validate(method);
        using(var definition=new DynamicMethodDefinition(method))
        using(var context=new ILContext(definition.Definition))
        {
            var calls=SoundIlContract.Find(context,out var index); Check(calls.Length==2 && index.VariableType.FullName=="System.Int32","two call sites and loop local");
            SoundIlContract.Apply(context);
            Check(!context.Body.Instructions.Any(i=>calls.Contains(i)),"both original IL calls replaced");
            Check(context.Body.Instructions.Count(i=>i.OpCode==OpCodes.Ldarg_1)>=2,"buffer argument emitted");
            var generated=definition.Generate();
            var executable=generated.CreateDelegate(typeof(Action<GameMap,short[],int,EngineInterface.PlayState,byte[]>));
            // Unity's native ECalls cannot be JIT-compiled by desktop CLR.
            // Generate the real patched body/delegate; actual execution is an
            // explicit in-game acceptance check, not a fake Unity replacement.
            Check(executable!=null,"patched real GameMap IL generates a CLR delegate");
            bool rejected=false;try{SoundIlContract.Find(context,out _);}catch(InvalidOperationException){rejected=true;}Check(rejected,"second patch rejected");
        }
        using(var definition=new DynamicMethodDefinition(method))
        using(var context=new ILContext(definition.Definition))
        {
            var calls=SoundIlContract.Find(context,out _);context.Body.Instructions.Remove(calls[0]);
            bool rejected=false;try{SoundIlContract.Apply(context);}catch(InvalidOperationException){rejected=true;}Check(rejected,"changed IL rejected before mutation");
        }
    }
    private static int Offset(byte[] image,int rva)
    {
        int pe=BitConverter.ToInt32(image,0x3C),count=BitConverter.ToUInt16(image,pe+6),optional=BitConverter.ToUInt16(image,pe+20);
        for(int i=0;i<count;i++){int h=pe+24+optional+i*40,start=BitConverter.ToInt32(image,h+12),size=BitConverter.ToInt32(image,h+16);
            if(rva>=start && rva<start+size)return BitConverter.ToInt32(image,h+20)+rva-start;}
        throw new InvalidOperationException("Unmapped test RVA");
    }
    private static void Probe(byte[] image,int rva,int size,byte[] entry)
    {
        // This separate process owns private executable copies. Published probes
        // and their memory remain rooted until process exit, like the real hooks.
        IntPtr copy=VirtualAlloc(IntPtr.Zero,new UIntPtr((uint)size),0x3000,0x40);
        Check(copy!=IntPtr.Zero,"private executable copy");Marshal.Copy(image,Offset(image,rva),copy,size);
        for(int i=0;i<entry.Length;i++)Check(Marshal.ReadByte(copy,i)==entry[i],"copied prologue");
        SoundRuntime.UnitTick callback=()=>{};
        var request=new DetourRequest<SoundRuntime.UnitTick>{Name="private idle-sound probe",TargetAddress=unchecked((ulong)copy.ToInt64()),Callback=callback};
        var hook=(NativeDetour<SoundRuntime.UnitTick>)NativeContract.Backend(size).CreateDetour(in request);
        NativeContract.Validate(hook,entry.Length,request.TargetAddress);checks++;
        roots.Add(callback);roots.Add(hook);roots.Add(copy);
        hook.Enable();NativeContract.Validate(hook,entry.Length,request.TargetAddress);checks++;
        Check(hook.IsInstalled,"copied hook installed");
    }
    [DllImport("kernel32.dll",SetLastError=true)] private static extern IntPtr VirtualAlloc(IntPtr address,UIntPtr size,uint allocationType,uint protection);
}
