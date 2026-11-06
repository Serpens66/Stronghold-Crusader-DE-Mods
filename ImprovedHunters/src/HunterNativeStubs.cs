using Iced.Intel;
using System;
using static Iced.Intel.AssemblerRegisters;

namespace ImprovedHunters
{
    /// <summary>
    /// Machine-code bodies of the permanent native gates. They run in the simulation's
    /// hottest unit loops, read only native simulation state and one logical flag, and
    /// never enter managed code, so they stay deterministic and cheap. Register and flag
    /// liveness of every site is documented in UpdateToNewDLL.md.
    /// </summary>
    internal static class HunterNativeStubs
    {
        private const int UnitStride = 0x490;
        private const int TribeStride = 0x688;
        // Unit record fields relative to the manager-based address form used at these sites.
        private const int UnitTypeOffset = 0x6E6;
        private const int UnitOwnerOffset = 0x6EE;
        private const int UnitDeadOffset = 0x8F8;
        private const int UnitTribeOffset = 0x930;
        private const int UnitHeightOffset = 0x712;
        private const int UnitStandingHeightOffset = 0x714;
        // Manager-based address of GameUnit +0x448 (the record starts at manager + 0x65C).
        private const int ManagerToReservation = 0xAA4;
        // The Hunter query walks RBX over unit field +0x29C, so these are field deltas.
        private const int QueryCandidateOwnerDelta = -0x20A;
        private const int QueryCandidateTypeDelta = -0x212;

        private static void JumpAbsolute(Assembler a, ulong target)
        {
            // RAX is dead at every absolute target used in this file.
            a.mov(rax, target);
            a.jmp(rax);
        }

        private static void Require(ReadOnlySpan<Instruction> instructions, int count, string site)
        {
            if (instructions.Length != count)
                throw new InvalidOperationException($"Unexpected displaced instructions at {site}: {instructions.Length}.");
        }

        /// <summary>
        /// Replaces the Hunter query's "candidate owner must be zero" branch. Neutral prey
        /// passes as before; a chicken additionally passes when it belongs to the Hunter.
        /// </summary>
        internal static void EmitOwnedChickenQuery(
            Assembler a,
            ReadOnlySpan<Instruction> instructions,
            ulong flagAddress,
            ulong rejectAddress,
            int chickenType)
        {
            Require(instructions, 2, "Hunter query owner check");
            Label pass = a.CreateLabel("ownerQueryPass");
            Label reject = a.CreateLabel("ownerQueryReject");
            a.AddInstruction(instructions[0]);
            a.je(pass);
            a.cmp(__word_ptr[rbx + QueryCandidateTypeDelta], chickenType);
            a.jne(reject);
            a.movzx(eax, __word_ptr[rbx + QueryCandidateOwnerDelta]);
            a.cmp(__word_ptr[r13 + UnitOwnerOffset], ax);
            a.jne(reject);
            a.mov(rax, flagAddress);
            a.cmp(__dword_ptr[rax], 0);
            a.jne(pass);
            a.Label(ref reject);
            JumpAbsolute(a, rejectAddress);
            a.Label(ref pass);
            a.nop();
        }

        /// <summary>
        /// Replaces the explicit attack's "same team" rejection for one case only: a Hunter
        /// (R12D) attacking a chicken of its own player (RCX target owner, R13 attacker owner).
        /// </summary>
        internal static void EmitOwnedChickenExplicitAttack(
            Assembler a,
            ReadOnlySpan<Instruction> instructions,
            ulong flagAddress,
            ulong rejectAddress,
            int chickenType,
            int hunterType)
        {
            Require(instructions, 2, "explicit attack team check");
            Label pass = a.CreateLabel("ownerAttackPass");
            Label reject = a.CreateLabel("ownerAttackReject");
            a.AddInstruction(instructions[0]);
            a.jne(pass);
            a.cmp(r12d, hunterType);
            a.jne(reject);
            a.cmp(__word_ptr[r8 + rbp + UnitTypeOffset], chickenType);
            a.jne(reject);
            a.cmp(rcx, r13);
            a.jne(reject);
            a.mov(rax, flagAddress);
            a.cmp(__dword_ptr[rax], 0);
            a.jne(pass);
            a.Label(ref reject);
            JumpAbsolute(a, rejectAddress);
            a.Label(ref pass);
            a.nop();
        }

