using ImprovedHunters;
using Iced.Intel;
using RedBird.X64.Hooks;
using System.Runtime.InteropServices;
using static Iced.Intel.AssemblerRegisters;

/// <summary>
/// Executes the real stub generators on copies of the audited DLL's bytes with chosen
/// register and memory contents. Catches wrong branch directions, clobbered registers and
/// lost flags, which assembling and decoding alone cannot show. Native offsets here are
/// the audited contract of FBCB9319; other DLLs are rejected by the pattern tests first.
/// </summary>
internal static unsafe class NativeStubExecutionTests
{
    private const int Hunter = 6;
    private const int Rabbit = 46;
    private const int Deer = 44;
    private const int Chicken = 62;
    private const int Archer = 22;
    private const int UnitStride = 0x490;
    private const int TribeStride = 0x688;
    private const int TeamDisplacement = 0x37EDF3C;
    private const int ProjectileOwnerDisplacement = 0x2F7A6C0;
    private const int ProjectileHeightDisplacement = 0x2F7A6D0;
    private const int ProjectileSourceDisplacement = 0x2F7A736;

    // Sentinel results; a stub that reaches the wrong exit fails the comparison.
    private const int ResultFallthrough = 0x11;
    private const int ResultReject = 0x22;
    private const int ResultContinue = 0x33;
    private const int ResultMiss = 0x44;
    private const int ResultNotAttackable = 0x55;
    private const int ResultAttackable = 0x66;
    private const int ResultNextChain = 0x77;

    private static int assertions;
    private static byte* flag;
    private static byte* units;

    [DllImport("kernel32", SetLastError = true)]
    private static extern IntPtr VirtualAlloc(IntPtr address, nuint size, uint type, uint protect);

