"""Hash-bound offline engage-range audit; never opens or patches the game process."""
from pathlib import Path
import capstone
import hashlib
import json
import pefile
import re
import sqlite3

ROOT = Path(__file__).resolve().parents[2]
OUT = Path(__file__).resolve().parent
GAME = Path(r'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition')
NATIVE = GAME / 'Stronghold Crusader Definitive Edition_Data/Plugins/x86_64/CrusaderDE.dll'
HASH = 'FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2'

def write(path, text):
    path.parent.mkdir(parents=True, exist_ok=True)
    expected = text.replace('\r\n', '\n').replace('\n', '\r\n').encode('utf-8')
    path.write_bytes(expected)
    assert path.read_bytes() == expected

def dump(path, value):
    write(path, json.dumps(value, indent=2) + '\n')

binary = NATIVE.read_bytes()
assert hashlib.sha256(binary).hexdigest().upper() == HASH
pe = pefile.PE(data=binary)
base = pe.OPTIONAL_HEADER.ImageBase
db = sqlite3.connect(f'file:{ROOT / "_inspect/CrusaderDE-Native-Baseline/sem/FBCB9319/CrusaderDE-semantic.sqlite"}?mode=ro', uri=True)
md = capstone.Cs(capstone.CS_ARCH_X86, capstone.CS_MODE_64)
md.detail = True
asint = lambda x: int(x, 16) if isinstance(x, str) else x
refs = [(asint(a), asint(b), t) for a,b,t in db.execute(
    'select from_rva,to_rva,type from xrefs where binary_hash=? and from_rva is not null and to_rva is not null', (HASH,))]
vtable_hits = list(re.finditer(b'\x41\xff\x94\xc6....\x8b\x15', binary, re.S))
assert len(vtable_hits) == 1
vtable_rva = int.from_bytes(binary[vtable_hits[0].start()+4:vtable_hits[0].start()+8], 'little')
names = re.findall(r'public delegate\* unmanaged\[Cdecl\]<Int64> (\w+);',
    (ROOT/'shcde-script-extender/src/SHCDESE.BepInEx/Interop/VTables/UnitFunctionsVTable.cs').read_text())
selected = ['ArcherUpdate','ArabBallistaUpdate','ArabBowUpdate','BallistaUpdate','TrebuchetUpdate',
    'CatapultUpdate','MangonelUpdate','ArabSlingerUpdate','BedouinHeavyCamelUpdate',
    'BedouinSkirmisherUpdate','XbowmanUpdate','ArabGrenadierUpdate','ArabHorsemanUpdate']
functions, sites = [], []
for name in selected:
    address = int.from_bytes(pe.get_data(vtable_rva+names.index(name)*8,8),'little')
    rva = address-base
    # Reproduce the inspected RedBird 1.5 ReachableTerminator boundary, including
    # forward branches but excluding CALLs and indirect jump-table dispatches.
    far = address
    instructions = []
    for ins in md.disasm(pe.get_data(rva,65536),address):
        instructions.append(ins)
        direct = ins.group(capstone.CS_GRP_JUMP) and ins.operands and ins.operands[0].type == capstone.x86.X86_OP_IMM
        if direct and far < ins.operands[0].imm < address+65536:
            far = ins.operands[0].imm
        if (ins.mnemonic == 'int3' or ins.group(capstone.CS_GRP_RET) or
            (ins.mnemonic == 'jmp' and direct)) and ins.address+ins.size > far:
            break
        assert len(instructions) < 16384
    size = instructions[-1].address+instructions[-1].size-address
    recorded = db.execute('select size,confidence from functions where binary_hash=? and rva=?',
        (HASH,f'0x{rva:X}')).fetchone()
    assert recorded and recorded[0] == size
    matches = [ins for ins in instructions if ins.mnemonic == 'cmp' and len(ins.operands) == 2
        and ins.operands[0].type == capstone.x86.X86_OP_MEM and ins.operands[0].mem.disp == 0x8fe
        and ins.operands[1].type == capstone.x86.X86_OP_REG]
    functions.append(dict(role=name,rva=rva,size=size,confidence=recorded[1],matchCount=len(matches)))
    for first in matches:
        block, length = [], 0
        for ins in instructions[instructions.index(first):]:
            block.append(ins); length += ins.size
            if length >= 14: break
        start, end = first.address-base, first.address-base+length
        incoming = [(a,b,t) for a,b,t in refs if start < b < end and not start <= a < end and t != 'FALL_THROUGH']
        assert not incoming, (hex(start),incoming)
        for ins in instructions:
            if ins.group(capstone.CS_GRP_JUMP) or ins.group(capstone.CS_GRP_CALL):
                for op in ins.operands:
                    assert not (op.type == capstone.x86.X86_OP_IMM and first.address < op.imm < first.address+length
                        and not first.address <= ins.address < first.address+length)
        next_ins = instructions[instructions.index(first)+len(block)]
        sites.append(dict(role=name,rva=start,length=length,continuationRva=end,
            compare=first.op_str,base=first.reg_name(first.operands[0].mem.base),
            index=first.reg_name(first.operands[0].mem.index),operand=first.reg_name(first.operands[1].reg),
            bytes=pe.get_data(start,64).hex(),instructions=[dict(rva=i.address-base,length=i.size,
                text=i.mnemonic+' '+i.op_str) for i in block],
            continuationInstruction=next_ins.mnemonic+' '+next_ins.op_str,incomingEdges=[]))
dump(OUT/'native-sites.json',sites)
dump(OUT/'native-audit.json',dict(nativeHash=HASH,vtableRva=vtable_rva,functions=functions,sites=sites))

# Compile the actual upstream lambda body without rewriting the extender fork.
source = (ROOT/'shcde-script-extender/src/SHCDESE.BepInEx/Detours/BulkUnitDetours.cs').read_text()
start = source.index('Span<Instruction> cloned = overwritten.CloneInstructionsWithoutIP();',source.index('// Scan attack range'))
end = source.index('}, name: $"UnitUpdateEngageRange_',start)
emitter = source[start:end].strip()
emitter = emitter.replace('GameGlobalsManager.Instance.CurrentContextUnitIdVA','ContextAddress')
emitter = emitter.replace('GameGlobalsManager.Instance.GameUnitManagerVA','ManagerAddress')
dump(OUT/'emitter-provenance.json',dict(sourceCommit='5908e1f12437deb7ef5f5b1dc2d64e301178fba3',
    sourcePath='src/SHCDESE.BepInEx/Detours/BulkUnitDetours.cs',lambdaSha256=hashlib.sha256(source[start:end].encode()).hexdigest(),
    substitutions=['CurrentContextUnitIdVA -> scratch ContextAddress','GameUnitManagerVA -> scratch ManagerAddress']))
project = (ROOT/'Shared/ScriptExtenderUpdate/2.10.4-2.11.0.HookProbe/HookProbe.csproj').read_text()
project = project.replace('<Nullable>enable</Nullable>','<Nullable>enable</Nullable><AllowUnsafeBlocks>true</AllowUnsafeBlocks>')
write(OUT/'EngageProbe.csproj',project)
template = r'''
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
EMITTER
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
'''
write(OUT/'Program.cs',template.replace('EMITTER',emitter))
print(f'PASS: {len(functions)} complete function boundaries; {len(sites)} sites, spans, continuations and incoming-edge checks.')
