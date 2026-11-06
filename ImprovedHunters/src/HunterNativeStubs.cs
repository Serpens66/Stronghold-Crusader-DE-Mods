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
    }
}