        /// <summary>
        /// Lets a Hunter's arrow hit a chicken of the same player. Vanilla's dead and height
        /// gates are repeated exactly; the following attacker record is a no-op for equal
        /// owners and the "under attack" warning is deliberately skipped, because a player's
        /// own Hunter shooting their own chicken is no attack. Every other case keeps
        /// Vanilla's flags for the JNE directly behind the span.
        /// </summary>
        internal static void EmitOwnedChickenArrowHit(
            Assembler a,
            ReadOnlySpan<Instruction> instructions,
            ulong flagAddress,
            ulong continueAddress,
            ulong missAddress,
            int chickenType,
            int hunterType,
            int arrowType,
            int projectileSourceDisplacement,
            int projectileHeightDisplacement)
        {
            Require(instructions, 2, "arrow team check");
            Label sameTeam = a.CreateLabel("arrowSameTeam");
            Label resume = a.CreateLabel("arrowResume");
            Label miss = a.CreateLabel("arrowMiss");
            a.AddInstruction(instructions[0]);
            a.jne(resume);
            a.cmp(r12w, (short)chickenType);
            a.jne(sameTeam);
            a.cmp(cx, (short)arrowType);
            a.jne(sameTeam);
            a.cmp(r8w, r10w);
            a.jne(sameTeam);
            a.mov(rax, flagAddress);
            a.cmp(__dword_ptr[rax], 0);
            a.je(sameTeam);
            a.movsx(rax, __word_ptr[r11 + r13 + projectileSourceDisplacement]);
            a.imul(rax, rax, UnitStride);
            a.cmp(__word_ptr[r15 + rax + UnitTypeOffset], hunterType);
            a.jne(sameTeam);

            a.cmp(__word_ptr[rsi + r15 + UnitDeadOffset], 0);
            a.jne(miss);
            a.movsx(eax, __word_ptr[rsi + r15 + UnitHeightOffset]);
            a.movsx(r9d, __word_ptr[rsi + r15 + UnitStandingHeightOffset]);
            a.add(r9d, eax);
            a.movsx(ecx, __word_ptr[r11 + r13 + projectileHeightDisplacement]);
            a.cmp(r9d, ecx);
            a.jg(miss);
            a.lea(eax, __[r9 + rdx]);
            a.cmp(eax, ecx);
            a.jl(miss);
            a.AddInstruction(instructions[1]);
            JumpAbsolute(a, continueAddress);

            a.Label(ref miss);
            JumpAbsolute(a, missAddress);

            a.Label(ref sameTeam);
            // Restores Vanilla's ZF=1 result of the team comparison for the following JNE.
            a.cmp(rax, rax);
            a.Label(ref resume);
            a.AddInstruction(instructions[1]);
        }

        /// <summary>
        /// Gives rabbits, camels and chickens the reservation countdown deer and goats already
        /// have (0x156130). A Hunter rewrites the value in every update while it holds a target,
        /// so a released or orphaned reservation expires deterministically within two updates.
        /// The span is the update's own "load current unit, load manager" pair directly behind
        /// the prologue (the Fixes mod hooks the function entry). The stub performs both loads
        /// itself with absolute addresses instead of relocating their RIP-relative forms.
        /// </summary>
        internal static void EmitReservationHeartbeat(
            Assembler a,
            ReadOnlySpan<Instruction> instructions,
            ulong flagAddress,
            ulong currentUnitAddress,
            ulong managerAddress,
            AssemblerRegister64 unitIndex,
            AssemblerRegister64 manager)
        {
            Require(instructions, 2, "prey update globals");
            Label done = a.CreateLabel("reservationDone");
            a.mov(rax, currentUnitAddress);
            a.movsxd(unitIndex, __dword_ptr[rax]);
            a.mov(manager, managerAddress);
            a.mov(rax, flagAddress);
            a.cmp(__dword_ptr[rax], 0);
            a.je(done);
            a.imul(rax, unitIndex, UnitStride);
            a.cmp(__word_ptr[manager + rax + ManagerToReservation], 0);
            a.jle(done);
            a.dec(__word_ptr[manager + rax + ManagerToReservation]);
            a.Label(ref done);
            a.nop();
        }

