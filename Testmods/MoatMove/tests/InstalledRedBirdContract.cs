using System.Reflection;
using System.Runtime.InteropServices;

internal static class InstalledRedBirdContract
{
    internal static void Validate()
    {
        const string extender = @"E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition\BepInEx\plugins\000shcdese";
        foreach (string name in new[] { "Microsoft.Extensions.Logging.Abstractions", "Iced", "RedBird.Abstractions", "RedBird.Core", "RedBird.X64" })
            Assembly.LoadFrom(Path.Combine(extender, name + ".dll"));
        var assembly = Assembly.LoadFrom(Path.Combine(extender, "RedBird.X64.dll"));
        var type = assembly.GetType("RedBird.X64.Hooks.X64InlineHook", throwOnError: true)!;
        byte[] bytes = { 0x33,0xC0,0x8B,0xD6,0x48,0x89,0x05,0x8E,0x70,0xF1,0x05,0x49,0x8B,0xCF };
        IntPtr memory = Marshal.AllocHGlobal(64);
        try
        {
            for (int i = 0; i < 64; i++) Marshal.WriteByte(memory, i, 0x90);
            Marshal.Copy(bytes, 0, memory, bytes.Length);
            // Decode-only candidate over private fixture memory. Never Generate/Enable.
            object candidate = Activator.CreateInstance(type,
                new object[] { unchecked((ulong)memory.ToInt64()), 14, null!, "MoatMove recovery span test" })!;
            try
            {
                if ((int)type.GetProperty("DisplacedByteCount")!.GetValue(candidate)! != 14 ||
                    (bool)type.GetProperty("IsInstalled")!.GetValue(candidate)!)
                    throw new Exception("Installed RedBird recovery span differs from audited 14 bytes.");
            }
            finally { ((IDisposable)candidate).Dispose(); }
            byte[] after = new byte[bytes.Length];
            Marshal.Copy(memory, after, 0, after.Length);
            if (!after.SequenceEqual(bytes)) throw new Exception("Decode-only probe modified fixture bytes.");
        }
        finally { Marshal.FreeHGlobal(memory); }
        Console.WriteLine("PASS: installed RedBird decode-only candidate displaces exactly 14 bytes; no hook installed.");
    }
}
