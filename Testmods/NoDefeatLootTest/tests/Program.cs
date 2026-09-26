using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace NoDefeatLootTest
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length != 1 || !Directory.Exists(args[0]))
                throw new ArgumentException("Pass the installed Script Extender directory.");
            string extenderDir = args[0];
            AppDomain.CurrentDomain.AssemblyResolve += (_, name) =>
            {
                string path = Path.Combine(extenderDir, new System.Reflection.AssemblyName(name.Name).Name + ".dll");
                return File.Exists(path) ? System.Reflection.Assembly.LoadFrom(path) : null;
            };
            Run();
            return 0;
        }

        private static void Run()
        {
            byte[] source = new byte[64];
            byte[] guard = {
                0x0F, 0x84, 0x46, 0x09, 0x00, 0x00,
                0x66, 0x42, 0x83, 0xBC, 0x23, 0x48, 0x0A, 0x00, 0x00, 0x00
            };
            for (int i = 0; i < source.Length; i++)
                source[i] = 0x90;
            Buffer.BlockCopy(guard, 0, source, 0, guard.Length);
            IntPtr copy = Marshal.AllocHGlobal(source.Length);
            try
            {
                Marshal.Copy(source, 0, copy, source.Length);
                ulong address = unchecked((ulong)copy.ToInt64());
                using (var probe = new X64InlineHook(address, guard.Length))
                {
                    var options = new ContextHookOptions
                    {
                        Registers = X64SmartCPUContextRegs.All,
                        HookSize = guard.Length,
                        Placement = OverwrittenInstructionPlacement.AfterCallback,
                        InstructionSelector = RewardGuardStubContract.SelectInstructions
                    };
                    // Desktop CLR cannot marshal RedBird's generic NativePointer callback.
                    // Invoke the installed backend's context assembler directly; the game
                    // repeats the check through ContextStub.CreateGenerator under Mono.
                    Type contextGenerator = typeof(ContextStub).Assembly.GetType(
                        "RedBird.X64.Assembly.ContextAssemblyGenerator", throwOnError: true);
                    MethodInfo generate = contextGenerator.GetMethod("Generate",
                        BindingFlags.Public | BindingFlags.Static);
                    if (generate == null)
                        throw new InvalidOperationException("Installed RedBird context generator is unavailable.");
                    probe.Generate((assembler, original, returnAddress) =>
                    {
                        generate.Invoke(null, new object[] {
                            assembler, new IntPtr(0x12345678), options.Registers
                        });
                        foreach (Instruction instruction in options.InstructionSelector(original.ToArray()))
                            assembler.AddInstruction(instruction);
                    });
                    RewardGuardStubContract.VerifyEmitted(probe, address + 0x94C);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(copy);
            }
            Console.WriteLine("No Defeat Loot Test: installed RedBird stub and Iced branch contract passed.");
        }

    }
}
