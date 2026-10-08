
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Iced.Intel;
using RedBird.Core.Memory;
using RedBird.X64.Extensions;
using RedBird.X64.Hooks;
using static Iced.Intel.AssemblerRegisters;

static class Probe {
    static NativeStateBlock state = new(4096);
    static NativeFlagArray customEngageRangeArrayFlag = state.AllocateFlags(128);
    static NativeStateArray<short> defaultEngageRangeArray = state.AllocateArray<short>(128);
    static NativeStateArray<short> customEngageRangeArray = state.AllocateArray<short>(128);
    static ulong ContextAddress = 0x10000000, ManagerAddress = 0x20000000;
    static void Check(bool value,string message) { if (!value) throw new Exception(message); }
    static void Emit(Assembler asm, ReadOnlySpan<Instruction> overwritten, ulong returnAddress) {
Span<Instruction> cloned = overwritten.CloneInstructionsWithoutIP();
                    Register operand = cloned[0].Op1Register.GetFullRegister();
                    Register memoryBase = cloned[0].MemoryBase.GetFullRegister();
                    Register memoryIndex = cloned[0].MemoryIndex.GetFullRegister();
                    // The override register must not participate in the memory address.
                    Register spare = new[] { Register.RAX, Register.RCX, Register.RDX, Register.R8, Register.R9, Register.R10, Register.R11, Register.R12 }
                        .First(r => r != operand && r != memoryBase && r != memoryIndex);

                    Register spareWord = (Register)((int)Register.AX + (int)spare - (int)Register.RAX);
                    AssemblerRegister64 savedOperand = new(spare);
                    AssemblerRegister64 origCmpFull = new(operand);

                    asm.AddInstruction(overwritten[0]);
                    // ex: cmp     [r9+rdi+8FEh], r8w
                    Label lblVanilla = asm.CreateLabel();
                    Label lblDone = asm.CreateLabel();
                    Label lblCached = asm.CreateLabel();

                    asm.push(savedOperand);
                    asm.mov(savedOperand, origCmpFull);
                    asm.push(r13);
                    asm.push(r14);
                    asm.push(r15);
                    asm.pushfq();

                    asm.mov(r13, (UInt64)ContextAddress);
                    asm.mov(r13d, __dword_ptr[r13]);
                    asm.mov(r15, (UInt64)ManagerAddress);
                    asm.add(r15, (0x65C + 0x8A)); // unitChimp offset
                    asm.imul(r14, r13, 0x490);
                    asm.add(r15, r14);
                    // r15 = unitTypeAddr
                    asm.movsx(r15d, __word_ptr[r15]);
                    // r15 = unitType

                    // cache the vanilla value once (register is still untouched here)
                    // this is not without problems, see known issues.
                    // savedOperand retains that value even when the source was r13-r15
                    asm.mov(r14, defaultEngageRangeArray.Address);
                    asm.cmp(__word_ptr[r14 + r15 * 2], 0);
                    asm.jnz(lblCached);
                    asm.AddInstruction(Instruction.Create(Code.Mov_rm16_r16, new MemoryOperand(Register.R14, Register.R15, 2), spareWord)); // ex: r8w (saved in spareWord)
                    asm.Label(ref lblCached);
                    // custom value test
                    asm.TestNativeFlag(customEngageRangeArrayFlag, r15, r14);
                    asm.jz(lblVanilla);

                    // custom loader
                    asm.mov(r14, customEngageRangeArray.Address);
                    asm.movsx(savedOperand, __word_ptr[r14 + r15 * 2]);
                    asm.lea(rsp, __[rsp + 8]);  // drop saved flags without touching flags
                    asm.pop(r15);
                    asm.pop(r14);
                    asm.pop(r13);
                    // All original address registers are restored before the compare.
                    Instruction customCompare = cloned[0];
                    customCompare.Op1Register = spareWord;
                    asm.AddInstruction(customCompare);
                    asm.pop(savedOperand);
                    asm.jmp(lblDone);

                    // vanilla: restore the original flags
                    asm.Label(ref lblVanilla);
                    asm.popfq();
                    asm.pop(r15);
                    asm.pop(r14);
                    asm.pop(r13);
                    asm.pop(savedOperand);

                    asm.Label(ref lblDone);
                    asm.AddInstructions(overwritten[1..]);
    }
    public static void Main(string[] args) {
        var sites = JsonDocument.Parse(File.ReadAllText(args[0]));
        var output = new List<object>();
        foreach (var site in sites.RootElement.EnumerateArray()) {
            byte[] bytes = Convert.FromHexString(site.GetProperty("bytes").GetString()!);
            int length=site.GetProperty("length").GetInt32();
            IntPtr copy=Marshal.AllocHGlobal(bytes.Length);
            try {
                Marshal.Copy(bytes,0,copy,bytes.Length);
                ulong target=(ulong)copy.ToInt64();
                using var hook=new X64InlineHook(target,14);
                Check(hook.DisplacedByteCount==length,"Backend displacement mismatch");
                hook.Generate((asm,original,continuation)=>{
                    Check(continuation==target+(ulong)length,"Continuation mismatch");
                    Emit(asm,original,continuation);
                });
                Check(!hook.IsInstalled && hook.StubAddress!=IntPtr.Zero,"Unpublished stub required");
                var asm=(Assembler)typeof(X64InlineHook).GetField("_asm",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(hook)!;
                using var stream=new MemoryStream();
                var writer=new StreamCodeWriter(stream);
                Check(asm.TryAssemble(writer,(ulong)hook.StubAddress.ToInt64(),out var error,out _),error);
                byte[] stub=stream.ToArray(),actual=new byte[stub.Length];
                Marshal.Copy(hook.StubAddress,actual,0,actual.Length);
                Check(stub.SequenceEqual(actual),"Published stub bytes differ from Iced assembly");
                output.Add(new {rva=site.GetProperty("rva").GetInt32(),length,target,
                    stubAddress=(ulong)hook.StubAddress.ToInt64(),stub=Convert.ToHexString(stub),
                    context=ContextAddress,manager=ManagerAddress,
                    defaults=defaultEngageRangeArray.Address,custom=customEngageRangeArray.Address,
                    flags=customEngageRangeArrayFlag.Address,
                    redbird=typeof(X64InlineHook).Assembly.GetName().Version!.ToString(),
                    baseRegister=site.GetProperty("base").GetString(),indexRegister=site.GetProperty("index").GetString(),
                    operand=site.GetProperty("operand").GetString()});
            } finally { Marshal.FreeHGlobal(copy); }
        }
        File.WriteAllText(args[1],JsonSerializer.Serialize(output,new JsonSerializerOptions{WriteIndented=true}).Replace("\r\n","\n").Replace("\n","\r\n"));
        state.Dispose();
        Console.WriteLine($"PASS: real RedBird/Iced assembled {output.Count} unpublished engage stubs; no hooks enabled.");
    }
}
