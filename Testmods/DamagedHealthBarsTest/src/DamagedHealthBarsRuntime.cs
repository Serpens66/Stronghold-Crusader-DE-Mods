using BepInEx;
using BepInEx.Logging;
using CrusaderDE;
using Iced.Intel;
using Noesis;
using R3;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Input;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using UnityEngine;

namespace DamagedHealthBarsTest
{
    internal sealed unsafe class DamagedHealthBarsRuntime
    {
        private readonly ManualLogSource log;
        private readonly HookHandle<X64InlineHook> buildingNorth = new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> buildingSouth = new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> unit = new HookHandle<X64InlineHook>();
        private HookTransaction transaction;
        private IDisposable keyDownSubscription;
        private GameTimeManagerAPI timeManager;
        private IntPtr activeFlag;
        private int installed;
        private int postStartupLogged;

        private DamagedHealthBarsRuntime(ManualLogSource log) =>
            this.log = log ?? throw new ArgumentNullException(nameof(log));

        internal static DamagedHealthBarsRuntime Install(CrusaderLibraryLoadContext context, ManualLogSource log)
        {
            var candidate = new DamagedHealthBarsRuntime(log);
            candidate.InstallCandidate(context);
            return candidate;
        }

        private void InstallCandidate(CrusaderLibraryLoadContext context)
        {
            if (context.ModuleHandle == IntPtr.Zero || context.Region == null || context.Memory.Length == 0)
                throw new InvalidOperationException("Incomplete native library load context.");
            if (typeof(X64InlineHook).Assembly.GetName().Version != new Version(1, 5, 0, 0))
                throw new InvalidOperationException("The installed RedBird.X64 version differs from the audited backend.");
            VerifyNativeHash();
            HealthBarNativeContract.ValidateBytes(context.Memory);
            ulong imageBase = unchecked((ulong)context.ModuleHandle.ToInt64());
            HealthBarNativeContract.ValidateLiveBytes(imageBase);

            HookTransaction pending = null;
            try
            {
                activeFlag = Marshal.AllocHGlobal(sizeof(int));
                *(int*)activeFlag.ToPointer() = 0;
                ulong flagAddress = unchecked((ulong)activeFlag.ToInt64());
                ProbeGeneratedStubs(imageBase, flagAddress);

                // Register against publishers that survive SHCDE's startup component cleanup.
                timeManager = GameTimeManagerAPI.Instance ??
                    throw new InvalidOperationException("The game-time event publisher is unavailable.");
                timeManager.OnTick += OnTick;
                keyDownSubscription = InputR3EventHooks.OnKeyDown.Observable.Subscribe(OnKeyDown);

                pending = new HookTransaction(
                    context.Region,
                    SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                    new HookTransactionOptions
                    {
                        FailureMode = TransactionFailureMode.RollbackAndThrow,
                        OwnsHooks = true
                    });
                pending.AddInline(buildingNorth,
                    HookTarget.FromAddress(imageBase + HealthBarNativeContract.BuildingNorthRva),
                    (assembler, instructions, returnAddress) => HealthBarNativeContract.EmitBuildingNorth(
                        assembler, instructions, returnAddress, imageBase, flagAddress),
                    hookSize: HealthBarNativeContract.BuildingLength);
                pending.AddInline(buildingSouth,
                    HookTarget.FromAddress(imageBase + HealthBarNativeContract.BuildingSouthRva),
                    (assembler, instructions, returnAddress) => HealthBarNativeContract.EmitBuildingSouth(
                        assembler, instructions, returnAddress, imageBase, flagAddress),
                    hookSize: HealthBarNativeContract.BuildingLength);
                pending.AddInline(unit,
                    HookTarget.FromAddress(imageBase + HealthBarNativeContract.UnitRva),
                    (assembler, instructions, returnAddress) => HealthBarNativeContract.EmitUnit(
                        assembler, instructions, returnAddress, imageBase, flagAddress),
                    hookSize: HealthBarNativeContract.UnitLength);

                CommitResult result = pending.Commit();
                if (!result.IsCompleteSuccess || !buildingNorth.Success ||
                    !buildingSouth.Success || !unit.Success ||
                    buildingNorth.Hook.DisplacedByteCount != HealthBarNativeContract.BuildingLength ||
                    buildingSouth.Hook.DisplacedByteCount != HealthBarNativeContract.BuildingLength ||
                    unit.Hook.DisplacedByteCount != HealthBarNativeContract.UnitLength)
                    throw new InvalidOperationException("The three health-bar hooks did not satisfy the audited transaction contract: " + result);

                Info("HEALTH_BARS_HOOKS_READY: north=0x489C4/17, south=0x4F9B4/17, " +
                     "unit=0x1A1945/24; enabled=false; nativeHash=" + HealthBarNativeContract.NativeSha256);
                transaction = pending;
                pending = null;
                Volatile.Write(ref installed, 1);
            }
            catch
            {
                // Roll back only this unpublished initialization candidate.
                pending?.Dispose();
                keyDownSubscription?.Dispose();
                if (timeManager != null) timeManager.OnTick -= OnTick;
                if (activeFlag != IntPtr.Zero) Marshal.FreeHGlobal(activeFlag);
                throw;
            }
        }