        /// <summary>
        /// In the shared melee hostility check (R9B == 0, ranged callers pass 1) a rabbit stays
        /// attackable only while the attacker's tribe holds the rabbit-hunt flag ranged tribes
        /// already use. Other targets replay Vanilla's comparison chain.
        /// </summary>
        internal static void EmitRabbitMeleeHostility(
            Assembler a,
            ReadOnlySpan<Instruction> instructions,
            ulong flagAddress,
            ulong rabbitHuntFlagBaseAddress,
            ulong notAttackableAddress,
            ulong attackableAddress,
            ulong nextChainAddress,
            int hunterType)
        {
            Require(instructions, 5, "rabbit melee hostility");
            Label other = a.CreateLabel("rabbitOther");
            Label attackable = a.CreateLabel("rabbitAttackable");
            Label notAttackable = a.CreateLabel("rabbitNotAttackable");
            Label notType9 = a.CreateLabel("rabbitNotType9");
            a.AddInstruction(instructions[0]);
            a.jne(other);
            a.mov(rax, flagAddress);
            a.cmp(__dword_ptr[rax], 0);
            a.je(attackable);
            a.test(r9b, r9b);
            a.jne(attackable);
            a.cmp(r10w, (short)hunterType);
            a.je(attackable);
            a.movsx(rax, __word_ptr[r11 + UnitTribeOffset]);
            a.test(eax, eax);
            a.jle(notAttackable);
            a.imul(rax, rax, TribeStride);
            a.mov(r8, rabbitHuntFlagBaseAddress);
            a.cmp(__word_ptr[r8 + rax], 0);
            a.jne(attackable);
            a.Label(ref notAttackable);
            JumpAbsolute(a, notAttackableAddress);
            a.Label(ref attackable);
            JumpAbsolute(a, attackableAddress);
            a.Label(ref other);
            a.AddInstruction(instructions[2]);
            a.jne(notType9);
            JumpAbsolute(a, nextChainAddress);
            a.Label(ref notType9);
            // The final CMP's flags are consumed by the original JNE behind the span.
            a.AddInstruction(instructions[4]);
        }

        /// <summary>
        /// Vanilla sets the tribe's rabbit-hunt flag only for ranged units. The melee branch of
        /// the same attack order (RBX manager, RBP target, R15+R12 tribe) now sets it too.
        /// </summary>
        internal static void EmitRabbitMeleeOrder(
            Assembler a,
            ReadOnlySpan<Instruction> instructions,
            ulong flagAddress,
            int rabbitType,
            int rabbitHuntFlagOffset)
        {
            Label done = a.CreateLabel("rabbitOrderDone");
            for (int index = 0; index < instructions.Length; index++)
                a.AddInstruction(instructions[index]);
            a.cmp(__word_ptr[rbx + rbp + UnitTypeOffset], rabbitType);
            a.jne(done);
            // RAX already holds the value the instruction behind the span stores.
            a.push(rax);
            a.mov(rax, flagAddress);
            a.cmp(__dword_ptr[rax], 0);
            a.pop(rax);
            a.je(done);
            a.mov(__word_ptr[r15 + r12 + rabbitHuntFlagOffset], 1);
            a.Label(ref done);
            a.nop();
        }

        /// <summary>
        /// Lets the melee stance scan accept rabbits while the attacker's tribe holds the flag,
        /// which gives melee units the follow-up behaviour ranged units have after one order.
        /// </summary>
        internal static void EmitRabbitStanceScan(
            Assembler a,
            ReadOnlySpan<Instruction> instructions,
            ulong flagAddress,
            ulong rabbitHuntFlagBaseAddress,
            ulong acceptAddress,
            int rabbitType)
        {
            Require(instructions, 3, "rabbit stance scan");
            Label accept = a.CreateLabel("stanceAccept");
            Label vanilla = a.CreateLabel("stanceVanilla");
            a.AddInstruction(instructions[0]);
            a.je(accept);
            a.cmp(r9w, (short)rabbitType);
            a.jne(vanilla);
            a.mov(rax, flagAddress);
            a.cmp(__dword_ptr[rax], 0);
            a.je(vanilla);
            a.movsx(rax, __word_ptr[rsi + r13 + UnitTribeOffset]);
            a.test(eax, eax);
            a.jle(vanilla);
            a.imul(rax, rax, TribeStride);
            a.mov(rdx, rabbitHuntFlagBaseAddress);
            a.cmp(__word_ptr[rdx + rax], 0);
            a.je(vanilla);
            a.Label(ref accept);
            JumpAbsolute(a, acceptAddress);
            a.Label(ref vanilla);
            a.AddInstruction(instructions[2]);
        }

