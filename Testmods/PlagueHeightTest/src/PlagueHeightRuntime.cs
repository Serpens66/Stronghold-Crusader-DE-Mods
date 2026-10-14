using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using BepInEx.Logging;
using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace PlagueHeightTest
{
    internal sealed unsafe class PlagueHeightRuntime
    {
        private readonly ManualLogSource log;
        private readonly IntPtr state;
        private PreparedNativeCode surface;
        private HookTransaction transaction;
        private readonly HookHandle<X64InlineHook>[] hooks = new HookHandle<X64InlineHook>[4];
        private bool published, tickLogged, compareLogged, updateLogged, invalidLogged;
        internal PlagueHeightRuntime(ManualLogSource log, CrusaderLibraryLoadContext context)
        {
            this.log = log;
            state = Marshal.AllocHGlobal(64);
            for (int i = 0; i < 64; i += 8) Marshal.WriteInt64(state, i, 0);
            try
            {
                VerifyBackend();
                int tiles = GameTileManagerAPI.Instance.GetHeightLayer().Length;
                int buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan().Length;
                if (tiles != 320800 || buildings != 3999 || sizeof(GameProjectile) != 0xE8 ||
                    sizeof(GameUnit) != 0x490 || sizeof(GameBuilding) != 0x32C ||
                    Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_HeightElevation)).ToInt32() != 0xB6 ||
                    Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.N0000006A)).ToInt32() != 0xB8 ||
                    Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_HeightElevation)).ToInt32() != 0xEC ||
                    (int)ProjectileType.Disease != 0x16 || (int)AliveState.IsAlive != 2 ||
                    (int)TilePropertyFlag.IsStairs != 0x800 || (int)TilePropertyFlag.IsElevated != 0x10000000)
                    throw new InvalidOperationException("Installed native/Extender layout contract changed.");
                ulong module = unchecked((ulong)context.ModuleHandle.ToInt64());
                if (unchecked((ulong)GameTileManagerAPI.Instance.GetTileManager().ToInt64()) != module + 0x405EDB0 ||
                    (ulong)GameBuildingManagerAPI.Instance.GetBuildingManager().Pointer != module + PlagueHeightAssembly.BuildingManager ||
                    (ulong)GameUnitManagerAPI.Instance.GetUnitManager().Pointer != module + 0x67E8400 ||
                    SHCDESE.GameGlobals.GameGlobalsManager.Instance.GameProjectilesManagerVA != module + PlagueHeightAssembly.ProjectileBase)
                    throw new InvalidOperationException("Installed manager bases do not match native contract.");
                // LoadContext.Memory is a snapshot. Detect competing live patches explicitly.
                if (!new ReadOnlySpan<byte>((void*)(module + 0xC07C0), 0xBF).SequenceEqual(context.Memory.Slice(0xC07C0, 0xBF)))
                    throw new InvalidOperationException("Live native building-height helper was modified.");
                // Validate every candidate and assemble all generators before the first commit.
                for (int i = 0; i < hooks.Length; i++)
                {
                    int site = PlagueHeightAssembly.Sites[i]; int length = i == 0 ? 32 : 26;
                    var expected = Hex(PlagueHeightAssembly.OriginalHex[i]);
                    if (!new ReadOnlySpan<byte>((void*)(module + (uint)site), length).SequenceEqual(expected))
                        throw new InvalidOperationException($"Live hook bytes differ: RVA=0x{site:X}.");
                    using (var probe = new X64InlineHook(module + (uint)site, length))
                        if (probe.DisplacedByteCount != length) throw new InvalidOperationException("RedBird displacement differs.");
                }
                surface = new PreparedNativeCode(a => PlagueHeightAssembly.GenerateSurface(a, module, tiles, buildings));
                ulong helper = unchecked((ulong)surface.Address.ToInt64());
                for (int i = 0; i < hooks.Length; i++)
                {
                    var a = new Assembler(64); int length = i == 0 ? 32 : 26;
                    var original = PlagueHeightAssembly.Decode(Hex(PlagueHeightAssembly.OriginalHex[i]), module + (uint)PlagueHeightAssembly.Sites[i]);
                    PlagueHeightAssembly.GenerateHook(a, original, module + (uint)PlagueHeightAssembly.Sites[i] + (uint)length,
                        i, module, unchecked((ulong)state.ToInt64()), helper);
                    PlagueHeightAssembly.Assemble(a, module + 0x20000000);
                }
                transaction = new HookTransaction(context.Region, SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    new HookTransactionOptions { FailureMode = TransactionFailureMode.RollbackAndThrow, OwnsHooks = true });
                for (int i = 0; i < hooks.Length; i++)
                {
                    int index = i; hooks[i] = new HookHandle<X64InlineHook>();
                    transaction.AddInline(hooks[i], HookTarget.FromAddress(module + (uint)PlagueHeightAssembly.Sites[i]),
                        (a, original, back) => PlagueHeightAssembly.GenerateHook(a, original, back, index, module,
                            unchecked((ulong)state.ToInt64()), helper), hookSize: i == 0 ? 32 : 26);
                }
                var result = transaction.Commit();
                if (!result.IsCompleteSuccess) throw new InvalidOperationException("Incomplete plague hook transaction.");
                for (int i = 0; i < hooks.Length; i++)
                {
                    if (!hooks[i].Success || hooks[i].Hook.DisplacedByteCount != (i == 0 ? 32 : 26) ||
                        !hooks[i].Hook.IsInstalled || hooks[i].Hook.TargetAddress != module + (uint)PlagueHeightAssembly.Sites[i] || hooks[i].Hook.StubAddress == IntPtr.Zero)
                        throw new InvalidOperationException("Committed plague hook contract differs.");
                    var patch = new ReadOnlySpan<byte>((void*)(module + (uint)PlagueHeightAssembly.Sites[i]), i == 0 ? 32 : 26);
                    if (patch[0] != 0xFF || patch[1] != 0x25 || patch[2] != 0 || patch[3] != 0 || patch[4] != 0 || patch[5] != 0 ||
                        BitConverter.ToInt64(patch.Slice(6, 8).ToArray(), 0) != hooks[i].Hook.StubAddress.ToInt64())
                        throw new InvalidOperationException("Committed jump/pointer differs.");
                    for (int j = 14; j < patch.Length; j++) if (patch[j] != 0x90) throw new InvalidOperationException("Committed hook padding differs.");
                }
                Shared.DebugLogHelper.LogInfo(log, "hook confirmed: PlagueHeightTest RVAs=9FDB8/192809/9CB6F/9CC3D; spans=32/26/26/26; inactive until publication; tolerance=20.");
            }
            catch { RollbackUnpublished(); throw; }
        }
        internal void Publish(bool enabled)
        {
            surface.Publish(); published = true; SetEnabled(enabled);
        }
        internal void SetEnabled(bool enabled) { Volatile.Write(ref *(int*)state, enabled ? 1 : 0); }
        internal void OnTick(int tick)
        {
            if (!tickLogged) { tickLogged = true; Shared.DebugLogHelper.LogInfo(log, "PlagueHeightTest runtime after startup cleanup: durable native hooks and Extender OnTick reached."); }
            if (!updateLogged && Marshal.ReadInt64(state, 32) != 0)
            { updateLogged = true; Shared.DebugLogHelper.LogInfo(log, "PlagueHeightTest cloud standing-plane update observed (+20 visual offset)."); }
            if (!invalidLogged && Marshal.ReadInt64(state, 56) != 0)
            { invalidLogged = true; Shared.DebugLogHelper.LogError(log, "PlagueHeightTest observed an invalid tile/building height context: affected damage rejected; affected visual update kept Vanilla. Native bounds remain enforced."); }
            long compared = Marshal.ReadInt64(state, 8);
            if (!compareLogged && compared != 0)
            {
                compareLogged = true;
                long accepted = Marshal.ReadInt64(state, 16), rejected = Marshal.ReadInt64(state, 24);
                Shared.DebugLogHelper.LogInfo(log, $"PlagueHeightTest damage comparison observed: tile={Marshal.ReadInt32(state, 40)}, cloudPlane={Marshal.ReadInt32(state, 44)}, unitPlane={Marshal.ReadInt32(state, 48)}, compared={compared}, heightAccepted={accepted}, heightRejected={rejected}, balanced={compared == accepted + rejected}. Height acceptance precedes Vanilla immunity/resistance/damage.");
            }
        }
        private void RollbackUnpublished()
        {
            if (published) throw new InvalidOperationException("Published hooks cannot be rolled back.");
            transaction?.Dispose(); transaction = null;
            surface?.RollbackUnpublished(); surface = null;
            Marshal.FreeHGlobal(state);
        }
        internal static byte[] Hex(string text)
        {
            var result = new byte[text.Length / 2];
            for (int i = 0; i < result.Length; i++) result[i] = Convert.ToByte(text.Substring(i * 2, 2), 16);
            return result;
        }
        private static void VerifyBackend()
        {
            foreach (var entry in new[] {
                new[] { typeof(X64InlineHook).Assembly.Location, "E085B83B2B87B5D2142C980090D8D9EC616A90A443BD125C17B41E0BB47A44EB" },
                new[] { typeof(RedBird.Core.Memory.ScanRegion).Assembly.Location, "EE4B036C486077F7D8898AE054F124EDDE73E6AE1A059E8991C361E2B720D29F" },
                new[] { typeof(IHook).Assembly.Location, "339CECE116F66F59ECC7DA5E9A5C0B2DFE24F4E7A5E7EFC005864FA77C027A1E" },
                new[] { typeof(Assembler).Assembly.Location, "CC915D1C2E7BE60933C5B129842CA3D08D1FE3CDA6C6D6ADE8824DCDDE16191F" } })
                using (var input = File.OpenRead(entry[0])) using (var sha = SHA256.Create())
                    if (BitConverter.ToString(sha.ComputeHash(input)).Replace("-", "") != entry[1])
                        throw new InvalidOperationException("Installed backend differs: " + entry[0]);
        }
    }
}