    private static byte* Alloc(long size)
    {
        IntPtr memory = VirtualAlloc(IntPtr.Zero, (nuint)size, 0x3000, 0x40);
        if (memory == IntPtr.Zero)
            throw new InvalidOperationException("VirtualAlloc failed.");
        return (byte*)memory;
    }

    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition)
            throw new InvalidOperationException("Native stub: " + message);
    }

    private static void SetFlag(bool enabled) => *(int*)flag = enabled ? 1 : 0;

    private static ulong Address(byte* pointer) => unchecked((ulong)pointer);

    private static ulong ExitBlock(int result)
    {
        byte* code = Alloc(16);
        code[0] = 0xB8;
        *(int*)(code + 1) = result;
        code[5] = 0xC3;
        return Address(code);
    }

    private delegate void BodyEmitter(Assembler assembler, ReadOnlySpan<Instruction> instructions);

    /// <summary>Builds the stub over a copy of the audited bytes; the bytes after the span are the given tail.</summary>
    private static ulong BuildStub(byte[] image, int rva, int expectedDisplaced, byte[] tail, BodyEmitter body)
    {
        byte* copy = Alloc(256);
        Marshal.Copy(image, rva, (IntPtr)copy, expectedDisplaced);
        for (int index = 0; index < tail.Length; index++)
            copy[expectedDisplaced + index] = tail[index];

        var hook = new X64InlineHook(Address(copy), 14);
        Check(hook.DisplacedByteCount == expectedDisplaced,
            $"RVA 0x{rva:X} displaced {hook.DisplacedByteCount}, expected {expectedDisplaced}.");
        hook.Generate((assembler, instructions, returnAddress) => body(assembler, instructions));
        Check(hook.StubAddress != IntPtr.Zero, $"RVA 0x{rva:X} has no stub.");
        return unchecked((ulong)hook.StubAddress.ToInt64());
    }

    private static readonly byte[] ReturnFallthrough = { 0xB8, ResultFallthrough, 0, 0, 0, 0xC3 };
    // setne al; movzx eax,al; add eax,0x10; ret: exposes the flags a following JNE would read.
    private static readonly byte[] ReturnNotEqualAs0x10 = { 0x0F, 0x95, 0xC0, 0x0F, 0xB6, 0xC0, 0x83, 0xC0, 0x10, 0xC3 };
    private static readonly byte[] ReturnPlain = { 0xC3 };

    private sealed class Registers
    {
        public ulong Rax, Rbx, Rcx, Rdx, Rbp, Rsi, Rdi, R8, R9, R10, R11, R12, R13, R15;
        public ushort StackWord78;
    }

    private sealed class Outcome
    {
        public int Result;
        public ulong Rax, R11;
    }

    private static Outcome Run(ulong stub, Registers r)
    {
        byte* output = Alloc(64);
        var a = new Assembler(64);
        foreach (AssemblerRegister64 pushed in new[] { rbx, rbp, rsi, rdi, r12, r13, r14, r15 })
            a.push(pushed);
        // Eight pushes plus the return address leave RSP misaligned by 8; 0xB8 realigns before the call.
        a.sub(rsp, 0xB8);
        // The stub sees [rsp+0x78] as this slot plus the call's return address.
        a.mov(rax, (ulong)r.StackWord78);
        a.mov(__qword_ptr[rsp + 0x70], rax);
        a.mov(rax, stub);
        a.mov(__qword_ptr[rsp + 0xA0], rax);
        a.mov(rbx, r.Rbx); a.mov(rcx, r.Rcx); a.mov(rdx, r.Rdx); a.mov(rbp, r.Rbp); a.mov(rsi, r.Rsi); a.mov(rdi, r.Rdi);
        a.mov(r8, r.R8); a.mov(r9, r.R9); a.mov(r10, r.R10); a.mov(r11, r.R11);
        a.mov(r12, r.R12); a.mov(r13, r.R13); a.mov(r15, r.R15);
        a.mov(rax, r.Rax);
        a.call(__qword_ptr[rsp + 0xA0]);
        a.mov(rcx, Address(output));
        a.mov(__qword_ptr[rcx], rax);
        a.mov(__qword_ptr[rcx + 8], r11);
        a.add(rsp, 0xB8);
        foreach (AssemblerRegister64 popped in new[] { r15, r14, r13, r12, rdi, rsi, rbp, rbx })
            a.pop(popped);
        a.ret();

        byte* code = Alloc(1024);
        var writer = new PointerWriter(code);
        if (!a.TryAssemble(writer, Address(code), out string? error, out _))
            throw new InvalidOperationException(error);
        Marshal.GetDelegateForFunctionPointer<Action>((IntPtr)code)();
        return new Outcome { Result = *(int*)output, Rax = *(ulong*)output, R11 = *(ulong*)(output + 8) };
    }

    private sealed class PointerWriter : CodeWriter
    {
        private byte* next;
        public PointerWriter(byte* start) => next = start;
        public override void WriteByte(byte value) => *next++ = value;
    }

    private static byte* Unit(int id) => units + id * UnitStride;

    private static void SetUnit(int id, int type, int owner)
    {
        *(short*)(Unit(id) + 0x6E6) = (short)type;
        *(short*)(Unit(id) + 0x6EE) = (short)owner;
    }

    public static void Run(byte[] image)
    {
        flag = Alloc(16);
        units = Alloc(UnitStride * 16);
        TestQuery(image);
        TestExplicitAttack(image);
        TestArrow(image);
        TestHeartbeat(image);
        TestHostility(image);
        TestMeleeOrder(image);
        TestStance(image);
        TestTanner(image);
        Console.WriteLine($"ImprovedHunters native stubs executed: assertions={assertions}.");
    }

    private static void TestQuery(byte[] image)
    {
        ulong reject = ExitBlock(ResultReject);
        ulong stub = BuildStub(image, 0x18AFD8, 14, ReturnFallthrough,
            (a, i) => HunterNativeStubs.EmitOwnedChickenQuery(a, i, Address(flag), reject, Chicken));
        const int hunterOwner = 3;
        SetUnit(1, Hunter, hunterOwner);

        int Query(int type, int owner, bool enabled)
        {
            SetFlag(enabled);
            SetUnit(2, type, owner);
            return Run(stub, new Registers
            {
                // The query walks RBX over unit field +0x29C, the manager form of which is +0x8F8.
                Rbx = Address(Unit(2) + 0x8F8),
                R13 = Address(Unit(1))
            }).Result;
        }

        Check(Query(Deer, 0, true) == ResultFallthrough, "neutral deer must stay a candidate.");
        Check(Query(Deer, 0, false) == ResultFallthrough, "neutral deer must stay a candidate when disabled.");
        Check(Query(Chicken, 0, true) == ResultFallthrough, "a neutral chicken passes like other neutral prey.");
        Check(Query(Chicken, hunterOwner, true) == ResultFallthrough, "the Hunter's own chicken must pass.");
        Check(Query(Chicken, hunterOwner, false) == ResultReject, "disabled feature must keep Vanilla's owner rule.");
        Check(Query(Chicken, hunterOwner + 1, true) == ResultReject, "another player's chicken must be rejected.");
        Check(Query(Deer, hunterOwner, true) == ResultReject, "owned non-chicken animals must stay rejected.");
    }

    private static void TestExplicitAttack(byte[] image)
    {
        ulong reject = ExitBlock(ResultReject);
        ulong stub = BuildStub(image, 0x18EBDA, 14, ReturnFallthrough,
            (a, i) => HunterNativeStubs.EmitOwnedChickenExplicitAttack(a, i, Address(flag), reject, Chicken, Hunter));
        byte* teams = Alloc(64);
        for (int owner = 0; owner < 9; owner++)
            ((int*)teams)[owner] = owner;
        ((int*)teams)[4] = 3; // Owner 4 is allied with owner 3.
        const int attackerOwner = 3;
        const int attackerTeam = 3;

        int Attack(int attackerType, int targetType, int targetOwner, bool enabled)
        {
            SetFlag(enabled);
            SetUnit(2, targetType, targetOwner);
            return Run(stub, new Registers
            {
                Rax = attackerTeam,
                Rcx = (ulong)targetOwner,
                R8 = Address(units),
                R9 = unchecked(Address(teams) - TeamDisplacement),
                R12 = (ulong)attackerType,
                R13 = attackerOwner,
                Rbp = 2 * UnitStride
            }).Result;
        }

        Check(Attack(Hunter, Chicken, 5, true) == ResultFallthrough, "different teams must continue.");
        Check(Attack(Archer, Deer, 5, false) == ResultFallthrough, "different teams must continue when disabled.");
        Check(Attack(Hunter, Chicken, attackerOwner, true) == ResultFallthrough, "own chicken must be attackable.");
        Check(Attack(Hunter, Chicken, attackerOwner, false) == ResultReject, "disabled feature must reject.");
        Check(Attack(Archer, Chicken, attackerOwner, true) == ResultReject, "non-Hunters must not shoot own chickens.");
        Check(Attack(Hunter, Deer, attackerOwner, true) == ResultReject, "Hunters must not attack other own units.");
        Check(Attack(Hunter, Chicken, 4, true) == ResultReject, "allied chickens must stay rejected.");
    }

    private static void TestArrow(byte[] image)
    {
        ulong next = ExitBlock(ResultContinue);
        ulong miss = ExitBlock(ResultMiss);
        ulong stub = BuildStub(image, 0x192AA7, 14, ReturnNotEqualAs0x10,
            (a, i) => HunterNativeStubs.EmitOwnedChickenArrowHit(
                a, i, Address(flag), next, miss, Chicken, Hunter, 1,
                ProjectileSourceDisplacement, ProjectileHeightDisplacement));
        byte* fakeImage = Alloc(TeamDisplacement + 0x1000);
        int* teams = (int*)(fakeImage + TeamDisplacement);
        for (int owner = 0; owner < 9; owner++)
            teams[owner] = owner;
        teams[4] = 3;
        const int projectileRecord = 0xE8 * 2;
        const ushort marker = 0x1234;

        Outcome Hit(int victimType, int shooterType, int projectileOwner, int victimOwner, bool enabled,
            int projectileType = 1, short dead = 0, short projectileHeight = 100)
        {
            SetFlag(enabled);
            SetUnit(1, shooterType, projectileOwner);
            SetUnit(2, victimType, victimOwner);
            *(short*)(Unit(2) + 0x8F8) = dead;
            *(short*)(Unit(2) + 0x712) = 80;
            *(short*)(Unit(2) + 0x714) = 0;
            *(short*)(fakeImage + projectileRecord + ProjectileSourceDisplacement) = 1;
            *(short*)(fakeImage + projectileRecord + ProjectileHeightDisplacement) = projectileHeight;
            return Run(stub, new Registers
            {
                Rax = (ulong)teams[victimOwner],
                Rcx = (ulong)projectileType,
                Rdx = 42,
                // In this function R15 is the manager and RSI the victim's record offset.
                Rsi = 2 * UnitStride,
                R8 = (ulong)projectileOwner,
                R9 = (ulong)(projectileOwner * 4),
                R10 = (ulong)victimOwner,
                R11 = Address(fakeImage),
                R12 = (ulong)victimType,
                R13 = projectileRecord,
                R15 = Address(units),
                StackWord78 = marker
            });
        }

        const int sameTeamVanilla = 0x10;
        const int differentTeamVanilla = 0x11;
        Outcome outcome = Hit(Deer, Hunter, 3, 5, true);
        Check(outcome.Result == differentTeamVanilla && (ushort)outcome.R11 == marker, "different teams must keep ZF=0 and R11D.");
        outcome = Hit(Deer, Hunter, 3, 3, true);
        Check(outcome.Result == sameTeamVanilla && (ushort)outcome.R11 == marker, "friendly non-chicken must keep ZF=1 and R11D.");
        outcome = Hit(Chicken, Hunter, 3, 3, false);
        Check(outcome.Result == sameTeamVanilla, "disabled feature must leave the friendly-fire rule.");
        outcome = Hit(Chicken, Archer, 3, 3, true);
        Check(outcome.Result == sameTeamVanilla, "only Hunter arrows may hit own chickens.");
        outcome = Hit(Chicken, Hunter, 3, 4, true);
        Check(outcome.Result == sameTeamVanilla, "allied chickens must stay protected.");
        outcome = Hit(Chicken, Hunter, 3, 3, true, projectileType: 2);
        Check(outcome.Result == sameTeamVanilla, "other projectile types keep Vanilla's rule.");
        outcome = Hit(Chicken, Hunter, 3, 3, true);
        Check(outcome.Result == ResultContinue && (ushort)outcome.R11 == marker, "own chicken must be hit and restore R11D.");
        Check(Hit(Chicken, Hunter, 3, 3, true, dead: 1).Result == ResultMiss, "a dead chicken must not be hit.");
        Check(Hit(Chicken, Hunter, 3, 3, true, projectileHeight: 79).Result == ResultMiss, "an arrow below the chicken must miss.");
        // Vanilla's window: unit height <= arrow height <= unit height + range (RDX).
        Check(Hit(Chicken, Hunter, 3, 3, true, projectileHeight: 80).Result == ResultContinue, "the window's lower end must hit.");
        Check(Hit(Chicken, Hunter, 3, 3, true, projectileHeight: 122).Result == ResultContinue, "the window's upper end must hit.");
        Check(Hit(Chicken, Hunter, 3, 3, true, projectileHeight: 123).Result == ResultMiss, "an arrow above the window must miss.");
    }

    private static void TestHeartbeat(byte[] image)
    {
        int* currentUnit = (int*)Alloc(16);
        foreach ((int site, AssemblerRegister64 unitRegister, AssemblerRegister64 managerRegister) in new[]
        {
            (0x157828, r10, rsi),
            (0x15803A, rdi, rbp),
            (0x162CC8, rsi, rbp)
        })
        {
            ulong stub = BuildStub(image, site, 14, ReturnPlain,
                (a, i) => HunterNativeStubs.EmitReservationHeartbeat(
                    a, i, Address(flag), Address((byte*)currentUnit), Address(units), unitRegister, managerRegister));
            *currentUnit = 2;
            short* field = (short*)(units + 2 * UnitStride + 0xAA4);
            SetFlag(true);
            foreach ((short before, short after) in new (short, short)[] { (2, 1), (1, 0), (0, 0), (-1, -1) })
            {
                *field = before;
                Outcome outcome = RunCapture(stub, unitRegister, managerRegister);
                Check(*field == after, $"reservation {before} became {*field}, expected {after}.");
                Check(outcome.Rax == 2 && outcome.R11 == Address(units), "the update's two loads must still happen.");
            }

            SetFlag(false);
            *field = 2;
            Outcome disabled = RunCapture(stub, unitRegister, managerRegister);
            Check(*field == 2, "a disabled countdown must not write.");
            Check(disabled.Rax == 2 && disabled.R11 == Address(units), "the loads must also happen when disabled.");
        }
    }

    // Calls the stub and returns the unit index and manager the update expects in its registers.
    private static Outcome RunCapture(ulong stub, AssemblerRegister64 unitRegister, AssemblerRegister64 managerRegister)
    {
        byte* output = Alloc(64);
        var a = new Assembler(64);
        foreach (AssemblerRegister64 pushed in new[] { rbx, rbp, rsi, rdi, r12, r13, r14, r15 })
            a.push(pushed);
        a.sub(rsp, 0x28);
        a.mov(rax, stub);
        a.call(rax);
        a.mov(rcx, Address(output));
        a.mov(__qword_ptr[rcx], unitRegister);
        a.mov(__qword_ptr[rcx + 8], managerRegister);
        a.add(rsp, 0x28);
        foreach (AssemblerRegister64 popped in new[] { r15, r14, r13, r12, rdi, rsi, rbp, rbx })
            a.pop(popped);
        a.ret();
        byte* code = Alloc(512);
        if (!a.TryAssemble(new PointerWriter(code), Address(code), out string? error, out _))
            throw new InvalidOperationException(error);
        Marshal.GetDelegateForFunctionPointer<Action>((IntPtr)code)();
        return new Outcome { Rax = *(ulong*)output, R11 = *(ulong*)(output + 8) };
    }

    private static void TestHostility(byte[] image)
    {
        ulong notAttackable = ExitBlock(ResultNotAttackable);
        ulong attackable = ExitBlock(ResultAttackable);
        ulong nextChain = ExitBlock(ResultNextChain);
        byte* tribes = Alloc(TribeStride * 8);
        ulong stub = BuildStub(image, 0x186952, 16, ReturnNotEqualAs0x10,
            (a, i) => HunterNativeStubs.EmitRabbitMeleeHostility(
                a, i, Address(flag), Address(tribes), notAttackable, attackable, nextChain, Hunter));

        int Check_(int targetType, bool enabled, int param4, int attacker, int tribe, short tribeFlag)
        {
            SetFlag(enabled);
            *(short*)(Unit(1) + 0x930) = (short)tribe;
            *(short*)(tribes + tribe * TribeStride) = tribeFlag;
            return Run(stub, new Registers
            {
                Rcx = (ulong)targetType,
                R9 = (ulong)param4,
                R10 = (ulong)attacker,
                R11 = Address(Unit(1))
            }).Result;
        }

        const int soldier = 24;
        Check(Check_(Rabbit, true, 0, soldier, 2, 0) == ResultNotAttackable, "a soldier without the tribe flag must ignore rabbits.");
        Check(Check_(Rabbit, true, 0, soldier, 0, 0) == ResultNotAttackable, "units without a tribe must ignore rabbits.");
        Check(Check_(Rabbit, true, 0, soldier, 2, 1) == ResultAttackable, "the ordered tribe must attack rabbits.");
        Check(Check_(Rabbit, true, 1, soldier, 2, 0) == ResultAttackable, "the ranged caller keeps Vanilla's result.");
        Check(Check_(Rabbit, true, 0, Hunter, 0, 0) == ResultAttackable, "Hunters keep Vanilla's result.");
        Check(Check_(Rabbit, false, 0, soldier, 2, 0) == ResultAttackable, "disabled feature keeps Vanilla's result.");
        Check(Check_(9, true, 0, soldier, 2, 0) == ResultNextChain, "type 9 continues Vanilla's chain.");
        Check(Check_(0x33, true, 0, soldier, 2, 0) == 0x10, "type 0x33 sets Vanilla's flags for the JNE.");
        Check(Check_(0x12, true, 0, soldier, 2, 0) == 0x11, "other types fall to Vanilla's JNE.");
    }

    private static void TestMeleeOrder(byte[] image)
    {
        byte* tribes = Alloc(TribeStride * 8);
        const int order = 0x58E;
        ulong stub = BuildStub(image, 0x11EE64, 19, ReturnPlain,
            (a, i) => HunterNativeStubs.EmitRabbitMeleeOrder(a, i, Address(flag), Rabbit, order));

        short Order(int targetType, bool enabled)
        {
            SetFlag(enabled);
            SetUnit(2, targetType, 0);
            *(int*)(Unit(2) + 0x72C) = 0x7777;
            *(short*)(tribes + 2 * TribeStride + order) = 0;
            Outcome outcome = Run(stub, new Registers
            {
                Rbx = Address(units),
                Rbp = 2 * UnitStride,
                R12 = 2 * TribeStride,
                R15 = Address(tribes)
            });
            Check((uint)outcome.Rax == 0x7777, "the value loaded for the next instruction must survive.");
            return *(short*)(tribes + 2 * TribeStride + order);
        }

        Check(Order(Rabbit, true) == 1, "a melee order on a rabbit must set the tribe flag.");
        Check(Order(Rabbit, false) == 0, "a disabled feature must not set the flag.");
        Check(Order(Deer, true) == 0, "other targets must not set the flag.");
    }

    private static void TestStance(byte[] image)
    {
        ulong accept = ExitBlock(ResultAttackable);
        byte* tribes = Alloc(TribeStride * 8);
        ulong stub = BuildStub(image, 0x18C731, 14, ReturnPlain,
            (a, i) => HunterNativeStubs.EmitRabbitStanceScan(a, i, Address(flag), Address(tribes), accept, Rabbit));

        Outcome Scan(int type, byte animalFlag, bool enabled, short tribeFlag)
        {
            SetFlag(enabled);
            *(Unit(2) + 0x984) = animalFlag;
            *(short*)(Unit(1) + 0x930) = 2;
            *(short*)(tribes + 2 * TribeStride) = tribeFlag;
            return Run(stub, new Registers
            {
                Rsi = Address(units),
                R13 = UnitStride,
                R9 = (ulong)type,
                R10 = Address(Unit(2))
            });
        }

        Check(Scan(24, 0, true, 0).Result == ResultAttackable, "non-animals are accepted like in Vanilla.");
        Check(Scan(Rabbit, 1, true, 1).Result == ResultAttackable, "a flagged tribe accepts rabbits.");
        Check((uint)Scan(Rabbit, 1, true, 0).Rax == Rabbit - 0x2D, "without the flag Vanilla's filter continues with its EAX.");
        Check((uint)Scan(Rabbit, 1, false, 1).Rax == Rabbit - 0x2D, "a disabled feature keeps Vanilla's filter.");
        Check((uint)Scan(Deer, 1, true, 1).Rax == unchecked((uint)(Deer - 0x2D)), "other animals keep Vanilla's filter.");
    }

    // --- Tanner meat path ---
    private const int Meat = 12;
    private const int LeatherArmour = 23;
    private const int BuildingStride = 0x32C;
    private const int TannerUnit = 2;
    private const int TannerOwner = 3;
    private const int Tannery = 3;
    private const uint TanneryGlobalId = 0x7777;
    private const int ManagerWorkplace = 0x990;
    private const int ManagerCarried = 0x9E0;
    private const int ManagerSoundPlayed = 0xAA6;
    private const int ManagerDestinationGlobal = 0x6F8;

    private sealed class Recorder
    {
        public ulong Address;
        public ulong* Block;
        public ulong Rcx => Block[0];
        public ulong Rdx => Block[1];
        public ulong R8 => Block[2];
        public ulong R9 => Block[3];
        public ulong Stack28 => Block[4];
        public ulong Calls => Block[5];

        public int Result
        {
            set => *(int*)(Address + 2) = value;
        }

        public void Reset()
        {
            for (int index = 0; index < 6; index++)
                Block[index] = 0;
        }
    }

    /// <summary>A native helper stand-in that stores its arguments and returns a chosen value.</summary>
    private static Recorder NewRecorder(int result)
    {
        byte* block = Alloc(64);
        byte* code = Alloc(256);
        var a = new Assembler(64);
        // One NOP puts the immediate of mov eax at +2, where Result rewrites it.
        a.nop();
        a.mov(eax, result);
        a.mov(r10, Address(block));
        a.mov(__qword_ptr[r10], rcx);
        a.mov(__qword_ptr[r10 + 8], rdx);
        a.mov(__qword_ptr[r10 + 16], r8);
        a.mov(__qword_ptr[r10 + 24], r9);
        a.mov(r11, __qword_ptr[rsp + 0x30]);
        a.mov(__qword_ptr[r10 + 32], r11);
        a.inc(__qword_ptr[r10 + 40]);
        a.ret();
        if (!a.TryAssemble(new PointerWriter(code), Address(code), out string? error, out _))
            throw new InvalidOperationException(error);
        return new Recorder { Address = Address(code), Block = (ulong*)block };
    }

    private sealed class TannerFixture
    {
        public byte* Buildings;
        public int* CurrentUnit;
        public int* LocalPlayer;
        public int* MeatStat;
        public Recorder Armoury = null!, Granary = null!, Warning = null!, Accept = null!, Deposit = null!, Stats = null!, Sound = null!;
        public HunterNativeStubs.TannerMeatAddresses Addresses = null!;
        public TannerRunTable Table = null!;

        public byte* Entry(int id) => (byte*)Table.Address + id * TannerRunTable.EntrySize;

        public byte* Building(int id) => Buildings + id * BuildingStride;

        public Registers Registers(ulong rax, ulong rcx, ulong rdx, ulong r8, ulong r9) => new Registers
        {
            Rax = rax,
            Rbx = TannerUnit * UnitStride,
            Rcx = rcx,
            Rdx = rdx,
            Rsi = Address(Buildings),
            Rdi = 0,
            R8 = r8,
            R9 = r9,
            R12 = Address(units),
            R13 = 1
        };

        public void Reset()
        {
            foreach (Recorder recorder in new[] { Armoury, Granary, Warning, Accept, Deposit, Stats, Sound })
                recorder.Reset();
        }
    }

    private static TannerFixture NewTannerFixture()
    {
        var f = new TannerFixture
        {
            Buildings = Alloc(BuildingStride * 8),
            CurrentUnit = (int*)Alloc(16),
            LocalPlayer = (int*)Alloc(16),
            MeatStat = (int*)Alloc(64),
            Armoury = NewRecorder(7),
            Granary = NewRecorder(8),
            Warning = NewRecorder(0),
            Accept = NewRecorder(1),
            Deposit = NewRecorder(1),
            Stats = NewRecorder(0),
            Sound = NewRecorder(0),
            Table = new TannerRunTable()
        };
        *(uint*)(f.Building(Tannery) + 0x134) = TanneryGlobalId;
        *f.CurrentUnit = TannerUnit;
        *f.LocalPlayer = TannerOwner;
        SetUnit(TannerUnit, 21, TannerOwner);
        *(short*)(units + TannerUnit * UnitStride + ManagerWorkplace) = Tannery;
        f.Addresses = new HunterNativeStubs.TannerMeatAddresses
        {
            CurrentUnit = Address((byte*)f.CurrentUnit),
            LocalPlayer = Address((byte*)f.LocalPlayer),
            ArmouryFind = f.Armoury.Address,
            GranaryFind = f.Granary.Address,
            GranaryMissingWarning = f.Warning.Address,
            GranaryAccept = f.Accept.Address,
            Deposit = f.Deposit.Address,
            StatsManager = 0x1234,
            StatsAdd = f.Stats.Address,
            SoundManager = 0x5678,
            SoundPlay = f.Sound.Address,
            FoodDeliveredStat = Address((byte*)f.MeatStat),
            AfterFindWarning = ExitBlock(ResultContinue),
            AcceptFail = ExitBlock(ResultReject),
            DepositDone = ExitBlock(ResultContinue),
            RunTable = f.Table.Address
        };
        return f;
    }

    private static void SetRun(TannerFixture f, bool enabled, int latched, uint entryGlobalId = TanneryGlobalId)
    {
        SetFlag(enabled);
        f.Table.Clear();
        *(uint*)(f.Entry(Tannery) + TannerRunTable.GlobalIdOffset) = entryGlobalId;
        f.Entry(Tannery)[TannerRunTable.MeatTripOffset] = (byte)(latched == Meat ? 1 : 0);
        f.Reset();
    }

    private static void TestTannerYield(byte[] image, TannerFixture f)
    {
        int* amount = (int*)Alloc(16);
        *amount = 5;
        Recorder helper = NewRecorder(9);
        ulong stub = BuildStub(image, 0x13EBF7, 14, ReturnPlain,
            (a, i) => HunterNativeStubs.EmitTannerYield(a, i, Address(flag), f.Table.Address, Address((byte*)amount), helper.Address));
        byte* unit = units + TannerUnit * UnitStride;
        byte* entry = f.Entry(Tannery);

        (int Base, int Trip) Yield(bool enabled, int meatRuns, int hides, uint entryGlobalId = TanneryGlobalId)
        {
            SetFlag(enabled);
            f.Table.Clear();
            *(uint*)(entry + TannerRunTable.GlobalIdOffset) = entryGlobalId;
            entry[TannerRunTable.MeatRunsOffset] = (byte)meatRuns;
            entry[TannerRunTable.MeatTripOffset] = 0x7F;
            *(short*)(f.Building(Tannery) + 0x2EC) = 0x51;
            *(short*)(f.Building(Tannery) + 0x2EA) = 0x52;
            *(int*)(f.Building(Tannery) + 0x190) = hides;
            helper.Reset();
            Run(stub, f.Registers(0, 21, 1, 1, 0));
            Check(helper.Calls == 1 && helper.Rcx == 21 && helper.R8 == 1, "the yield helper runs once, with Vanilla's unit and bonus flag.");
            Check(*(short*)(unit + ManagerCarried) == 9, "the helper's result is carried, so its bonus stays.");
            Check(*(short*)(f.Building(Tannery) + 0x2EC) == 0x51 && *(short*)(f.Building(Tannery) + 0x2EA) == 0x52,
                "the workshop's produced-good fields stay free for Vanilla and other mods.");
            return ((int)helper.Rdx, entry[TannerRunTable.MeatTripOffset]);
        }

        Check(Yield(false, 3, 0) == (1, 0x7F), "a disabled patch keeps Vanilla's yield and the table.");
        Check(Yield(true, 3, 0, 0x1234) == (1, 0x7F), "a stale entry of an earlier building on this slot does not count.");
        for (int meatRuns = 0; meatRuns <= TannerMeatPolicy.HidesPerCow; meatRuns++)
        {
            for (int hides = 0; hides < TannerMeatPolicy.HidesPerCow; hides++)
            {
                bool meat = TannerMeatPolicy.RunCarriesMeat(hides, meatRuns);
                Check(Yield(true, meatRuns, hides) == (meat ? 5 : 1, meat ? 1 : 0), "a run follows the slider: meat runs come last.");
            }
        }

        *(short*)(unit + ManagerWorkplace) = (short)TannerRunTable.Capacity;
        Check(Yield(true, 3, 0) == (1, 0x7F), "a building above the table keeps the workshop's own good.");
        *(short*)(unit + ManagerWorkplace) = Tannery;
    }

    private static void TestTannerFind(byte[] image, TannerFixture f)
    {
        ulong stub = BuildStub(image, 0x13ED8B, 16, ReturnFallthrough,
            (a, i) => HunterNativeStubs.EmitTannerFind(a, i, Address(flag), f.Addresses, Meat));
        ulong manager = Address(f.Buildings);

        int Find(bool enabled, int latched, int owner, int granary)
        {
            SetRun(f, enabled, latched);
            f.Granary.Result = granary;
            SetUnit(TannerUnit, 21, owner);
            return Run(stub, f.Registers(9, manager, 0, 0, (ulong)owner)).Result;
        }

        Check(Find(false, Meat, TannerOwner, 8) == ResultFallthrough && f.Armoury.Calls == 1 && f.Granary.Calls == 0,
            "a disabled patch must search the armoury.");
        Check(Find(true, 0, TannerOwner, 8) == ResultFallthrough && f.Armoury.Calls == 1 && f.Armoury.R8 == 0x17 && f.Armoury.Rcx == manager,
            "an armour run keeps Vanilla's search arguments.");
        Check(Find(true, Meat, TannerOwner, 8) == ResultContinue && f.Armoury.Calls == 0 && f.Granary.Calls == 1,
            "a meat run must search a granary and skip the armoury warning.");
        Check(f.Granary.Rcx == manager && f.Granary.Rdx == Meat && f.Granary.R8 == 1 && f.Granary.R9 == TannerOwner,
            "the granary search must use the Hunter's arguments.");
        Check(f.Warning.Calls == 0, "a found granary must not warn.");
        SetRun(f, true, Meat, 0x1234);
        Check(Run(stub, f.Registers(9, manager, 0, 0, TannerOwner)).Result == ResultFallthrough && f.Granary.Calls == 0,
            "a stale meat trip of an earlier building must not count.");
        Check(Find(true, Meat, TannerOwner, 0) == ResultContinue && f.Warning.Calls == 1, "the local player is warned about a missing granary.");
        Check(Find(true, Meat, TannerOwner + 1, 0) == ResultContinue && f.Warning.Calls == 0, "other players are not warned locally.");
        SetUnit(TannerUnit, 21, TannerOwner);
    }

    private static void TestTannerAccept(byte[] image, TannerFixture f)
    {
        ulong stub = BuildStub(image, 0x13EEFB, 16, ReturnFallthrough,
            (a, i) => HunterNativeStubs.EmitTannerAccept(a, i, Address(flag), f.Addresses, Meat));
        ulong manager = Address(f.Buildings);

        int Accept(bool enabled, int latched, int accepted, int armoury)
        {
            SetRun(f, enabled, latched);
            f.Accept.Result = accepted;
            f.Armoury.Result = armoury;
            return Run(stub, f.Registers(0, TannerUnit * UnitStride, 5, 0x17, TannerOwner)).Result;
        }

        Check(Accept(false, Meat, 1, 7) == ResultFallthrough && f.Armoury.Calls == 1 && f.Accept.Calls == 0, "disabled keeps the armoury check.");
        Check(Accept(true, 0, 1, 0) == ResultReject && f.Armoury.Calls == 1, "a full armoury goes to Vanilla's failure path.");
        Check(Accept(true, 0, 1, 7) == ResultFallthrough && f.Armoury.Rcx == manager && f.Armoury.Rdx == 5 && f.Armoury.R8 == 0x17, "armour keeps Vanilla's arguments.");
        Check(Accept(true, Meat, 1, 0) == ResultFallthrough && f.Accept.Calls == 1 && f.Armoury.Calls == 0, "meat asks the granary.");
        Check(f.Accept.Rcx == manager && f.Accept.Rdx == 5 && f.Accept.R8 == Meat && f.Accept.R9 == 0xFA, "the granary check uses the Hunter's arguments.");
        Check(Accept(true, Meat, 0, 7) == ResultReject, "a full granary goes to the failure path.");
    }

    private static void TestTannerDeposit(byte[] image, TannerFixture f)
    {
        ulong stub = BuildStub(image, 0x13EF38, 16, ReturnFallthrough,
            (a, i) => HunterNativeStubs.EmitTannerDeposit(a, i, Address(flag), f.Addresses, Meat));
        ulong manager = Address(f.Buildings);
        byte* unit = units + TannerUnit * UnitStride;
        *(int*)(unit + ManagerDestinationGlobal) = 0x4242;

        int Deposit(bool enabled, int latched, byte soundPlayed)
        {
            SetRun(f, enabled, latched);
            *(short*)(unit + ManagerCarried) = 5;
            *(unit + ManagerSoundPlayed) = soundPlayed;
            f.MeatStat[TannerOwner] = 0;
            return Run(stub, f.Registers(0, TannerUnit * UnitStride, 6, 0, 0x17)).Result;
        }

        Check(Deposit(false, Meat, 0) == ResultFallthrough && f.Deposit.Calls == 1 && f.Deposit.R9 == 0x17 && f.Stats.Calls == 0, "disabled keeps Vanilla's deposit.");
        Check(*(short*)(unit + ManagerCarried) == 5 && f.MeatStat[TannerOwner] == 0, "Vanilla's own code counts armour after the deposit.");
        Check(Deposit(true, 0, 0) == ResultFallthrough && f.Deposit.Rcx == manager && f.Deposit.R8 == 0x4242 && f.Deposit.R9 == 0x17, "armour keeps Vanilla's arguments.");
        Check(Deposit(true, Meat, 0) == ResultContinue, "meat must continue behind the armour bookkeeping.");
        Check(f.Deposit.Rcx == manager && f.Deposit.Rdx == 6 && f.Deposit.R8 == 0x4242 && f.Deposit.R9 == Meat && (int)f.Deposit.Stack28 == 0xFA,
            "meat must be stored with the Hunter's arguments.");
        Check(f.MeatStat[TannerOwner] == 1 && f.Stats.Calls == 1 && f.Stats.Rcx == 0x1234 && f.Stats.Rdx == 2 && f.Stats.R8 == 1 && f.Stats.R9 == TannerOwner,
            "meat counts as delivered food.");
        Check(*(short*)(unit + ManagerCarried) == 4, "each delivery lowers the carried amount by one.");
        Check(f.Sound.Calls == 1 && f.Sound.Rcx == 0x5678 && f.Sound.R9 == 0x21 && *(unit + ManagerSoundPlayed) == 1, "the first delivery plays the Hunter's sound once.");
        Check(Deposit(true, Meat, 1) == ResultContinue && f.Sound.Calls == 0 && *(short*)(unit + ManagerCarried) == 4, "later deliveries stay silent.");
    }

    private static void TestTannerRefind(byte[] image, TannerFixture f)
    {
        ulong stub = BuildStub(image, 0x13F009, 15, ReturnFallthrough,
            (a, i) => HunterNativeStubs.EmitTannerRefind(a, i, Address(flag), f.Addresses, Meat));
        ulong manager = Address(f.Buildings);

        int Refind(bool enabled, int latched)
        {
            SetRun(f, enabled, latched);
            return Run(stub, f.Registers(5, TannerUnit * UnitStride, 0, 0x17, TannerOwner)).Result;
        }

        Check(Refind(false, Meat) == ResultFallthrough && f.Armoury.Calls == 1 && f.Granary.Calls == 0, "disabled keeps the armoury search.");
        Check(Refind(true, 0) == ResultFallthrough && f.Armoury.Calls == 1 && f.Armoury.Rcx == manager && f.Armoury.R8 == 0x17, "armour keeps Vanilla's arguments.");
        Check(Refind(true, Meat) == ResultFallthrough && f.Granary.Calls == 1 && f.Armoury.Calls == 0, "meat looks for another granary.");
        Check(f.Granary.Rcx == manager && f.Granary.Rdx == Meat && f.Granary.R8 == 1 && f.Granary.R9 == TannerOwner, "the search uses the Hunter's arguments.");
    }

    private static void TestTanner(byte[] image)
    {
        TannerFixture fixture = NewTannerFixture();
        TestTannerYield(image, fixture);
        TestTannerFind(image, fixture);
        TestTannerAccept(image, fixture);
        TestTannerDeposit(image, fixture);
        TestTannerRefind(image, fixture);
    }
}