        /// <summary>
        /// Availability check of the Hunter's hut. Vanilla answers "not available" in skirmish and
        /// multiplayer (R10D not zero) on a map without deer and goats; with the flag set it goes on as usual.
        /// </summary>
        internal static void EmitHutAlwaysAvailable(Assembler a, ReadOnlySpan<Instruction> instructions, ulong flag, ulong hasPrey, ulong available)
        {
            Require(instructions, 4, "Hunter's hut availability");
            Label proceed = a.CreateLabel("hutAvailable");
            Label unavailable = a.CreateLabel("hutUnavailable");
            a.AddInstruction(instructions[0]);
            a.je(proceed);
            a.mov(rax, flag);
            a.cmp(__dword_ptr[rax], 0);
            a.jne(proceed);
            a.mov(rax, hasPrey);
            a.cmp(__dword_ptr[rax], 0);
            a.je(unavailable);
            a.Label(ref proceed);
            JumpAbsolute(a, available);
            a.Label(ref unavailable);
            a.nop();
        }

        /// <summary>
        /// Deer and goat herd update, reading the threats of its sector into EDI. RBP is the herd.
        /// A herd marked calm sees none. RAX and RCX are written again before they are read here.
        /// </summary>
        internal static void EmitHerdFleeGate(Assembler a, ReadOnlySpan<Instruction> instructions, ulong flag, ulong calmHerds)
        {
            Require(instructions, 4, "herd threat read");
            Label done = a.CreateLabel("herdFleeDone");
            foreach (Instruction instruction in instructions)
                a.AddInstruction(instruction);
            a.mov(rax, flag);
            a.cmp(__dword_ptr[rax], 0);
            a.je(done);
            a.mov(rax, calmHerds);
            a.cmp(__byte_ptr[rax + rbp], 0);
            a.je(done);
            a.xor(edi, edi);
            a.Label(ref done);
            a.nop();
        }

        internal const int CorpseEntrySize = 8;
        internal const int CorpseEntryGlobalId = 0;
        internal const int CorpseEntryUnitId = 4;
        private const int ManagerUnitGlobalId = 0x6F0;
        private const int ManagerUnitAliveState = 0x6E4;
        private const short UnitAliveValue = 2;

        /// <summary>
        /// Leaves the chosen corpse's ID in EAX and reserves it, or goes to the label when this Hunter
        /// (RBX) has no choice that is still valid. R13 is the unit manager; RAX, RCX, R10 and R11 are free.
        /// </summary>
        private static void TakeCorpseChoice(Assembler a, ulong flag, ulong table, Label none)
        {
            a.mov(rax, flag);
            a.cmp(__dword_ptr[rax], 0);
            a.je(none);
            a.mov(r10, table);
            a.lea(r10, __[r10 + rbx * CorpseEntrySize]);
            a.movzx(eax, __word_ptr[r10 + CorpseEntryUnitId]);
            a.test(eax, eax);
            a.je(none);
            a.imul(r11, rax, UnitStride);
            a.add(r11, r13);
            a.mov(ecx, __dword_ptr[r10 + CorpseEntryGlobalId]);
            a.cmp(ecx, __dword_ptr[r11 + ManagerUnitGlobalId]);
            a.jne(none);
            a.cmp(__word_ptr[r11 + ManagerUnitAliveState], UnitAliveValue);
            a.jne(none);
            a.cmp(__word_ptr[r11 + UnitDeadOffset], 0);
            a.je(none);
            a.cmp(__word_ptr[r11 + ManagerToReservation], 0);
            a.jne(none);
            a.mov(__word_ptr[r11 + ManagerToReservation], 2);
            a.mov(__word_ptr[r10 + CorpseEntryUnitId], 0);
        }

