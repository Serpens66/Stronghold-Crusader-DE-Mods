using APIShared.NativeSites;
using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using static BugfixesAndQoL.AiOutpostRetargetNative;

namespace BugfixesAndQoL
{
    internal sealed unsafe class AiOutpostRetargetFix
    {
        private const string Site = "AI.OutpostRetarget";
        private const string NativeHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        private static readonly byte[] SelectorEntry = {
            0x44, 0x89, 0x44, 0x24, 0x18, 0x48, 0x89, 0x4C, 0x24, 0x08, 0x48, 0x83, 0xEC, 0x78
        };
        private static readonly byte[] ValidLordEntry = {
            0x4C, 0x63, 0xCA, 0x4C, 0x8B, 0xD1, 0x48, 0x8D, 0x0D, 0xA3, 0x43, 0x4E, 0x03,
            0x4A, 0x0F, 0xBF, 0x84, 0x49, 0xE0, 0x37, 0x3A, 0x00
        };

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void RetargetCallback(IntPtr aiManager, int tribeId);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void SelectOpponent(IntPtr aiManager, int owner, int tribeId);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate long FindValidLord(IntPtr unitManager, int playerId);

        private readonly ManualLogSource log;
        private readonly RetargetCallback callback;
        private readonly HookHandle<X64InlineHook> handle = new HookHandle<X64InlineHook>();
        private HookTransaction transaction;
        private SelectOpponent select;
        private FindValidLord findLord;
        private IntPtr image, demand;
        private bool published;
        private int callbackFailed, firstRetarget;

        internal AiOutpostRetargetFix(ManualLogSource log)
        {
            this.log = log;
            callback = Retarget;
        }

