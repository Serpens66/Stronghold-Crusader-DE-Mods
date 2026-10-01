using BepInEx;
using BepInEx.Logging;
using CrusaderDE;
using Iced.Intel;
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
using SHCDESE.Interop;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using UnityEngine;

namespace BugfixesAndQoL
{
    internal sealed unsafe class DamagedHealthBarsRuntime
    {
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly HookHandle<X64InlineHook> buildingNorth = new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> buildingSouth = new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> unit = new HookHandle<X64InlineHook>();
        private HookTransaction transaction;
        private IDisposable keyDownSubscription;
        private IntPtr activeFlag;
        private int installed;
        private int postStartupLogged;
        private int fieldMapValidated;
        private ulong nativeImageBase;

        private DamagedHealthBarsRuntime(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        internal static DamagedHealthBarsRuntime Install(CrusaderLibraryLoadContext context,
            ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            var candidate = new DamagedHealthBarsRuntime(log, settings);
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
            ValidateInteropLayout();
            HealthBarNativeContract.ValidateBytes(context.Memory);
            ulong imageBase = unchecked((ulong)context.ModuleHandle.ToInt64());
            HealthBarNativeContract.ValidateLiveBytes(imageBase);
            nativeImageBase = imageBase;

            HookTransaction pending = null;
            try
            {
                activeFlag = Marshal.AllocHGlobal(sizeof(int));
                *(int*)activeFlag.ToPointer() = 0;
                ulong flagAddress = unchecked((ulong)activeFlag.ToInt64());
                ProbeGeneratedStubs(imageBase, flagAddress);

                // InputR3EventHooks is a publisher that survives startup cleanup.
                keyDownSubscription = InputR3EventHooks.OnKeyDown.Observable.Subscribe(OnKeyDown);
                settings.SettingChanged += OnSettingChanged;

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

                transaction = pending;
                pending = null;
                Volatile.Write(ref installed, 1);
            }
            catch
            {
                // Roll back only this unpublished initialization candidate.
                pending?.Dispose();
                keyDownSubscription?.Dispose();
                settings.SettingChanged -= OnSettingChanged;
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

        private static void ValidateInteropLayout()
        {
            if (Marshal.SizeOf<GameUnit>() != 0x490 ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_UnitHover)).ToInt32() != 0x30 ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_HealthBarBlocks)).ToInt32() != 0x34 ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_UnitChimp)).ToInt32() != 0x8A ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_CurrentHealthPercentage)).ToInt32() != 0x2D0 ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_CurrentHealth)).ToInt32() != 0x3C4 ||
                Marshal.OffsetOf<GameUnit>(nameof(GameUnit.r_MaxHealth)).ToInt32() != 0x3C8 ||
                Marshal.SizeOf<GameBuilding>() != 0x32C ||
                Marshal.OffsetOf<GameBuilding>(nameof(GameBuilding.r_CurrentHealth)).ToInt32() != 0x10C ||
                Marshal.OffsetOf<GameBuilding>(nameof(GameBuilding.r_MaxHealth)).ToInt32() != 0x10E)
                throw new InvalidOperationException("Installed Script Extender unit/building interop layout differs from the native field map.");
        }

        private void ValidateLiveFieldMap()
        {
            Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            if (units.Length == 0 || buildings.Length == 0)
                throw new InvalidOperationException("The unit or building array is unavailable.");
            fixed (GameUnit* firstUnit = &units[0])
            fixed (GameBuilding* firstBuilding = &buildings[0])
            {
                ulong expectedUnit = nativeImageBase + 0x67E8A5CUL + 0x490UL;
                ulong expectedBuilding = nativeImageBase + 0x64CCC0CUL + 0x32CUL;
                if (unchecked((ulong)firstUnit) != expectedUnit ||
                    unchecked((ulong)firstBuilding) != expectedBuilding)
                    throw new InvalidOperationException("Native array bases do not match the audited unit/building field formulas.");
            }

            string unitSample = "none";
            for (int spanIndex = 0; spanIndex < units.Length; spanIndex++)
            {
                ref GameUnit unit = ref units[spanIndex];
                if (unit.r_MaxHealth == 0 || unit.r_CurrentHealth > unit.r_MaxHealth) continue;
                unitSample = "id=" + (spanIndex + 1) + ", hp=" + unit.r_CurrentHealth +
                    "/" + unit.r_MaxHealth + ", percent=" + unit.r_CurrentHealthPercentage +
                    ", blocks=" + unit.r_HealthBarBlocks + ", type=" + unit.r_UnitChimp +
                    ", hover=" + unit.r_UnitHover;
                break;
            }
            string buildingSample = "none";
            for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
            {
                ref GameBuilding building = ref buildings[spanIndex];
                if (building.r_MaxHealth == 0 || building.r_CurrentHealth < 0 ||
                    building.r_CurrentHealth > building.r_MaxHealth) continue;
                buildingSample = "id=" + (spanIndex + 1) + ", hp=" + building.r_CurrentHealth +
                    "/" + building.r_MaxHealth + ", type=" + building.r_BuildingType;
                break;
            }
            log.LogDebug("HEALTH_BARS_FIELD_MAP: unitSample={" + unitSample +
                "}, buildingSample={" + buildingSample + "}.");
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

        private bool EnsurePostStartupReady()
        {
            if (Volatile.Read(ref fieldMapValidated) != 0)
                return true;
            try
            {
                ValidateLiveFieldMap();
                Volatile.Write(ref fieldMapValidated, 1);
                if (Interlocked.Exchange(ref postStartupLogged, 1) == 0)
                    Info("HEALTH_BARS_POST_STARTUP: persistent native hooks and input publisher are active.");
                return true;
            }
            catch (Exception ex)
            {
                Error("HEALTH_BARS_FIELD_MAP_FAILED: configured hotkey remains disabled: " + ex);
                return false;
            }
        }

        private void OnSettingChanged(string propertyName)
        {
            if (propertyName != nameof(BugfixesAndQoLViewModel.EnableMod) &&
                propertyName != nameof(BugfixesAndQoLViewModel.EnableClientFeatures) &&
                propertyName != nameof(BugfixesAndQoLViewModel.EnableDamagedHealthBars) &&
                propertyName != nameof(BugfixesAndQoLViewModel.HealthBarHotkey))
                return;
            if (settings.EnableMod && settings.EnableClientFeatures &&
                settings.EnableDamagedHealthBars &&
                settings.HealthBarHotkeyKeyCode != KeyCode.None)
                return;
            if (Volatile.Read(ref installed) == 0 || activeFlag == IntPtr.Zero)
                return;
            int* flag = (int*)activeFlag.ToPointer();
            Interlocked.Exchange(ref *flag, 0);
        }

        private void OnKeyDown(UnityInputEventArgs args)
        {
            if (args == null || args.Phase != EventHookPhase.Pre)
                return;
            try
            {
                if (settings.IsCapturingHealthBarHotkey)
                {
                    var hub = SHCDESE.BepInEx.Bootstrap.Plugin.ModSettingsHubViewModel;
                    if (hub != null && hub.WindowVisibility == Noesis.Visibility.Visible &&
                        ReferenceEquals(hub.SelectedTab?.ViewModel, settings))
                        settings.CaptureHealthBarHotkeyFromInput(args.Key);
                    else
                        settings.CancelHealthBarCapture();
                    return;
                }
                if (args.Key != settings.HealthBarHotkeyKeyCode || args.Key == KeyCode.None)
                    return;
                bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
                bool control = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (alt != settings.HealthBarHotkeyAlt ||
                    control != settings.HealthBarHotkeyControl ||
                    shift != settings.HealthBarHotkeyShift ||
                    !settings.EnableMod || !settings.EnableClientFeatures ||
                    !settings.EnableDamagedHealthBars)
                    return;
                if (Volatile.Read(ref installed) == 0)
                    return;
                if (!args.Result)
                    return;
                GameData gameData = GameData.Instance;
                if (GameMap.instance == null || gameData?.lastGameState == null)
                    return;
                if (gameData.app_mode != 14 && gameData.app_mode != 16)
                    return;
                if (Shared.GameModeHelper.IsMapEditor())
                    return;
                FatControler controller = FatControler.instance;
                if (controller == null || controller.NoesisHasKeyboard)
                    return;
                if (!EnsurePostStartupReady())
                    return;

                int* flag = (int*)activeFlag.ToPointer();
                int next = Volatile.Read(ref *flag) == 0 ? 1 : 0;
                Interlocked.Exchange(ref *flag, next);
                Info("HEALTH_BARS_TOGGLED: enabled=" + (next != 0));
            }
            catch (Exception ex)
            {
                Error("Health-bar hotkey handling failed: " + ex);
            }
        }

        private void Info(string message) =>
            log.LogInfo($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");

        private void Error(string message) =>
            log.LogError($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
    }
}
