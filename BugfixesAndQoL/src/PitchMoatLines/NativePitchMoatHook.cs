using System;
using System.IO;
using System.Runtime.InteropServices;
using APIShared.NativeSites;
using Iced.Intel;
using Microsoft.Extensions.Logging.Abstractions;
using RedBird.Abstractions.Hooks;
using RedBird.X64.Hooks;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL.PitchMoatLines
{
    internal sealed unsafe partial class NativePlacement
    {
        private const string PitchMoatSiteName = "pitch-line-preserve-planned-moat";
        private const int PitchMoatRva = 0x6D788, PitchMoatLength = 16;
        private const int PitchMoatOccupiedRva = PitchMoatRva + PitchMoatLength;
        private const int PitchMoatVanillaRva = 0x6D8DF, PitchMoatPreserveRva = 0x6D912;
        private const int PitchMoatOccupantOffset = 0xBF6C00;
        private const string PitchMoatLoadAndTest = "0FBFAC73006CBF0085ED";
        private const string PitchMoatOriginal = PitchMoatLoadAndTest + "0F8447010000";
        private const string PitchMoatVanillaEntry = "F784B30084890000400040";
        private const int ChoreExecutingSlot = 0x84CC8, ChoreTransferDirection = 0x84CCC;
        private const int ChoreTransferredBytes = 0x370BF8, ChoreExecutingSender = 0x36FA4C, ChoreLocalSender = 0x36FA50;
        private const int ChoreSender = 4, ChoreOpcode = 8, ChoreCommandId = 12, ChoreBody = 17;
        private const int ChoreCommandIdStride = 100_000_000;
        private const int StageAuxiliaryRva = 0x86C1340, EditorChoreActorRva = 0x88E3D78;
        private const int PitchChoreBytes = 10;
        private const int PitchChoreShape = (int)eMappers.MAPPER_PITCH_DITCH | (BrushWidth << 16);
        private const int LogicOffset = 0x898400, MoatTaskIndexOffset = 0x1EA23F0;
        private const int MoatTaskBase = 0x1F3EE30, MoatTaskLimit = 0x2038E30, MoatTaskStrideShift = 4;
        private const int MoatTaskTile = 0, MoatTaskX = 4, MoatTaskY = 6, MoatTaskOwner = 12;
        private const int MoatTaskProgress = 13, MoatTaskWorkType = 14, MoatCompletedProgress = 4;
        private const int TokenActive = 0, TokenSlot = 4, TokenSender = 8, TokenCommand = 12;
        private const int TokenTile = 16, TokenXY = 20, TokenActor = 24, TokenOutcome = 28, TokenOpcode = 32, TokenSize = 36;
        private const int PitchExecuted = 1, PitchPreserved = 2;

        private X64InlineHook pitchMoatHook;
        private IntPtr pitchMoatToken;
        internal bool PitchMoatReady { get; private set; }
        internal bool PitchMoatExecuted => PitchMoatReady && ReadPitchMoatToken(TokenOutcome) != 0;
        internal bool PitchMoatPreserved => PitchMoatReady && ReadPitchMoatToken(TokenOutcome) == PitchPreserved;

        private void InstallPitchMoatHook(CrusaderLibraryLoadContext context)
        {
            RequireOriginal(context, PitchMoatRva, PitchMoatOriginal, true);
            RequireOriginal(context, PitchMoatOccupiedRva, "410FBFC483C0D383F82177184898410FB68400F0DF0600418B8C80E8DF06004903C8FFE1", true);
            // Pitch's occupied-field dispatch is a table jump to the same moat-removal entry
            // reached by the JE on an empty field. It performs no unit cleanup.
            RequireOriginal(context, 0x6DFE8, "DFD80600", true);
            RequireOriginal(context, 0x6DFF0 + ((int)eStructs.STRUCT_PITCH_DITCH - 0x2D), "00", true);
            RequireOriginal(context, PitchMoatVanillaRva, PitchMoatVanillaEntry, false);
            RequireOriginal(context, PitchMoatPreserveRva, "8BAC24B0000000", true);
            RequireOriginal(context, 0x12090, "40534883EC408B05506F5E08", false);
            RequireOriginal(context, 0x23990, "48895C240848896C24104889742418", false);
            RequireOriginal(context, 0x20950, "8B81F40B37004C8BC141B9F4010000", false);
            RequireOriginal(context, 0x1F7E0, "40534883EC30488BD9E8F2260000", false);
            VerifyPitchMoatVanillaEntry(context);
            var claim = NativeSiteRegistry.Register(OwnerGuid, PitchMoatSiteName,
                new[] { new NativeSiteSpan(PitchMoatRva, PitchMoatLength, NativeSiteMode.Exclusive) });
            if (!claim.Succeeded)
                throw new InvalidOperationException("Pitch/moat coexistence site unavailable: " + claim);

            bool publishing = false;
            try
            {
                pitchMoatToken = Marshal.AllocHGlobal(TokenSize);
                new Span<byte>((void*)pitchMoatToken, TokenSize).Clear();
                ulong target = NativeAddress(Address(PitchMoatRva));
                ulong occupied = NativeAddress(Address(PitchMoatOccupiedRva));
                ulong vanilla = NativeAddress(Address(PitchMoatVanillaRva));
                ulong preserve = NativeAddress(Address(PitchMoatPreserveRva));
                ulong token = NativeAddress(pitchMoatToken);
                ulong chores = NativeAddress(choreManager);
                ulong tiles = NativeAddress(tileManager);
                ulong auxiliary = NativeAddress(Address(StageAuxiliaryRva));
                pitchMoatHook = new X64InlineHook(target, PitchMoatLength, NullLogger.Instance, PitchMoatSiteName);
                if (pitchMoatHook.TargetAddress != target || pitchMoatHook.DisplacedByteCount != PitchMoatLength)
                    throw new InvalidOperationException("RedBird selected an unexpected pitch/moat span.");
                pitchMoatHook.Generate((a, _, __) => EmitPitchMoatStub(a, token, chores, tiles, auxiliary, occupied, vanilla, preserve));
                VerifyEmittedPitchMoatStub(token, chores, tiles, auxiliary, occupied, vanilla, preserve);

                // Once published, the hook and token remain rooted through NativePlacement.rooted.
                publishing = true;
                pitchMoatHook.Enable();
                if (!pitchMoatHook.IsInstalled || Marshal.ReadByte(Address(PitchMoatRva)) != 0xFF ||
                    Marshal.ReadByte(Address(PitchMoatRva), 1) != 0x25 || Read(PitchMoatRva + 2) != 0 ||
                    Marshal.ReadInt64(Address(PitchMoatRva), 6) != pitchMoatHook.StubAddress.ToInt64())
                    throw new InvalidOperationException("Published pitch/moat jump does not match its prepared stub.");
                for (int i = 14; i < PitchMoatLength; i++)
                    if (Marshal.ReadByte(Address(PitchMoatRva), i) != 0x90)
                        throw new InvalidOperationException("Unexpected published pitch/moat padding.");
                PitchMoatReady = true;
            }
            catch
            {
                if (!publishing)
                {
                    pitchMoatHook?.Dispose();
                    if (pitchMoatToken != IntPtr.Zero) Marshal.FreeHGlobal(pitchMoatToken);
                    pitchMoatToken = IntPtr.Zero;
                    NativeSiteRegistry.Unregister(OwnerGuid, PitchMoatSiteName);
                }
                throw;
            }
        }

        private void VerifyPitchMoatVanillaEntry(CrusaderLibraryLoadContext context)
        {
            IntPtr entry = Address(PitchMoatVanillaRva);
            bool claimed = NativeSiteRegistry.TryGetWriter(PitchMoatVanillaRva, out var writer);
            bool knownWriter = claimed && writer.OwnerGuid == "BugfixesAndQoL_Serp" && writer.Name == "MoatTerrainRestoration" &&
                writer.Rva == PitchMoatVanillaRva && writer.Length == 20 && writer.Mode == NativeSiteMode.Exclusive;
            if (claimed && !knownWriter)
                throw new InvalidOperationException("Unknown writer at the adjacent pitch/moat continuation: " + writer);
            if (knownWriter && Marshal.ReadByte(entry) == 0xFF && Marshal.ReadByte(entry, 1) == 0x25 &&
                Marshal.ReadInt32(entry, 2) == 0 && Marshal.ReadInt64(entry, 6) != 0)
            {
                for (int i = 14; i < writer.Length; i++)
                    if (Marshal.ReadByte(entry, i) != 0x90)
                        throw new InvalidOperationException("Unexpected adjacent moat-terrain jump padding.");
                return;
            }
            RequireOriginal(context, PitchMoatVanillaRva, PitchMoatVanillaEntry, true);
        }

        private static ulong NativeAddress(IntPtr pointer) => unchecked((ulong)pointer.ToInt64());
        private int ReadPitchMoatToken(int offset) => Marshal.ReadInt32(pitchMoatToken, offset);
        private void WritePitchMoatToken(int offset, int value) => Marshal.WriteInt32(pitchMoatToken, offset, value);

        internal bool TryArmPitchMoat(LineTile tile, int tileId, int player, int slot)
        {
            if (!PitchMoatReady || (uint)tileId >= GameTileManagerView.NativePackedTileCapacity || (uint)slot >= ChoreQueueCount ||
                (ReadPitchMoatToken(TokenActive) != 0 && HasQueuedPitchMoatToken()))
                return false;
            ClearPitchMoatScope();
            int sender = Marshal.ReadInt32(choreManager, ChoreLocalSender);
            bool editorOwner = Read(NativeMapTypeRva) == EditorPitchOwnerMapType;
            int actor = editorOwner ? Read(EditorChoreActorRva) : sender;
            int serial = Marshal.ReadInt32(choreManager, ChoreSerialOffset);
            // The local Chore sender field aliases NativePlayerRva in this image.
            if (sender != player ||
                sender < 1 || sender > GamePlayerManagerAPI.MAX_PLAYERS ||
                actor < 1 || actor > GamePlayerManagerAPI.MAX_PLAYERS ||
                serial < 0 || serial >= ChoreCommandIdStride)
                return false;

            WritePitchMoatToken(TokenSlot, slot);
            WritePitchMoatToken(TokenSender, sender);
            WritePitchMoatToken(TokenCommand, sender * ChoreCommandIdStride + serial);
            WritePitchMoatToken(TokenTile, tileId);
            WritePitchMoatToken(TokenXY, tile.X | (tile.Y << 16));
            WritePitchMoatToken(TokenActor, actor);
            WritePitchMoatToken(TokenOpcode, (byte)ChoreType.PlaceBuildingCategory2);
            WritePitchMoatToken(TokenActive, 1);
            return true;
        }

        private bool TryArmNetworkPitchMoat(LineTile tile, int tileId, int player, int commandId)
        {
            if (!PitchMoatReady || ReadPitchMoatToken(TokenActive) != 0 ||
                (uint)tileId >= GameTileManagerView.NativePackedTileCapacity ||
                Marshal.ReadInt32(choreManager, ChoreTransferDirection) != 0 ||
                Marshal.ReadInt32(choreManager, ChoreExecutingSender) != player)
                return false;
            int slot = Marshal.ReadInt32(choreManager, ChoreExecutingSlot);
            if ((uint)slot >= ChoreQueueCount) return false;
            IntPtr entry = ChoreSlotAddress(slot);
            if (Marshal.ReadByte(entry, ChoreOpcode) != (byte)ChoreType.ScriptExtenderChore ||
                Marshal.ReadInt32(entry, ChoreSender) != player ||
                Marshal.ReadInt32(entry, ChoreCommandId) != commandId) return false;
            // The public authenticated channel has already verified this exact packet
            // against the native106 body. Never interpret its MessagePack body as28.
            ClearPitchMoatScope();
            WritePitchMoatToken(TokenSlot, slot);
            WritePitchMoatToken(TokenSender, player);
            WritePitchMoatToken(TokenCommand, commandId);
            WritePitchMoatToken(TokenTile, tileId);
            WritePitchMoatToken(TokenXY, tile.X | (tile.Y << 16));
            WritePitchMoatToken(TokenActor, player);
            WritePitchMoatToken(TokenOpcode, (byte)ChoreType.ScriptExtenderChore);
            WritePitchMoatToken(TokenActive, 1);
            return true;
        }

        internal bool ConfirmPitchMoatSubmission(bool enqueued)
        {
            if (PitchMoatExecuted) return enqueued; // Immediate execution can already have cleared its slot.
            if (ReadPitchMoatToken(TokenActive) == 0) return false;
            // A matching queued command can still run even if a separate staging check failed.
            if (HasQueuedPitchMoatToken()) return enqueued;
            ClearPitchMoatScope();
            return false;
        }

        internal void ClearPitchMoatScope()
        {
            if (!PitchMoatReady) return;
            WritePitchMoatToken(TokenActive, 0);
            WritePitchMoatToken(TokenOutcome, 0);
        }

        private bool TryFindFreeChoreSlot(out int slot)
        {
            slot = Marshal.ReadInt32(choreManager, ChoreQueueCursor);
            if ((uint)slot >= ChoreQueueCount) return false;
            for (int i = 0; i < ChoreQueueCount; i++)
            {
                if (++slot == ChoreQueueCount) slot = 0;
                if (!IsLiveChore(ChoreSlotAddress(slot))) return true;
            }
            return false;
        }

        private IntPtr ChoreSlotAddress(int slot) => IntPtr.Add(choreManager, ChoreQueueStart + slot * ChoreQueueStride);

        private static bool IsLiveChore(IntPtr slot)
        {
            int state = unchecked((sbyte)Marshal.ReadByte(slot, ChoreStateOffset));
            return state != 0 && state < FreeTerminalState;
        }

        private bool HasQueuedPitchMoatToken()
        {
            int slot = ReadPitchMoatToken(TokenSlot);
            if ((uint)slot >= ChoreQueueCount) return false;
            IntPtr entry = ChoreSlotAddress(slot);
            return IsLiveChore(entry) && Marshal.ReadInt32(entry, ChoreSender) == ReadPitchMoatToken(TokenSender) &&
                Marshal.ReadByte(entry, ChoreOpcode) == (byte)ChoreType.PlaceBuildingCategory2 &&
                Marshal.ReadInt32(entry, ChoreCommandId) == ReadPitchMoatToken(TokenCommand) &&
                Marshal.ReadInt32(entry, ChoreBody) == ReadPitchMoatToken(TokenXY) &&
                Marshal.ReadInt32(entry, ChoreBody + 4) == PitchChoreShape;
        }

        private static void EmitPitchMoatStub(Assembler a, ulong token, ulong chores, ulong tiles,
            ulong auxiliary, ulong occupied, ulong vanilla, ulong preserve)
        {
            Label ordinary = a.CreateLabel(), unrelated = a.CreateLabel();
            Label vanillaPayload = a.CreateLabel(), verified = a.CreateLabel();
            // Preserve the signed occupant load and TEST flags so the ordinary branch can
            // resume at the same target as the native JE. For pitch both targets otherwise
            // reach the same moat-removal entry.
            a.movsx(ebp, __word_ptr[rbx + rsi * 2 + PitchMoatOccupantOffset]);
            a.test(ebp, ebp);
            a.pushfq();
            a.push(rax);
            a.mov(rax, token);
            a.cmp(__dword_ptr[rax + TokenActive], 0); a.je(ordinary);
            a.cmp(r12d, (int)eStructs.STRUCT_PITCH_DITCH); a.jne(ordinary);
            a.cmp(r13d, BrushWidth); a.jne(ordinary);
            a.cmp(esi, __dword_ptr[rax + TokenTile]); a.jne(ordinary);
            a.cmp(edi, __dword_ptr[rax + TokenActor]); a.jne(ordinary);
            a.push(rcx); a.push(rdx);
            a.mov(rcx, tiles);
            a.cmp(rbx, rcx); a.jne(unrelated);
            a.mov(edx, r14d); a.shl(edx, 16); a.or(edx, r15d);
            a.cmp(edx, __dword_ptr[rax + TokenXY]); a.jne(unrelated);
            a.mov(rcx, chores);
            a.cmp(__dword_ptr[rcx + ChoreTransferDirection], 0); a.jne(unrelated);
            a.mov(edx, __dword_ptr[rcx + ChoreExecutingSlot]);
            a.cmp(edx, __dword_ptr[rax + TokenSlot]); a.jne(unrelated);
            a.imul(edx, edx, ChoreQueueStride);
            a.mov(ecx, __dword_ptr[rcx + ChoreExecutingSender]);
            a.cmp(ecx, __dword_ptr[rax + TokenSender]); a.jne(unrelated);
            a.mov(rcx, chores + ChoreQueueStart);
            a.add(rcx, rdx);
            a.mov(edx, __dword_ptr[rax + TokenSender]);
            a.cmp(edx, __dword_ptr[rcx + ChoreSender]); a.jne(unrelated);
            a.mov(edx, __dword_ptr[rax + TokenCommand]);
            a.cmp(edx, __dword_ptr[rcx + ChoreCommandId]); a.jne(unrelated);
            a.cmp(__dword_ptr[rax + TokenOpcode], (byte)ChoreType.ScriptExtenderChore); a.jne(vanillaPayload);
            a.cmp(__byte_ptr[rcx + ChoreOpcode], (byte)ChoreType.ScriptExtenderChore); a.jne(unrelated);
            a.jmp(verified);
            a.Label(ref vanillaPayload);
            a.cmp(__dword_ptr[rax + TokenOpcode], (byte)ChoreType.PlaceBuildingCategory2); a.jne(unrelated);
            a.cmp(__byte_ptr[rcx + ChoreOpcode], (byte)ChoreType.PlaceBuildingCategory2); a.jne(unrelated);
            a.mov(rdx, chores);
            a.cmp(__dword_ptr[rdx + ChoreTransferredBytes], PitchChoreBytes); a.jne(unrelated);
            a.mov(edx, __dword_ptr[rax + TokenXY]);
            a.cmp(edx, __dword_ptr[rcx + ChoreBody]); a.jne(unrelated);
            a.cmp(__dword_ptr[rcx + ChoreBody + 4], PitchChoreShape); a.jne(unrelated);
            a.mov(rdx, auxiliary);
            a.movzx(edx, __word_ptr[rdx]);
            a.cmp(dx, __word_ptr[rcx + ChoreBody + 8]); a.jne(unrelated);

            a.Label(ref verified);
            a.mov(__dword_ptr[rax + TokenActive], 0);
            a.mov(__dword_ptr[rax + TokenOutcome], PitchExecuted);
            a.mov(ecx, __dword_ptr[rbx + rsi * 4 + LogicOffset]);
            a.and(ecx, (int)(TilePropertyFlag.PlannedMoat | TilePropertyFlag.IsMoat));
            a.cmp(ecx, (int)TilePropertyFlag.PlannedMoat); a.jne(unrelated);
            a.movzx(ecx, __word_ptr[rbx + rsi * 2 + MoatTaskIndexOffset]);
            a.test(ecx, ecx); a.je(unrelated);
            a.mov(edx, __dword_ptr[rbx + MoatTaskLimit]);
            a.cmp(edx, GameTileManagerView.MoatWorkTaskSlotCapacity); a.ja(unrelated);
            a.cmp(ecx, edx); a.jae(unrelated);
            a.shl(ecx, MoatTaskStrideShift);
            a.lea(rcx, __qword_ptr[rbx + rcx + MoatTaskBase]);
            a.cmp(esi, __dword_ptr[rcx + MoatTaskTile]); a.jne(unrelated);
            a.cmp(r15w, __word_ptr[rcx + MoatTaskX]); a.jne(unrelated);
            a.cmp(r14w, __word_ptr[rcx + MoatTaskY]); a.jne(unrelated);
            a.movzx(edx, __byte_ptr[rcx + MoatTaskOwner]);
            a.dec(edx);
            a.cmp(edx, GamePlayerManagerAPI.MAX_PLAYERS); a.jae(unrelated);
            a.cmp(__byte_ptr[rcx + MoatTaskProgress], MoatCompletedProgress); a.jae(unrelated);
            a.cmp(__byte_ptr[rcx + MoatTaskWorkType], (byte)MoatWorkType.DigPlannedMoat); a.jne(unrelated);
            a.mov(__dword_ptr[rax + TokenOutcome], PitchPreserved);
            a.pop(rdx); a.pop(rcx); a.pop(rax); a.popfq();
            a.jmp(preserve);

            a.Label(ref unrelated);
            a.pop(rdx); a.pop(rcx);
            a.Label(ref ordinary);
            a.pop(rax); a.popfq();
            a.je(vanilla);
            a.jmp(occupied);
        }

        private void VerifyEmittedPitchMoatStub(ulong token, ulong chores, ulong tiles, ulong auxiliary,
            ulong occupied, ulong vanilla, ulong preserve)
        {
            if (pitchMoatHook.State != HookState.Prepared || pitchMoatHook.StubAddress == IntPtr.Zero)
                throw new InvalidOperationException("RedBird did not prepare a pitch/moat stub.");
            ulong ip = NativeAddress(pitchMoatHook.StubAddress);
            var assembler = new Assembler(64);
            EmitPitchMoatStub(assembler, token, chores, tiles, auxiliary, occupied, vanilla, preserve);
            using (var stream = new MemoryStream())
            {
                if (!assembler.TryAssemble(new StreamCodeWriter(stream), ip, out string error, out _))
                    throw new InvalidOperationException("Could not assemble pitch/moat stub: " + error);
                byte[] expected = stream.ToArray();
                byte[] installed = new byte[expected.Length];
                Marshal.Copy(pitchMoatHook.StubAddress, installed, 0, installed.Length);
                if (!installed.AsSpan().SequenceEqual(expected) ||
                    BitConverter.ToString(installed, 0, PitchMoatLoadAndTest.Length / 2).Replace("-", "") != PitchMoatLoadAndTest)
                    throw new InvalidOperationException("Prepared pitch/moat stub differs from the audited instructions.");
                var decoder = Decoder.Create(64, new ByteArrayCodeReader(installed), ip);
                bool foundPreserve = false;
                bool foundEmpty = false;
                while (decoder.IP < ip + (ulong)installed.Length)
                {
                    Instruction instruction = decoder.Decode();
                    if (instruction.IsInvalid) break;
                    if (foundPreserve && instruction.Mnemonic == Mnemonic.Je && instruction.NearBranchTarget == vanilla)
                    {
                        foundEmpty = true;
                        continue;
                    }
                    if (instruction.Mnemonic != Mnemonic.Jmp) continue;
                    if (instruction.NearBranchTarget >= ip && instruction.NearBranchTarget < ip + (ulong)installed.Length)
                        continue;
                    if (!foundPreserve && PitchMoatJumpTargets(instruction, preserve, ip, installed))
                        foundPreserve = true;
                    else if (foundEmpty && PitchMoatJumpTargets(instruction, occupied, ip, installed))
                        return;
                    else break;
                }
            }
            throw new InvalidOperationException("Prepared pitch/moat stub has unexpected native continuations.");
        }

        private static bool PitchMoatJumpTargets(Instruction instruction, ulong target, ulong ip, byte[] stub)
        {
            if (instruction.NearBranchTarget == target) return true;
            if (!instruction.IsIPRelativeMemoryOperand || instruction.IPRelativeMemoryAddress < ip) return false;
            ulong offset = instruction.IPRelativeMemoryAddress - ip;
            return offset <= (ulong)(stub.Length - sizeof(ulong)) && BitConverter.ToUInt64(stub, (int)offset) == target;
        }
    }
}
