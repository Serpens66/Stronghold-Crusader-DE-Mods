// Feature: Lifecycle orchestration for the Bugfixes and QoL features.
using BepInEx.Logging;
using System;

namespace BugfixesAndQoL
{
    public sealed class BugfixesAndQoLRuntime : IDisposable
    {
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly TroopMovementFix3Runtime troopMovementFixRuntime;
        private MinimapPlacementClickHook minimapPlacementClickHook;
        private SkirmishAiSelectionMemoryHook skirmishAiSelectionMemoryHook;
        private AutoTradeSellZeroHook autoTradeSellZeroHook;
        private EnemyProximityBulldozeCursorHook enemyProximityBulldozeCursorHook;
        private MarketKeyMainTradeMenuHook marketKeyMainTradeMenuHook;
        private HdMarketViewHook hdMarketViewHook;
        private CameraMovementModifierHook cameraMovementModifierHook;
        private AssemblyPointPlacementPatch assemblyPointPlacementPatch;
        private PlaguePopularityFix plaguePopularityFix;
        private IntPtr libraryHandle;
        private int libraryLength;
        private bool nativeLibraryAvailable;
        private bool fixedLayoutHashValidated;
        private bool hooksSubscribed;
        private bool settingsSubscribed;
        private bool enemyProximityFixedLayoutErrorLogged;
        private bool assemblyPointPlacementPatchUnavailable;
        private bool plaguePopularityFixUnavailable;

        public BugfixesAndQoLRuntime(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            troopMovementFixRuntime = new TroopMovementFix3Runtime(log, settings);
            settings.SettingChanged += OnSettingChanged;
            settingsSubscribed = true;
        }

        public void InitializeNative(IntPtr newLibraryHandle, ReadOnlySpan<byte> memory, bool isFixedLayoutHashValidated)
        {
            if (nativeLibraryAvailable)
                return;

            if (newLibraryHandle == IntPtr.Zero || memory.Length == 0)
                throw new ArgumentException("The Crusader library is unavailable.");

            libraryHandle = newLibraryHandle;
            libraryLength = memory.Length;
            fixedLayoutHashValidated = isFixedLayoutHashValidated;
            nativeLibraryAvailable = true;
            troopMovementFixRuntime.InitializeNative(newLibraryHandle, memory, isFixedLayoutHashValidated);
            EnsurePlaguePopularityFix();
            ApplyAssemblyPointPlacementPatchSetting();
        }

        public void ApplySettings()
        {
            TryInitializeFeature("AI castle/settings selection memory", EnsureAiSelectionHook);
            troopMovementFixRuntime.ApplySetting();
            ApplyAssemblyPointPlacementPatchSetting();

            if (settings.EnableMod)
                SubscribeHooks();
            else
                UnsubscribeHooks();
        }

        public void Dispose()
        {
            UnsubscribeHooks();
            skirmishAiSelectionMemoryHook?.Dispose();
            skirmishAiSelectionMemoryHook = null;
            DisableAssemblyPointPlacementPatch();
            plaguePopularityFix?.Dispose();
            plaguePopularityFix = null;
            troopMovementFixRuntime.Dispose();
            nativeLibraryAvailable = false;
            libraryHandle = IntPtr.Zero;
            libraryLength = 0;

            if (settingsSubscribed)
            {
                settings.SettingChanged -= OnSettingChanged;
                settingsSubscribed = false;
            }
        }

        private void SubscribeHooks()
        {
            if (hooksSubscribed || !settings.EnableMod)
                return;

            TryInitializeFeature("minimap placement", () =>
                minimapPlacementClickHook = new MinimapPlacementClickHook(log, settings));
            TryInitializeFeature("market autotrade sell threshold", () =>
                autoTradeSellZeroHook = new AutoTradeSellZeroHook(log));
            TryInitializeFeature("market key main-menu return", () =>
                marketKeyMainTradeMenuHook = new MarketKeyMainTradeMenuHook(log, settings));
            TryInitializeFeature("HD market view", () =>
                hdMarketViewHook = new HdMarketViewHook(log, settings));
            TryInitializeFeature("camera movement modifier", () =>
                cameraMovementModifierHook = new CameraMovementModifierHook(log, settings));

            if (fixedLayoutHashValidated)
            {
                TryInitializeFeature("enemy-proximity bulldoze cursor", () =>
                    enemyProximityBulldozeCursorHook = new EnemyProximityBulldozeCursorHook(log, settings));
            }
            else if (!enemyProximityFixedLayoutErrorLogged)
            {
                enemyProximityFixedLayoutErrorLogged = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    "Bugfixes and QoL enemy-proximity bulldoze cursor remains inactive because its fixed native layout is not validated for this CrusaderDE.dll.");
            }

