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
        foreach (var entry in new[] {
            ("196100", "4883EC484863C24C8D1D1206B307", 14),
            ("12BF0", "40534883EC308B05F0635E08C705EE635E080F000000", 22),
            ("11C3A0", "4C635C24284C63D24969C2880600004969D2A2010000", 15) })
        {
            byte[] prefix = Convert.FromHexString(entry.Item2);
            IntPtr fixture = Marshal.AllocHGlobal(64);
            try
            {
                for (int i = 0; i < 64; i++) Marshal.WriteByte(fixture, i, 0x90);
                Marshal.Copy(prefix, 0, fixture, prefix.Length);
                object candidate = Activator.CreateInstance(type,
                    new object[] { unchecked((ulong)fixture.ToInt64()), 14, null!, "MoatMove Fast " + entry.Item1 })!;
                try
                {
                    if ((int)type.GetProperty("DisplacedByteCount")!.GetValue(candidate)! != entry.Item3 ||
                        (bool)type.GetProperty("IsInstalled")!.GetValue(candidate)!)
                        throw new Exception("Installed RedBird Fast entry span changed: " + entry.Item1);
                }
                finally { ((IDisposable)candidate).Dispose(); }
                byte[] after = new byte[prefix.Length]; Marshal.Copy(fixture, after, 0, after.Length);
                if (!after.SequenceEqual(prefix)) throw new Exception("Fast decode probe modified memory.");
            }
            finally { Marshal.FreeHGlobal(fixture); }
        }
        Console.WriteLine("PASS: installed RedBird decode-only candidate displaces exactly 14 bytes; no hook installed.");
        Console.WriteLine("PASS: installed RedBird Fast entry spans 14/22/15 bytes; decode-only, no hooks installed.");
    }
}