        /// <summary>
        /// State 0, before Vanilla looks for living prey: a chosen corpse goes straight to the code
        /// that points the Hunter at a corpse and walks there. EBP is zero there, as this function needs.
        /// </summary>
        internal static void EmitCorpseChoiceInSearch(Assembler a, ReadOnlySpan<Instruction> instructions, ulong flag, ulong table, ulong liveSearch, ulong walkToCorpse)
        {
            Require(instructions, 4, "Hunter target search");
            Label none = a.CreateLabel("corpseSearchNone");
            TakeCorpseChoice(a, flag, table, none);
            a.mov(r10, walkToCorpse);
            a.jmp(r10);
            a.Label(ref none);
            a.AddInstruction(instructions[0]);
            a.AddInstruction(instructions[1]);
            CallAbsolute(a, liveSearch);
            a.AddInstruction(instructions[3]);
        }

        /// <summary>After a shot that left the target alive: the chosen corpse comes before Vanilla's own search.</summary>
        internal static void EmitCorpseChoiceAfterShot(Assembler a, ReadOnlySpan<Instruction> instructions, ulong flag, ulong table, ulong corpseSearch, ulong noCorpse)
        {
            Require(instructions, 5, "Hunter corpse search");
            Label none = a.CreateLabel("corpseShotNone");
            Label check = a.CreateLabel("corpseShotCheck");
            Label found = a.CreateLabel("corpseShotFound");
            TakeCorpseChoice(a, flag, table, none);
            a.jmp(check);
            a.Label(ref none);
            a.AddInstruction(instructions[0]);
            a.AddInstruction(instructions[1]);
            CallAbsolute(a, corpseSearch);
            a.Label(ref check);
            a.test(eax, eax);
            a.jne(found);
            JumpAbsolute(a, noCorpse);
            a.Label(ref found);
            a.nop();
        }

        /// <summary>Addresses of the native helpers the Tanner meat stubs use.</summary>
        internal sealed class TannerMeatAddresses
        {
            public ulong CurrentUnit, LocalPlayer, ArmouryFind, GranaryFind, GranaryMissingWarning,
                GranaryAccept, Deposit, StatsManager, StatsAdd, SoundManager, SoundPlay,
                FoodDeliveredStat, AfterFindWarning, AcceptFail, DepositDone, RunTable;
        }

        private const int BuildingStride = 0x32C;
        // Manager-relative fields of the building record; the Extender's struct starts 0x5C later.
        private const int BuildingGlobalId = 0x134;
        private const int BuildingHides = 0x190;
        private const int TannerWorkplace = 0x990;
        private const int TannerCarried = 0x9E0;
        private const int TannerSoundPlayed = 0xAA6;
        private const int TannerTileX = 0x71C;
        private const int TannerTileY = 0x71E;
        // Vanilla's constants on the Hunter's meat path.
        private const int GranaryFoodCapacity = 0xFA;
        private const int DeliverySoundMeat = 0x21;
        private const int StatsFoodCategory = 2;

        /// <summary>Jumps to the label unless this Tanner's current run carries meat. Only R10 and R11 are touched.</summary>
        private static void BranchUnlessMeatRun(Assembler a, AssemblerRegister64 unitOffset, ulong flag, ulong table, Label vanilla)
        {
            a.mov(r10, flag);
            a.cmp(__dword_ptr[r10], 0);
            a.je(vanilla);
            a.movsx(r10, __word_ptr[unitOffset + r12 + TannerWorkplace]);
            a.cmp(r10, TannerRunTable.Capacity);
            a.jae(vanilla);
            a.mov(r11, table);
            a.lea(r11, __[r11 + r10 * TannerRunTable.EntrySize]);
            a.cmp(__byte_ptr[r11 + TannerRunTable.MeatTripOffset], 0);
            a.je(vanilla);
            a.imul(r10, r10, BuildingStride);
            a.mov(r10d, __dword_ptr[r10 + rsi + BuildingGlobalId]);
            a.cmp(r10d, __dword_ptr[r11 + TannerRunTable.GlobalIdOffset]);
            a.jne(vanilla);
        }

        private static void CallAbsolute(Assembler a, ulong target)
        {
            a.mov(rax, target);
            a.call(rax);
        }