            hooksSubscribed = true;
            Shared.DebugLogHelper.LogDebug(log, "Bugfixes and QoL feature hooks reconciled.");
        }

        private void UnsubscribeHooks()
        {
            minimapPlacementClickHook?.Dispose();
            minimapPlacementClickHook = null;
            autoTradeSellZeroHook?.Dispose();
            autoTradeSellZeroHook = null;
            enemyProximityBulldozeCursorHook?.Dispose();
            enemyProximityBulldozeCursorHook = null;
            marketKeyMainTradeMenuHook?.Dispose();
            marketKeyMainTradeMenuHook = null;
            hdMarketViewHook?.Dispose();
            hdMarketViewHook = null;
            cameraMovementModifierHook?.Dispose();
            cameraMovementModifierHook = null;
            hooksSubscribed = false;
        }

        private void EnsureAiSelectionHook()
        {
            if (skirmishAiSelectionMemoryHook == null)
                skirmishAiSelectionMemoryHook = new SkirmishAiSelectionMemoryHook(log, settings);
        }

        private void TryInitializeFeature(string featureName, Action initialize)
        {
            try
            {
                initialize();
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL feature '{featureName}' could not be initialized and remains inactive: {ex}");
            }
        }

        private void OnSettingChanged(string propertyName)
        {
            if (propertyName == nameof(BugfixesAndQoLViewModel.EnableTroopMovementFix))
            {
                troopMovementFixRuntime.ApplySetting();
                return;
            }

            ApplySettings();
        }

        private void ApplyAssemblyPointPlacementPatchSetting()
        {
            if (!nativeLibraryAvailable)
                return;

            if (settings.EnableMod)
                InstallAssemblyPointPlacementPatch();
            else
                DisableAssemblyPointPlacementPatch();
        }

        private unsafe ReadOnlySpan<byte> GetNativeLibraryMemory()
        {
            // The game DLL stays loaded for the process lifetime.
            return new ReadOnlySpan<byte>(libraryHandle.ToPointer(), libraryLength);
        }

        private void EnsurePlaguePopularityFix()
        {
            if (!nativeLibraryAvailable || plaguePopularityFix != null || plaguePopularityFixUnavailable)
                return;

            try
            {
                // This hook remains installed while disabled so an in-progress herd can
                // still be identified if the host enables the setting later.
                plaguePopularityFix = new PlaguePopularityFix(
                    log,
                    settings,
                    GetNativeLibraryMemory(),
                    unchecked((ulong)libraryHandle.ToInt64()));
            }
            catch (Exception ex)
            {
                plaguePopularityFixUnavailable = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL plague popularity fix could not be installed; Vanilla behavior remains active: {ex}");
            }
        }

        private void InstallAssemblyPointPlacementPatch()
        {
            if (assemblyPointPlacementPatch != null || assemblyPointPlacementPatchUnavailable)
                return;

            try
            {
                assemblyPointPlacementPatch = new AssemblyPointPlacementPatch(
                    log,
                    GetNativeLibraryMemory(),
                    unchecked((ulong)libraryHandle.ToInt64()));
            }
            catch (Exception ex)
            {
                // A changed or already hooked signature cannot become valid later in this process.
                assemblyPointPlacementPatchUnavailable = true;
                Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL assembly-point placement patch could not be installed: {ex}");
            }
        }

        private void DisableAssemblyPointPlacementPatch()
        {
            assemblyPointPlacementPatch?.Dispose();
            assemblyPointPlacementPatch = null;
        }
    }
}
