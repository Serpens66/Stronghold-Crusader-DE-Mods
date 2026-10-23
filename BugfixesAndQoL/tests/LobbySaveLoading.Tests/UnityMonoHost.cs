// Isolated test host for the installed game's actual Mono compiler; no game methods are executed.
using System;
using System.IO;
using System.Runtime.InteropServices;

internal static class UnityMonoHost
{
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryW(string path);
    private const string Mono = "mono-2.0-bdwgc.dll";
    [DllImport(Mono, CallingConvention = CallingConvention.Cdecl)] private static extern void mono_set_dirs(string libs, string config);
    [DllImport(Mono, CallingConvention = CallingConvention.Cdecl)] private static extern void mono_set_assemblies_path(string path);
    [DllImport(Mono, CallingConvention = CallingConvention.Cdecl)] private static extern void mono_config_parse(string path);
    [DllImport(Mono, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr mono_jit_init_version(string name, string version);
    [DllImport(Mono, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr mono_domain_assembly_open(IntPtr domain, string path);
    [DllImport(Mono, CallingConvention = CallingConvention.Cdecl)] private static extern int mono_jit_exec(IntPtr domain, IntPtr assembly, int argc,
        [In, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPStr)] string[] argv);

    private static int Main(string[] args)
    {
        if (args.Length != 2) return 100;
        string game = Path.GetFullPath(args[0]), test = Path.GetFullPath(args[1]);
        string managed = Path.Combine(game, "Stronghold Crusader Definitive Edition_Data", "Managed");
        string mono = Path.Combine(game, "MonoBleedingEdge", "EmbedRuntime", Mono);
        if (!File.Exists(test) || !File.Exists(mono) || LoadLibraryW(mono) == IntPtr.Zero) return 101;
        mono_set_dirs(managed, Path.Combine(game, "MonoBleedingEdge", "etc"));
        mono_set_assemblies_path(managed + ";" + Path.Combine(game, "BepInEx", "core") + ";" +
            Path.Combine(game, "BepInEx", "plugins", "000shcdese") + ";" + Path.GetDirectoryName(test));
        mono_config_parse(null);
        IntPtr domain = mono_jit_init_version("BugfixesAndQoL-lobby-save-tests", "v4.0.30319");
        if (domain == IntPtr.Zero) return 102;
        IntPtr assembly = mono_domain_assembly_open(domain, test);
        if (assembly == IntPtr.Zero) return 103;
        return mono_jit_exec(domain, assembly, 1, new[] { test });
    }
}