        /// <summary>
        /// At the end of a run: decides for this trip whether it carries meat. RSI is the building
        /// manager, RBX the unit offset. The yield helper runs either way, with the configured base
        /// amount on a meat trip, so meat gets the same Vanilla bonus as every other product.
        /// Vanilla's arguments are still in RCX, RDX and R8; RAX, R10 and R11 are free here.
        /// </summary>
        internal static void EmitTannerYield(Assembler a, ReadOnlySpan<Instruction> instructions, ulong flag, ulong table, ulong meatAmount, ulong yieldHelper)
        {
            Require(instructions, 2, "Tanner run yield");
            Label call = a.CreateLabel("tannerYieldCall");
            a.mov(r10, flag);
            a.cmp(__dword_ptr[r10], 0);
            a.je(call);
            a.movsx(r11, __word_ptr[rbx + r12 + TannerWorkplace]);
            a.cmp(r11, TannerRunTable.Capacity);
            a.jae(call);
            a.mov(r10, table);
            a.lea(r10, __[r10 + r11 * TannerRunTable.EntrySize]);
            a.imul(r11, r11, BuildingStride);
            a.add(r11, rsi);
            a.mov(eax, __dword_ptr[r11 + BuildingGlobalId]);
            a.cmp(eax, __dword_ptr[r10 + TannerRunTable.GlobalIdOffset]);
            a.jne(call);
            a.mov(__byte_ptr[r10 + TannerRunTable.MeatTripOffset], 0);
            a.movzx(eax, __byte_ptr[r10 + TannerRunTable.MeatRunsOffset]);
            a.cmp(__dword_ptr[r11 + BuildingHides], eax);
            a.jge(call);
            a.mov(__byte_ptr[r10 + TannerRunTable.MeatTripOffset], 1);
            a.mov(r10, meatAmount);
            a.mov(edx, __dword_ptr[r10]);
            a.Label(ref call);
            CallAbsolute(a, yieldHelper);
            a.AddInstruction(instructions[1]);
        }

        /// <summary>State 7: a meat run searches a granary and warns like the Hunter when there is none.</summary>
        internal static void EmitTannerFind(Assembler a, ReadOnlySpan<Instruction> instructions, ulong flag, TannerMeatAddresses addresses, int meat)
        {
            Require(instructions, 4, "Tanner storage search");
            Label vanilla = a.CreateLabel("tannerFindVanilla");
            Label found = a.CreateLabel("tannerFindFound");
            a.AddInstruction(instructions[0]);
            a.AddInstruction(instructions[1]);
            BranchUnlessMeatRun(a, rbx, flag, addresses.RunTable, vanilla);
            a.mov(edx, meat);
            a.mov(r8d, 1);
            CallAbsolute(a, addresses.GranaryFind);
            a.movsxd(rbx, eax);
            a.test(eax, eax);
            a.jne(found);
            a.mov(rax, addresses.CurrentUnit);
            a.movsxd(rcx, __dword_ptr[rax]);
            a.imul(rdx, rcx, UnitStride);
            a.movsx(ecx, __word_ptr[rdx + r12 + UnitOwnerOffset]);
            a.mov(rax, addresses.LocalPlayer);
            a.cmp(ecx, __dword_ptr[rax]);
            a.jne(found);
            CallAbsolute(a, addresses.GranaryMissingWarning);
            a.Label(ref found);
            JumpAbsolute(a, addresses.AfterFindWarning);
            a.Label(ref vanilla);
            CallAbsolute(a, addresses.ArmouryFind);
            a.AddInstruction(instructions[3]);
        }

        /// <summary>State 8: on arrival a meat run asks the granary instead of the armoury. RCX is the unit offset.</summary>
        internal static void EmitTannerAccept(Assembler a, ReadOnlySpan<Instruction> instructions, ulong flag, TannerMeatAddresses addresses, int meat)
        {
            Require(instructions, 4, "Tanner storage acceptance");
            Label vanilla = a.CreateLabel("tannerAcceptVanilla");
            Label check = a.CreateLabel("tannerAcceptCheck");
            Label pass = a.CreateLabel("tannerAcceptPass");
            BranchUnlessMeatRun(a, rcx, flag, addresses.RunTable, vanilla);
            a.mov(rcx, rsi);
            a.mov(r8d, meat);
            a.mov(r9d, GranaryFoodCapacity);
            CallAbsolute(a, addresses.GranaryAccept);
            a.jmp(check);
            a.Label(ref vanilla);
            a.mov(rcx, rsi);
            CallAbsolute(a, addresses.ArmouryFind);
            a.Label(ref check);
            a.test(eax, eax);
            a.jne(pass);
            JumpAbsolute(a, addresses.AcceptFail);
            a.Label(ref pass);
            a.nop();
        }

