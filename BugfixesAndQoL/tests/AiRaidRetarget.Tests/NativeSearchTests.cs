using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Iced.Intel;
using RedBird.X64.Hooks;
using BugfixesAndQoL;
using static BugfixesAndQoL.RaidAttackFieldEvaluation;
using static Iced.Intel.AssemblerRegisters;

internal static class NativeSearchTests
{
    private static int assertions;
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); assertions++; }
    private static AttackCandidateSnapshot Empty() => new AttackCandidateSnapshot(true, 0, Array.Empty<CandidateRecord>());
    private static RaidSearchEvidence Frame(int building = 808, uint global = 1373353) =>
        new RaidSearchEvidence(1, 2, 3, 4, 4390, 1375870, building, global, new IntPtr(123));
    private static void Observe(RaidSearchEvidence frame, int building = 808, uint global = 1373353,
        int thread = 4, long epoch = 2, long session = 1, uint tribeGlobal = 1375870, long context = 123) =>
        frame.Observe(session, epoch, thread, 4390, tribeGlobal, building, global, new IntPtr(context), Empty());

    internal static void Run()
    {
        Check(!RaidSearchObserver.IsAvailable, "Absent hook reports correction unavailable before publication");
        var first = Frame(); Observe(first);
        var second = Frame(); Observe(second);
        Check(first.GetFreshness(true, Empty()) == "nativeSearchObserved" &&
            second.GetFreshness(true, Empty()) == "nativeSearchObserved", "Two identical freshly computed empty lists");
        Check(Frame().GetFreshness(true, Empty()) == "searchCompletionNotObserved", "Bypassed search not classified unreachable");
        Check(first.GetFreshness(false, Empty()) == "unmatchedPrePost", "Pre/post mismatch");
        Check(first.GetFreshness(true, new AttackCandidateSnapshot(true, 1,
            new[] { new CandidateRecord(100,101,1) })) == "decisionPostSnapshotMismatch", "Post overwrite");
        var reusedBuilding = Frame(); Observe(reusedBuilding, global: 77);
        Check(reusedBuilding.Association == "buildingIdentityMismatch", "Reused building ID");
        var reusedTribe = Frame(); Observe(reusedTribe, tribeGlobal: 77);
        Check(reusedTribe.Association == "tribeIdentityMismatch", "Reused tribe ID");
        var newMap = Frame(); Observe(newMap, epoch: 3);
        Check(newMap.Association == "sessionOrEpochChanged", "Map epoch");
        var newSession = Frame(); Observe(newSession, session: 2);
        Check(newSession.Association == "sessionOrEpochChanged", "Session identity");
        var otherThread = Frame(); Observe(otherThread, thread: 5);
        Check(otherThread.Association == "threadMismatch", "Thread association");
        var nullContext = Frame(); Observe(nullContext, context: 0);
        Check(nullContext.Association == "contextUnavailable", "Missing context");
        var wrongContext = Frame(); Observe(wrongContext, context: 456);
        Check(wrongContext.Association == "contextMismatch", "Different context");
        var changedRole = new RaidSearchEvidence(1,2,3,4,4390,1375870,808,1373353,new IntPtr(123),6,0);
        changedRole.Observe(1,2,4,4390,1375870,808,1373353,new IntPtr(123),Empty(),6,1);
        Check(changedRole.Association == "raidRoleOrOwnerMismatch", "Role reassignment");
        var repeated = Frame(); Observe(repeated); Observe(repeated);
        Check(repeated.GetFreshness(true, Empty()) == "multipleSearchCompletions", "Multiple consumer returns");
        var nesting = new Stack<RaidSearchEvidence>(); var outer = Frame(); var inner = Frame(119,6450);
        nesting.Push(outer); nesting.Push(inner); Observe(nesting.Peek(),119,6450); nesting.Pop(); Observe(nesting.Peek());
        Check(inner.Association == "matchedNativeConsumerReturn" && outer.Association == inner.Association,
            "Nested commands retain separate observations");
        Check(Frame().Describe(true, Empty()).Contains("searchObserved=False"), "Missing search logged");
        Check(first.DecisionSnapshot.SameRecords(Empty()), "Observer leaves source snapshot unchanged");
        TestCorrectionClassification();
        TestBackendAndEmission();
        ExecuteBranch(false); ExecuteBranch(true);
        Console.WriteLine($"PASS: native search evidence and assembler tests ({assertions} assertions).");
    }

    private static AttackResult Classify(RaidSearchEvidence frame, AttackCandidateSnapshot post,
        bool paired = true) => Evaluate(post, paired, frame.GetFreshness(paired, post), 1,
            true, 119, 1000, tile => tile == 101 ? 119 : 120,
            (stand, building) => stand == 100 && building == 101, out _);

    private static void TestCorrectionClassification()
    {
        var empty = Empty();
        var pair = new AttackCandidateSnapshot(true, 1, new[] { new CandidateRecord(100,101,1) });
        var first = Frame(); Observe(first);
        var second = Frame(); Observe(second);
        Check(Classify(first, empty) == AttackResult.NoAttackPoint &&
            Classify(second, empty) == AttackResult.NoAttackPoint, "Consecutive fresh empty searches drive rejection");
        var valid = Frame();
        valid.Observe(1,2,4,4390,1375870,808,1373353,new IntPtr(123),pair);
        Check(Classify(valid, pair) == AttackResult.AttackPoint, "Unchanged valid pair remains valid");
        Check(Classify(Frame(), pair) == AttackResult.Unknown, "Changed content without marker cannot drive correction");
        Check(Classify(first, pair) == AttackResult.Unknown, "Conflicting Post cannot continue retry");
        Check(Classify(Frame(), empty) == AttackResult.Unknown, "Missing hook cannot reject empty target");
        Check(Classify(valid, pair, false) == AttackResult.Unknown, "Post role/context/identity mismatch cannot correct");
        var repeated = Frame(); Observe(repeated); Observe(repeated);
        Check(Classify(repeated, empty) == AttackResult.Unknown, "Multiple completions cannot correct");
        var reused = Frame(); Observe(reused, global: 77);
        Check(Classify(reused, empty) == AttackResult.Unknown, "Reused building ID cannot reject new identity");
        var role = new RaidSearchEvidence(1,2,3,4,4390,1375870,808,1373353,new IntPtr(123),6,0);
        role.Observe(1,2,4,4390,1375870,808,1373353,new IntPtr(123),empty,6,1);
        Check(Classify(role, empty) == AttackResult.Unknown, "Reassigned raid role cannot correct");
        var invalid = new AttackCandidateSnapshot(true,-1,new CandidateRecord[500]);
        var invalidFrame = Frame();
        invalidFrame.Observe(1,2,4,4390,1375870,808,1373353,new IntPtr(123),invalid);
        Check(Classify(invalidFrame,invalid) == AttackResult.Unknown, "Observed unterminated list remains unknown");
        // Exercise the shared result consumed by the existing N/N/A retry branches.
        var results = new[] { Classify(first,empty), Classify(second,empty), Classify(valid,pair) };
        Check(results[0] == AttackResult.NoAttackPoint && results[1] == AttackResult.NoAttackPoint &&
            results[2] == AttackResult.AttackPoint, "Multiple negative candidates followed by usable replacement");
    }

    private static byte[] Assemble(Assembler asm, ulong address)
    {
        using (var stream = new MemoryStream())
        {
            if (!asm.TryAssemble(new StreamCodeWriter(stream), address, out string error, out AssemblerResult result))
                throw new Exception(error);
            return stream.ToArray();
        }
    }

    private static Instruction[] Decode(byte[] bytes, ulong address)
    {
        var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes)); decoder.IP = address;
        var instructions = new List<Instruction>();
        while (decoder.IP < address + (ulong)bytes.Length) {
            var instruction = decoder.Decode(); Check(!instruction.IsInvalid, "Generated instruction decodes");
            instructions.Add(instruction);
        }
        return instructions.ToArray();
    }

    private static void TestBackendAndEmission()
    {
        ulong module = 0x180000000;
        var original = Decode(RaidSearchObserver.ExpectedBytes, module + RaidSearchObserver.SiteRva);
        RaidSearchObserver.ValidateOriginal(original, module, module + RaidSearchObserver.SiteRva + 17);
        var asm = new Assembler(64);
        RaidSearchObserver.Emit(asm, original, module + 0x1000, module + RaidSearchObserver.ContextRva);
        var generated = Decode(Assemble(asm, module + 0x300000), module + 0x300000);
        Check(generated[0].Code == Code.Call_rel32_64 && generated[0].NearBranch64 == module + 0x123090,
            "Original filter is first and retains live target");
        Check(generated[generated.Length-2].Mnemonic == Mnemonic.Cmp && generated[generated.Length-1].Mnemonic == Mnemonic.Je,
            "Original field gate is last");
        int xmm = 0; foreach(var i in generated) if(i.Mnemonic == Mnemonic.Movdqu) xmm++;
        Check(xmm == 12, "All six volatile XMM registers saved and restored");
        IntPtr memory = VirtualAlloc(IntPtr.Zero, (UIntPtr)64, 0x3000, 0x40);
        if (memory == IntPtr.Zero) throw new Exception("Allocation failed");
        X64InlineHook hook = null;
        try {
            Marshal.Copy(RaidSearchObserver.ExpectedBytes,0,memory,17);
            hook = new X64InlineHook(unchecked((ulong)memory.ToInt64()),14);
            Check(hook.DisplacedByteCount == 17, "Installed backend rounds 14 to audited 17 bytes");
            ulong fakeBase = unchecked((ulong)memory.ToInt64()) - RaidSearchObserver.SiteRva;
            hook.Generate((a,o,r) => {
                RaidSearchObserver.ValidateOriginal(o,fakeBase,r);
                RaidSearchObserver.Emit(a,o,fakeBase + 0x1000,fakeBase + RaidSearchObserver.ContextRva);
            });
            Check(!hook.IsInstalled && hook.StubAddress != IntPtr.Zero, "Same backend prepares copied span without live patch");
        } finally { hook?.Dispose(); VirtualFree(memory,UIntPtr.Zero,0x8000); }
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate void Observer(int tribe,int building,IntPtr context);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)] private delegate int Execute();
    [DllImport("kernel32.dll")] private static extern IntPtr VirtualAlloc(IntPtr at,UIntPtr size,uint type,uint protect);
    [DllImport("kernel32.dll")] private static extern bool VirtualFree(IntPtr at,UIntPtr size,uint type);

    private static void ExecuteBranch(bool usable)
    {
        IntPtr memory = VirtualAlloc(IntPtr.Zero,(UIntPtr)4096,0x3000,0x40);
        if(memory == IntPtr.Zero) throw new Exception("Allocation failed");
        try {
            ulong start = unchecked((ulong)memory.ToInt64()), field=start+0x700;
            int observed=0;
            Observer callback=(tribe,building,context) => {
                Check(tribe==4390 && building==808 && context==new IntPtr(123), "Native ABI observation arguments");
                Check(Marshal.ReadInt32(new IntPtr((long)field))==(usable?101:0), "Filter completed before callback"); observed++;
            };
            var consumer = new Assembler(64); consumer.mov(__dword_ptr[field],usable?101:0); consumer.ret();
            byte[] consumerBytes = Assemble(consumer,start+0x600); Marshal.Copy(consumerBytes,0,new IntPtr((long)start+0x600),consumerBytes.Length);
            var nativeOriginal = new Assembler(64);
            nativeOriginal.call(start+0x600); nativeOriginal.cmp(__dword_ptr[field],esi); nativeOriginal.je(start+0x800);
            var original=Decode(Assemble(nativeOriginal,start+0x500),start+0x500);
            var program = new Assembler(64);
            program.push(rsi); program.push(rdi); program.push(r14); program.sub(rsp,0x20);
            program.xor(esi,esi); program.mov(edi,4390); program.mov(r14d,808);
            RaidSearchObserver.Emit(program,original,unchecked((ulong)Marshal.GetFunctionPointerForDelegate(callback).ToInt64()),123);
            program.mov(eax,1); program.add(rsp,0x20); program.pop(r14); program.pop(rdi); program.pop(rsi); program.ret();
            byte[] bytes=Assemble(program,start); Marshal.Copy(bytes,0,memory,bytes.Length);
            var failed=new Assembler(64); failed.xor(eax,eax); failed.add(rsp,0x20); failed.pop(r14); failed.pop(rdi); failed.pop(rsi); failed.ret();
            byte[] failBytes=Assemble(failed,start+0x800); Marshal.Copy(failBytes,0,new IntPtr((long)start+0x800),failBytes.Length);
            int result=Marshal.GetDelegateForFunctionPointer<Execute>(memory)();
            Check(result==(usable?1:0) && observed==1,"Original gate branch and exactly one observation");
            GC.KeepAlive(callback);
        } finally { VirtualFree(memory,UIntPtr.Zero,0x8000); }
    }
}
