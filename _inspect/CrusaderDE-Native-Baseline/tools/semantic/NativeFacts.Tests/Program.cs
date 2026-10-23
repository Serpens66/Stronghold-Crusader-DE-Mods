using Iced.Intel;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using RedBird.X64.Hooks;

// All fixture memory is non-executable data. Never Generate/Enable/Apply/Undo a hook.
static class Program {
    static void Assert(bool condition,string text){if(!condition)throw new Exception(text);}
    static void Main(){
        string installed=@"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\000shcdese";
        foreach(string name in new[]{"Iced","RedBird.X64","RedBird.Backends.NativeX64"}){
            Assembly loaded=Assembly.Load(name);
            Assert(SHA256.HashData(File.ReadAllBytes(loaded.Location)).SequenceEqual(SHA256.HashData(File.ReadAllBytes(Path.Combine(installed,name+".dll")))),"Loaded backend/decoder differs from installed assembly: "+name);
        }
        byte[] bytes=Convert.FromHexString("85FF0F8422110000420FB78423D8090000");
        IntPtr buffer=Marshal.AllocHGlobal(bytes.Length+64);
        try{
            Marshal.Copy(bytes,0,buffer,bytes.Length);
            var hook=new X64InlineHook(unchecked((ulong)buffer.ToInt64()),7);
            Assert(hook.DisplacedByteCount==17,"Actual installed inline decoder must round requested seven to seventeen.");
        }finally{Marshal.FreeHGlobal(buffer);}
        Assembly backend=Assembly.Load("RedBird.Backends.NativeX64");
        Type jumps=backend.GetType("RedBird.Backends.NativeX64.JumpFactory",true)!;
        MethodInfo patch=jumps.GetMethod("GetPatchSize",BindingFlags.Public|BindingFlags.Static)!;
        Type scheme=patch.GetParameters()[0].ParameterType;
        foreach(var (name,size) in new[]{("Indirect",6),("Absolute",14),("PushRet",12),("PushRetRedZoneSafe",23)}){
            Assert((int)patch.Invoke(null,new[]{Enum.Parse(scheme,name)})! == size,"Installed schema changed: "+name);
        }
        Type prologue=backend.GetType("RedBird.Backends.NativeX64.PrologueAnalyzer",true)!;
        var map=prologue.GetMethod("BuildBranchMap",BindingFlags.NonPublic|BindingFlags.Static)!;
        var select=prologue.GetMethod("TrySelectPrologue",BindingFlags.Public|BindingFlags.Static)!;
        List<Instruction> Decode(byte[] b){var d=Decoder.Create(64,new ByteArrayCodeReader(b),0x1000);var result=new List<Instruction>();while(d.IP<0x1000UL+(ulong)b.Length){var ins=d.Decode();if(ins.IsInvalid)break;result.Add(ins);}return result;}
        int Select(byte[] b,int minimum){
            var ins=Decode(b);object?[] args={0x1000UL,ins,map.Invoke(null,new object[]{ins}),minimum,NullLogger.Instance,null};
            Assert((bool)select.Invoke(null,args)!,"Actual prologue selection failed.");
            return (int)args[5]!.GetType().GetProperty("Length")!.GetValue(args[5])!;
        }
        Assert(Select(Convert.FromHexString("4963C08B8CC1B8171800B867666666C3"),6)==10,"Installed market prefix selection must be ten bytes.");
        byte[] branch=Enumerable.Repeat((byte)0x90,24).ToArray();branch[20]=0xEB;branch[21]=0xEC;branch[22]=0xC3;
        Assert(Select(branch,6)==22,"Installed self-branch widening must cover the incoming branch source.");
        Console.WriteLine("Installed RedBird offline contracts: inline rounding, four schemas, market prefix and self-branch widening passed.");
    }
}
