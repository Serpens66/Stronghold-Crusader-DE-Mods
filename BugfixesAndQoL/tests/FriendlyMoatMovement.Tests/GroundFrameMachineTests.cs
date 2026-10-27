using System.Runtime.InteropServices;
using BugfixesAndQoL;
using Iced.Intel;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging.Abstractions;
using RedBird.X64.Assembly;
using static Iced.Intel.AssemblerRegisters;

internal static unsafe class GroundFrameMachineTests
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate long SixArguments(long a, long b, long c, long d, long e, long f);
    private static readonly List<object> roots = new();

    internal static void Validate(string root)
    {
        // Execute the actual production demand guard around the actual render-entry
        // instructions. A native active body replaces only Mono's context transition.
        var syntax = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root,
            "BugfixesAndQoL/src/Formations/GroundMoveFrameObserver.cs"))).GetRoot();
        var declaration = syntax.DescendantNodes().OfType<VariableDeclaratorSyntax>()
            .Single(v => v.Identifier.Text == "EntryBytes");
        byte[] entry = ((InitializerExpressionSyntax)declaration.Initializer!.Value).Expressions
            .Select(e => Convert.ToByte(((LiteralExpressionSyntax)e).Token.Value)).ToArray();
        var function = ExecutableFunction.Create(a => {
            a.db(entry);
            a.mov(rax, rcx); a.imul(rax, rax, 3); a.add(rax, rdx);
            a.imul(rax, rax, 3); a.add(rax, r8); a.imul(rax, rax, 3); a.add(rax, r9);
            a.imul(rax, rax, 3); a.add(rax, __qword_ptr[rsp + 0x28]);
            a.imul(rax, rax, 3); a.add(rax, __qword_ptr[rsp + 0x30]); a.ret();
        });
        roots.Add(function);
        IntPtr demand = Marshal.AllocHGlobal(1), counter = Marshal.AllocHGlobal(4);
        Marshal.WriteByte(demand, 0); Marshal.WriteInt32(counter, 0);
        roots.Add(demand); roots.Add(counter);
        var hook = DemandGatedContextHook.CreateGated(function.Address, 14,
            _ => (a, original, resume) => {
                a.pushfq(); a.push(rax); a.mov(rax, unchecked((ulong)counter.ToInt64()));
                a.inc(__dword_ptr[rax]); a.pop(rax); a.popfq();
            }, demand, "groundRenderEntryFixture", NullLogger.Instance);
        roots.Add(hook);
        if (hook.DisplacedByteCount != entry.Length || entry.Length != 15)
            throw new Exception("Render-entry displacement changed");
        hook.Enable();
        SixArguments run = function.GetDelegate<SixArguments>();
        int expected = 0;
        foreach (byte active in new byte[] { 0, 1, 1, 0, 255, 0 })
        {
            Marshal.WriteByte(demand, active);
            foreach (long first in new long[] { 1, -7, 0x123456789 })
            {
                long result = run(first, 2, 3, 4, 5, 6);
                long wanted = (((((first * 3 + 2) * 3 + 3) * 3 + 4) * 3 + 5) * 3 + 6);
                if (active != 0) expected++;
                if (result != wanted || Marshal.ReadInt32(counter) != expected)
                    throw new Exception("Render observer changed arguments/stack or demand transition count");
            }
        }
        Marshal.WriteByte(demand, 0);
        Console.WriteLine("PASS: real 15-byte render-entry hook; six Win64 arguments, stack, dormant bypass and one active callback per pass.");
    }
}