        /// <summary>
        /// State 8: a meat run stores meat and counts it as delivered food, exactly like the
        /// Hunter's delivery, instead of counting armour. RDI is zero and R13D one in this path.
        /// </summary>
        internal static void EmitTannerDeposit(Assembler a, ReadOnlySpan<Instruction> instructions, ulong flag, TannerMeatAddresses addresses, int meat)
        {
            Require(instructions, 3, "Tanner deposit");
            Label vanilla = a.CreateLabel("tannerDepositVanilla");
            Label done = a.CreateLabel("tannerDepositDone");
            BranchUnlessMeatRun(a, rcx, flag, addresses.RunTable, vanilla);
            a.AddInstruction(instructions[0]);
            a.mov(rcx, rsi);
            a.mov(r9d, meat);
            a.mov(__dword_ptr[rsp + 0x28], GranaryFoodCapacity);
            CallAbsolute(a, addresses.Deposit);

            a.mov(rax, addresses.CurrentUnit);
            a.movsxd(rax, __dword_ptr[rax]);
            a.imul(rcx, rax, UnitStride);
            a.movsx(rdx, __word_ptr[rcx + r12 + UnitOwnerOffset]);
            a.mov(r9d, edx);
            a.mov(rax, addresses.FoodDeliveredStat);
            a.inc(__dword_ptr[rax + rdx * 4]);
            a.mov(edx, StatsFoodCategory);
            a.mov(r8d, r13d);
            a.mov(rcx, addresses.StatsManager);
            CallAbsolute(a, addresses.StatsAdd);

            a.mov(rax, addresses.CurrentUnit);
            a.movsxd(rbx, __dword_ptr[rax]);
            a.imul(rcx, rbx, UnitStride);
            a.dec(__word_ptr[rcx + r12 + TannerCarried]);
            a.cmp(__byte_ptr[rcx + r12 + TannerSoundPlayed], dil);
            a.jne(done);
            a.movsx(r8d, __word_ptr[rcx + r12 + TannerTileY]);
            a.mov(r9d, DeliverySoundMeat);
            a.movsx(edx, __word_ptr[rcx + r12 + TannerTileX]);
            a.mov(rcx, addresses.SoundManager);
            CallAbsolute(a, addresses.SoundPlay);
            a.mov(rax, addresses.CurrentUnit);
            a.movsxd(rbx, __dword_ptr[rax]);
            a.imul(rcx, rbx, UnitStride);
            a.mov(__byte_ptr[rcx + r12 + TannerSoundPlayed], r13b);
            a.Label(ref done);
            JumpAbsolute(a, addresses.DepositDone);

            a.Label(ref vanilla);
            a.AddInstruction(instructions[0]);
            a.mov(rcx, rsi);
            CallAbsolute(a, addresses.Deposit);
        }

        /// <summary>State 8: when the storage no longer accepts, a meat run looks for another granary. RCX is the unit offset.</summary>
        internal static void EmitTannerRefind(Assembler a, ReadOnlySpan<Instruction> instructions, ulong flag, TannerMeatAddresses addresses, int meat)
        {
            Require(instructions, 4, "Tanner storage re-search");
            Label vanilla = a.CreateLabel("tannerRefindVanilla");
            Label done = a.CreateLabel("tannerRefindDone");
            BranchUnlessMeatRun(a, rcx, flag, addresses.RunTable, vanilla);
            a.mov(rcx, rsi);
            a.mov(edx, meat);
            a.mov(r8d, 1);
            CallAbsolute(a, addresses.GranaryFind);
            a.jmp(done);
            a.Label(ref vanilla);
            a.mov(rcx, rsi);
            a.AddInstruction(instructions[1]);
            CallAbsolute(a, addresses.ArmouryFind);
            a.Label(ref done);
            a.AddInstruction(instructions[3]);
        }
    }
}