        internal void Install(CrusaderLibraryLoadContext context, bool referenceHashMatches, bool enabled)
        {
            if (context == null || context.ModuleHandle == IntPtr.Zero ||
                !referenceHashMatches || Shared.DebugLogHelper.CurrentNativeSha256 != NativeHash)
                throw new InvalidOperationException("Unknown Outpost retarget native layout.");

            if (sizeof(GameTribe) != TribeStride || sizeof(GamePlayerResources) != PlayerStride ||
                sizeof(GameUnit) != UnitStride ||
                Marshal.OffsetOf<GamePlayerResources>(nameof(GamePlayerResources.r_WinLossState)).ToInt32() != LossOffset ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_AliveState)).ToInt32() + UnitOrigin != UnitAlive ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_GlobalId)).ToInt32() + UnitOrigin != UnitGlobal ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_IsKilledByProjectile)).ToInt32() + UnitOrigin != UnitDeath)
                throw new InvalidOperationException("Outpost retarget interop layout mismatch.");

            image = context.ModuleHandle;
            var resolution = Shared.NativePatternResolver.ResolveUnique(context.Memory,
                BitConverter.ToString(Original).Replace('-', ' '), HookRva, true, Site, log);
            if (resolution.Rva != HookRva ||
                !context.Memory.Slice(ContinueRva, 5).SequenceEqual(new byte[] { 0x4C, 0x89, 0x64, 0x24, 0x50 }))
                throw new InvalidOperationException("Outpost retarget continuation differs from its audited block.");
            VerifyCode(context, HookRva, Original);
            VerifyCode(context, SelectorRva, SelectorEntry);
            VerifyCode(context, ValidLordRva, ValidLordEntry);
            select = Marshal.GetDelegateForFunctionPointer<SelectOpponent>(image + SelectorRva);
            findLord = Marshal.GetDelegateForFunctionPointer<FindValidLord>(image + ValidLordRva);

            string originalHash;
            using (var sha = SHA256.Create())
                originalHash = BitConverter.ToString(sha.ComputeHash(Original)).Replace("-", "");
            var claim = NativeSiteRegistry.Register(BugfixesAndQoLPlugin.PluginGuid, Site, new[] {
                new NativeSiteSpan(HookRva, Original.Length, NativeSiteMode.Exclusive, originalHash),
                new NativeSiteSpan(SelectorRva, SelectorEntry.Length, NativeSiteMode.Observe),
                new NativeSiteSpan(ValidLordRva, ValidLordEntry.Length, NativeSiteMode.Observe)
            });
            if (!claim.Succeeded)
                throw new InvalidOperationException("Outpost retarget native site refused: " + claim.Reason);

            try
            {
                demand = Marshal.AllocHGlobal(1);
                Marshal.WriteByte(demand, 0);
                ulong target = Address(image + HookRva);
                ulong callbackAddress = Address(Marshal.GetFunctionPointerForDelegate(callback));
                transaction = BugfixesHookInfrastructure.CreateOwnedTransaction(context.Region);
                transaction.AddInline(handle, HookTarget.FromAddress(target),
                    (assembler, displaced, continuation) =>
                    {
                        if (continuation != Address(image + ContinueRva))
                            throw new InvalidOperationException("Unexpected Outpost hook continuation.");
                        EmitStub(assembler, displaced, Address(demand), Address(image + LossSlotZeroRva),
                            Address(image + LordSlotsRva), Address(image + LordGlobalsRva), Address(image + UnitManagerRva),
                            callbackAddress);
                    }, hookSize: Original.Length);
                VerifyCode(context, HookRva, Original);
                var result = transaction.Commit();
                if (!result.IsCompleteSuccess || !handle.Success || !handle.IsInstalled ||
                    handle.ResolvedAddress != target || handle.Require().DisplacedByteCount != Original.Length)
                    throw new InvalidOperationException("Outpost retarget hook commit failed: " + result);

                var patch = new byte[Original.Length];
                patch[0] = 0xFF;
                patch[1] = 0x25;
                Array.Copy(BitConverter.GetBytes(Address(handle.Require().StubAddress)), 0, patch, 6, 8);
                for (int i = 14; i < patch.Length; i++) patch[i] = 0x90;
                if (!LiveMatches(HookRva, patch))
                    throw new InvalidOperationException("Outpost retarget RedBird patch differs.");

                published = true;
                SetEnabled(enabled);
            }
            catch
            {
                // Only unpublished hooks are rolled back. If rollback fails, keep the
                // demand byte, delegate and claim alive with demand still disabled.
                transaction?.Dispose();
                if (LiveMatches(HookRva, Original))
                {
                    NativeSiteRegistry.Unregister(BugfixesAndQoLPlugin.PluginGuid, Site);
                    if (demand != IntPtr.Zero) Marshal.FreeHGlobal(demand);
                    demand = IntPtr.Zero;
                }
                throw;
            }
            Shared.DebugLogHelper.LogInfo(log, $"AI_OUTPOST_RETARGET_READY: method={resolution.Method}, rva=0x{HookRva:X}, " +
                $"continuation=0x{ContinueRva:X}, span=22 (8+5+9), selector=0x{SelectorRva:X}, enabled={enabled}.");
        }

        internal void SetEnabled(bool enabled)
        {
            if (published)
                Volatile.Write(ref *(byte*)demand, enabled && Volatile.Read(ref callbackFailed) == 0 ? (byte)1 : (byte)0);
        }

        private void Retarget(IntPtr aiManager, int tribeId)
        {
            if (Volatile.Read(ref callbackFailed) != 0) return;
            try
            {
                if (aiManager != image + AiManagerRva)
                    throw new InvalidOperationException("Unexpected Outpost AI manager.");

                var tribes = GameTribeManagerAPI.Instance;
                if (!tribes.IsValidId(tribeId) || tribeId <= 0 ||
                    !tribes.TryGetTribeById(tribeId, out var tribe) || tribe == null)
                    return;
                if ((byte*)tribe != (byte*)image + TribeManagerRva + TribeOrigin + tribeId * TribeStride)
                    throw new InvalidOperationException("Unexpected Outpost tribe origin.");
                if (tribe->r_AliveState != AliveState.IsAlive || tribe->r_UnitsInGroup == 0)
                    return;

                var players = GamePlayerManagerAPI.Instance;
                int owner = tribe->r_PlayerIdOwner;
                if (!players.IsPlayerIdValid(owner))
                    return;
                short* target = (short*)((byte*)tribe + TargetPlayerOffset);
                short oldTarget = *target;
                if (!players.IsPlayerIdValid(oldTarget) ||
                    !players.TryGetPlayerResourcesById(oldTarget, out var previous))
                    return;
                if ((byte*)&previous->r_WinLossState != (byte*)image + LossSlotZeroRva + oldTarget * PlayerStride)
                    throw new InvalidOperationException("Unexpected Outpost defeat-state address.");
                IntPtr units = image + UnitManagerRva;
                if (previous->r_WinLossState != WinLossState.Loss && findLord(units, oldTarget) != 0)
                    return;

                bool accepted = false;
                try
                {
                    select(aiManager, owner, tribeId);
                    int candidate = *target;
                    if (candidate == oldTarget || candidate == owner || !players.IsPlayerIdValid(candidate) ||
                        players.GetPlayerTeam(candidate) == players.GetPlayerTeam(owner) ||
                        !players.TryGetPlayerResourcesById(candidate, out var replacement) ||
                        replacement->r_WinLossState != WinLossState.None || replacement->r_IsPaused != 0 ||
                        findLord(units, candidate) == 0)
                        return;

                    *(short*)((byte*)tribe + CachedBuildingOffset) = 0;
                    *(uint*)((byte*)tribe + CachedGlobalOffset) = 0;
                    accepted = true;
                }
                finally
                {
                    if (!accepted) *target = oldTarget;
                }
                if (firstRetarget == 0 && Interlocked.Exchange(ref firstRetarget, 1) == 0)
                {
                    try { Shared.DebugLogHelper.LogInfo(log, $"AI_OUTPOST_RETARGET_CONFIRMED: tribe={tribeId}/{tribe->r_GlobalId}, owner={owner}, old={oldTarget}, new={*target}."); }
                    catch { /* The diagnostic is independent of the correction. */ }
                }
            }
            catch (Exception ex)
            {
                if (Interlocked.Exchange(ref callbackFailed, 1) != 0) return;
                Volatile.Write(ref *(byte*)demand, (byte)0);
                try { Shared.DebugLogHelper.LogErrorLimited(log, Site, "AI_OUTPOST_RETARGET_DISABLED: " + ex); }
                catch { /* Never let diagnostics unwind through the game. */ }
            }
        }

        private void VerifyCode(CrusaderLibraryLoadContext context, int rva, byte[] bytes)
        {
            if (!context.Memory.Slice(rva, bytes.Length).SequenceEqual(bytes) || !LiveMatches(rva, bytes))
                throw new InvalidOperationException($"Unexpected or occupied Outpost code at 0x{rva:X}.");
        }

        private bool LiveMatches(int rva, byte[] bytes) =>
            new ReadOnlySpan<byte>((byte*)image + rva, bytes.Length).SequenceEqual(bytes);

        private static ulong Address(IntPtr pointer) => unchecked((ulong)pointer.ToInt64());
    }
}