        private static void VerifyNativeHash()
        {
            string path = Path.Combine(Paths.GameRootPath,
                "Stronghold Crusader Definitive Edition_Data", "Plugins", "x86_64", "CrusaderDE.dll");
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                if (!string.Equals(actual, HealthBarNativeContract.NativeSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Installed native DLL differs from the audited build: " + actual);
            }
        }

        private static void ProbeGeneratedStubs(ulong imageBase, ulong flagAddress)
        {
            using (var probe = new X64InlineHook(
                imageBase + HealthBarNativeContract.BuildingNorthRva,
                HealthBarNativeContract.BuildingLength))
            {
                RequireProbeLength(probe, HealthBarNativeContract.BuildingLength, "building north");
                probe.Generate((assembler, instructions, returnAddress) =>
                    HealthBarNativeContract.EmitBuildingNorth(assembler, instructions,
                        returnAddress, imageBase, flagAddress));
                DecodeAndVerifyStub(probe, imageBase + 0x489F5UL,
                    imageBase + HealthBarNativeContract.BuildingNorthRva + HealthBarNativeContract.BuildingLength);
            }
            using (var probe = new X64InlineHook(
                imageBase + HealthBarNativeContract.BuildingSouthRva,
                HealthBarNativeContract.BuildingLength))
            {
                RequireProbeLength(probe, HealthBarNativeContract.BuildingLength, "building south");
                probe.Generate((assembler, instructions, returnAddress) =>
                    HealthBarNativeContract.EmitBuildingSouth(assembler, instructions,
                        returnAddress, imageBase, flagAddress));
                DecodeAndVerifyStub(probe, imageBase + 0x4F9E4UL,
                    imageBase + HealthBarNativeContract.BuildingSouthRva + HealthBarNativeContract.BuildingLength);
            }
            using (var probe = new X64InlineHook(
                imageBase + HealthBarNativeContract.UnitRva,
                HealthBarNativeContract.UnitLength))
            {
                RequireProbeLength(probe, HealthBarNativeContract.UnitLength, "unit");
                probe.Generate((assembler, instructions, returnAddress) =>
                    HealthBarNativeContract.EmitUnit(assembler, instructions,
                        returnAddress, imageBase, flagAddress));
                DecodeAndVerifyStub(probe, imageBase + 0x1A1971UL,
                    imageBase + HealthBarNativeContract.UnitRva + HealthBarNativeContract.UnitLength);
            }
        }

        private static void RequireProbeLength(X64InlineHook probe, int expected, string name)
        {
            if (probe.DisplacedByteCount != expected)
                throw new InvalidOperationException("Unexpected RedBird " + name +
                    " displacement: " + probe.DisplacedByteCount);
        }

        private static void DecodeAndVerifyStub(X64InlineHook probe, ulong healthTarget, ulong vanillaReturn)
        {
            if (probe.StubAddress == IntPtr.Zero)
                throw new InvalidOperationException("RedBird did not assemble an inline stub.");
            const int maxBytes = 512;
            byte[] bytes = new byte[maxBytes];
            Marshal.Copy(probe.StubAddress, bytes, 0, bytes.Length);
            ulong start = unchecked((ulong)probe.StubAddress.ToInt64());
            var reader = new ByteArrayCodeReader(bytes);
            Decoder decoder = Decoder.Create(64, reader, start);
            bool foundHealth = false;
            bool foundReturn = false;
            for (int i = 0; i < 100 && decoder.IP < start + maxBytes - 16; i++)
            {
                Instruction instruction = decoder.Decode();
                if (instruction.IsInvalid)
                    throw new InvalidOperationException("Iced could not fully decode the generated inline stub.");
                if (JumpTargets(instruction, healthTarget)) foundHealth = true;
                if (JumpTargets(instruction, vanillaReturn))
                {
                    foundReturn = true;
                    break;
                }
                // AddUnrestrictedJmp emits FF 25 00 00 00 00 followed by an
                // eight-byte target literal. The literal is data, not code.
                if (instruction.Mnemonic == Mnemonic.Jmp &&
                    instruction.Op0Kind == OpKind.Memory && instruction.IsIPRelativeMemoryOperand &&
                    instruction.IPRelativeMemoryAddress == instruction.NextIP)
                {
                    reader.Position += sizeof(ulong);
                    decoder.IP += sizeof(ulong);
                }
            }
            if (!foundHealth || !foundReturn)
                throw new InvalidOperationException("Generated inline stub lacks its health or Vanilla continuation.");
        }

        private static bool JumpTargets(Instruction instruction, ulong target)
        {
            if (instruction.Mnemonic != Mnemonic.Jmp) return false;
            if (instruction.NearBranchTarget == target) return true;
            if (instruction.Op0Kind != OpKind.Memory || !instruction.IsIPRelativeMemoryOperand ||
                instruction.IPRelativeMemoryAddress != instruction.NextIP)
                return false;
            return unchecked((ulong)Marshal.ReadInt64(
                new IntPtr(unchecked((long)instruction.IPRelativeMemoryAddress)))) == target;
        }

        private void OnTick(int tick)
        {
            if (Volatile.Read(ref installed) == 0 || GameMap.instance == null ||
                Interlocked.Exchange(ref postStartupLogged, 1) != 0)
                return;
            Info("HEALTH_BARS_POST_STARTUP: persistent native hooks and publisher callbacks are active.");
        }

        private void OnKeyDown(UnityInputEventArgs args)
        {
            if (Volatile.Read(ref installed) == 0 || args == null ||
                args.Phase != EventHookPhase.Pre || args.Key != KeyCode.H || !args.Result)
                return;
            try
            {
                if (!Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.RightAlt)) return;
                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) ||
                    Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) return;
                if (GameMap.instance == null || GameData.Instance?.lastGameState == null ||
                    GameData.Instance.lastGameState.app_mode != 16 ||
                    FatControler.instance?.NGview?.Content == null || MainViewModel.Instance == null)
                    return;
                if (FatControler.instance.NGview.Content.Keyboard?.FocusedElement is TextBox)
                    return;

                int* flag = (int*)activeFlag.ToPointer();
                int next = Volatile.Read(ref *flag) == 0 ? 1 : 0;
                Interlocked.Exchange(ref *flag, next);
                args.Result = false;
                Info("HEALTH_BARS_TOGGLED: enabled=" + (next != 0));
            }
            catch (Exception ex)
            {
                Error("Alt+H handling failed: " + ex);
            }
        }

        private void Info(string message) =>
            log.LogInfo($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");

        private void Error(string message) =>
            log.LogError($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
    }
}
