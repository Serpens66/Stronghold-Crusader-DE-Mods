[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Write-Host '[Preflight] Checking permanent published hooks and executable-memory mutations...'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$roots = @('.')
$excludedSegments = @(
    '\.git\', '\bin\', '\obj\', '\tests\', '\.inspect\', '\_inspect\',
    '\.release-output\', '\BepInEx\plugins\', '\packages\', '\shcde-script-extender\'
)
$files = foreach ($relativeRoot in $roots) {
    $root = Join-Path $workspace $relativeRoot
    if (-not (Test-Path -LiteralPath $root)) { continue }
    Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.cs' -ErrorAction SilentlyContinue | Where-Object {
        $path = $_.FullName
        (-not ($excludedSegments | Where-Object { $path.Contains($_) })) -and
        (-not $path.Contains('\Testmods\') -or $path.Contains('\Testmods\MoatMove\'))
    }
}

$errors = [System.Collections.Generic.List[string]]::new()
foreach ($file in $files) {
    if (-not $file.FullName.Contains('\APIShared\')) {
        $source = [IO.File]::ReadAllText($file.FullName)
        $retiredTarget = '"(?:NoesisGUIUpdateChecksInGame|ButtonEnterCreateTroop|ButtonLeaveCreateTroop|ButtonTroopPanelMouseEnter|ButtonTroopPanelMouseLeave|ButtonUnitRechargeRock)"|nameof\(FatControler\.NoesisGUIUpdateChecksInGame\)|nameof\(HUD_Main\.UpdateRollover\)|nameof\(EngineInterface\.GameAction\)'
        if ($source -match $retiredTarget) {
            $errors.Add("$($file.FullName): shared managed interception target must use APIShared's publisher")
        }
    }
    $lines = [System.IO.File]::ReadAllLines($file.FullName)
    for ($index = 0; $index -lt $lines.Length; $index++) {
        $line = $lines[$index]
        if ($line -match '\.Hook\.(Enable|Disable)\s*\(' -or
            $line -match '\bCodePatch\.Write\s*\(' -or
            $line -match '\bGatehouseNativeMutation\b' -or
            $line -match '\bVirtualProtect\s*\(' -or
            $line -match '\bFlushInstructionCache\s*\(') {
            $errors.Add("$($file.FullName):$($index + 1): runtime executable-memory mutation: $($line.Trim())")
        }

        if ($line -notmatch '(classifierTransaction|transaction)\??\.Dispose\s*\(') { continue }
        $start = [Math]::Max(0, $index - 10)
        $context = [string]::Join("`n", $lines[$start..$index])
        $isInitializationRollback =
            $context -match '\bcatch\b' -or
            $context -match '!published' -or
            $context -match '!nativeInitialized' -or
            $context -match 'RollbackUnpublished' -or
            $context -match 'DisplacedByteCount' -or
            $context -match 'could not be installed'
        if (-not $isInitializationRollback) {
            $errors.Add("$($file.FullName):$($index + 1): published transaction teardown is not an initialization rollback")
        }
    }
}

$permanentManagedContracts = @(
    @{
        Path = 'BugfixesAndQoL\src\GatehouseTargetMarkerHeightHook.cs'
        Required = @('private static readonly Harmony harmony', 'harmony.Patch(target, prefix:', 'Volatile.Write(ref enabled', 'Volatile.Read(ref enabled)', 'Interlocked.Exchange(ref attempted, 1)')
        Forbidden = @('Unpatch(', 'UnpatchAll(', 'Dispose(', 'Undo(', 'CodePatch', 'VirtualProtect', 'Marshal.Write')
    },
    @{
        Path = 'APIShared\src\Economy\MarketPriceNativeRuntime.cs'
        Required = @('AllowedSchemes = DetourScheme.Indirect', 'FollowJumps = false', 'Validate(buy.Hook', 'Validate(sell.Hook', 'published = true', 'if (!published)', 'native.TrampolineAddress', 'jump.NearBranchTarget')
        Forbidden = @('transaction.Dispose(', 'transaction?.Dispose(', 'buy.Hook.Disable(', 'sell.Hook.Disable(')
    },
    @{
        Path = 'ExtraFeatures\src\AIMarketVanillaPriceHook.cs'
        Required = @('MarketPriceEvents.TryRegister(', 'MarketPriceEvents.CalculateTradeTotal(')
        Forbidden = @('AddDetour(', 'HookTransaction', 'NativeDetour', 'CalculateTradeTotal(price.BuyPrice')
    },
    @{
        Path = 'APIShared\src\Core\Events\ManagedInterceptionHook.cs'
        Required = @('ManualApply = true', 'GenerateTrampoline<T>()', 'candidate.Apply();', 'hook = candidate;', 'candidate?.Dispose();')
        Forbidden = @('public void Dispose()', 'hook.Dispose(', 'hook?.Dispose(', 'hook.Undo(', 'hook?.Undo(')
    },
    @{
        Path = 'APIShared\src\Units\Recruitment\RecruitmentMaterialUi.cs'
        Required = @('ManualApply = true', 'RecruitmentMaterialUiIlContract.Validate(method)', 'candidate.Apply();', 'hook = candidate;')
        Forbidden = @('public void Dispose()', 'hook.Dispose(', 'hook?.Dispose(', 'hook.Undo(', 'hook?.Undo(')
    },
    @{
        Path = 'BugfixesAndQoL\src\SiegeAmmoRestockFeature.cs'
        Required = @('if (eventsRegistered) return;', 'PresentationEvents.TryRegister(', 'args.Replacement = parameter')
        Forbidden = @('new Hook(', 'buttonHook', 'mouseEnterHook', 'mouseLeaveHook')
    },
    @{
        Path = 'UnitCosts\src\RecruitmentAvailabilityUiHook.cs'
        Required = @(
            'RecruitmentMaterialUi.TryRegister(',
            'PresentationEvents.TryRegister('
        )
        Forbidden = @(
            'materialUiHook?.Undo()',
            'materialUiHook?.Dispose()',
            'materialUiHook.Undo()',
            'materialUiHook.Dispose()',
            'public void Dispose()', 'new Hook(', 'new ILHook('
        )
    },
    @{
        Path = 'UnitCosts\src\UnitCostsRuntime.cs'
        Required = @(
            'private volatile int noWeaponsUiMask;',
            'IsNoWeaponsUiActive, RefreshRecruitmentUi',
            'noWeaponsUiMask = appliedNoWeaponsMask;'
        )
        Forbidden = @(
            'recruitmentAvailabilityUiHook = null',
            'recruitmentAvailabilityUiHook?.Dispose()',
            'recruitmentAvailabilityUiHook?.Undo()'
        )
    },
    @{
        Path = 'UnitLimit\src\UnitLimitRuntime.cs'
        Required = @(
            'InstallPermanentHooks();',
            'DeactivateEffects("ModeDisabled")',
            '() => IsEffectsActive',
            'Volatile.Write(ref effectsActive, 0)',
            'Volatile.Write(ref effectsActive, 1)'
        )
        Forbidden = @(
            'UnsubscribeHooks(',
            'makeTroopGameActionHook = null',
            'createTroopHoverHook = null',
            'siegeBuildHoverHook = null',
            'recruitmentAvailabilityUiHook = null',
            'public void Dispose()', 'new Hook(', 'new ILHook('
        )
    },
    @{
        Path = 'UnitLimit\src\UnitLimitRuntime.Settings.cs'
        Required = @('DeactivateEffects("SettingDisabled")')
        Forbidden = @('SubscribeHooks(', 'UnsubscribeHooks(')
    },
    @{
        Path = 'UnitLimit\src\MakeTroopGameActionHook.cs'
        Required = @('!isActive()', 'GameActionEvents.TryRegister(')
        Forbidden = @('public void Dispose()', 'new Hook(', 'new ILHook(', 'hook?.Undo()', 'hook?.Dispose()')
    },
    @{
        Path = 'UnitLimit\src\CreateTroopHoverHook.cs'
        Required = @('PresentationEvents.TryRegister(', 'args.OriginalCompleted')
        Forbidden = @('public void Dispose()', 'new Hook(', 'new ILHook(', 'enterHook?.Undo()', 'leaveHook?.Undo()')
    },
    @{
        Path = 'UnitLimit\src\SiegeBuildHoverHook.cs'
        Required = @('PresentationEvents.TryRegister(', 'args.OriginalCompleted')
        Forbidden = @('public void Dispose()', 'new Hook(', 'new ILHook(', 'enterHook?.Undo()', 'leaveHook?.Undo()')
    },
    @{
        Path = 'UnitLimit\src\RecruitmentAvailabilityUiHook.cs'
        Required = @('PresentationEvents.TryRegister(', 'args.OriginalCompleted')
        Forbidden = @('public void Dispose()', 'new Hook(', 'new ILHook(', 'hook?.Undo()', 'hook?.Dispose()')
    },
    @{
        Path = 'UnitLimit\src\UnitLimitIntegration.cs'
        Required = @('private static UnitLimitRuntime runtime;')
        Forbidden = @('runtime = null')
    },
    @{
        Path = 'BugfixesAndQoL\src\BugfixesAndQoLRuntime.cs'
        Required = @('ReconcilePermanentClientHook(')
        Forbidden = @(
            'DisposeFeature("market autotrade sell threshold"',
            'DisposeFeature("enemy-proximity bulldoze cursor"',
            'DisposeFeature("HD market view"',
            'DisposeFeature("camera movement modifier"',
            'DisposeFeature("Custom Trail starting-gold fix"'
        )
    },
    @{
        Path = 'BugfixesAndQoL\src\SingleBuildingPauseHook.cs'
        Required = @('!localHooksInstalled || !IsFeatureActive()')
        Forbidden = @('addChimpActionsHook.Apply()', 'addChimpActionsHook.Undo()')
    },
    @{
        Path = 'BugfixesAndQoL\src\QuarryPileRelocationRuntime.cs'
        Required = @('Keep the published', 'MonoMod hook')
        Forbidden = @('setUpInbuildingHook?.Undo()', 'setUpInbuildingHook?.Dispose()')
    },
    @{
        Path = 'ExtraFeatures\src\PlagueApothecarySearchRangePatch.cs'
        Required = @(
            'HookRva = 0x9F866',
            'HookDisplacedBytes = 14',
            'rootedPublishedInstance',
            'Interlocked.Exchange',
            'RollbackUnpublishedCandidate'
        )
        Forbidden = @(
            'BuildingDistanceComparisonRva = 0x9F86B',
            'AddContextHook(',
            'IDisposable'
        )
    },
    @{
        Path = 'ExtraFeatures\src\ExtraFeaturesRuntime.cs'
        Required = @(
            'PlagueApothecarySearchRangePatch.Install(',
            'ApplyPlagueApothecarySearchRangeSetting();'
        )
        Forbidden = @(
            'plagueApothecarySearchRangePatch?.Dispose()',
            'plagueApothecarySearchRangePatch = null'
        )
    },
    @{
        Path = 'BugfixesAndQoL\src\WaterboyTargetReservationRuntime.cs'
        Required = @(
            'private readonly HookTransaction transaction;',
            'private readonly DetourHandle<FindNearestBurningBuildingDelegate> targetSearchHook',
            'pendingTransaction?.Dispose();',
            'ValidateCommittedDetour(committedDetour, expectedTargetAddress);'
        )
        Forbidden = @(
            'transaction?.Dispose()',
            'transaction.Dispose()',
            'setUpInbuildingHook?.Dispose()',
            'setUpInbuildingHook.Dispose()',
            'pendingManagedHook',
            'TrySendModeChore',
            'targetSearchHook.Hook.Disable()',
            'targetSearchHook.Hook.Dispose()'
        )
    }
)

foreach ($contract in $permanentManagedContracts) {
    $path = Join-Path $workspace $contract.Path
    if (-not (Test-Path -LiteralPath $path)) {
        $errors.Add("$path`: permanent managed-hook contract source is missing")
        continue
    }

    $source = [System.IO.File]::ReadAllText($path)
    foreach ($required in $contract.Required) {
        if (-not $source.Contains($required)) {
            $errors.Add("$path`: permanent managed-hook contract is missing '$required'")
        }
    }
    foreach ($forbidden in $contract.Forbidden) {
        if ($source.Contains($forbidden)) {
            $errors.Add("$path`: published managed hook can be repatched through '$forbidden'")
        }
    }
}

$workshopPath = Join-Path $workspace 'BugfixesAndQoL\src\WorkshopIdleDelayHook.cs'
if (Test-Path -LiteralPath $workshopPath) {
    $workshop = [IO.File]::ReadAllText($workshopPath)
    if (-not $workshop.Contains('private readonly HookTransaction transaction;') -or
        -not $workshop.Contains('Volatile.Write(ref *(int*)enabledFlag') -or
        $workshop -match '(?:transaction|\.Hook)\??\.(?:Dispose|Undo|Enable|Disable)\s*\(' -or
        $workshop -match '\b(?:OnDestroy|OnDisable|OnApplicationQuit|StartCoroutine|Update|LateUpdate|FixedUpdate|VirtualProtect|CodePatch)\s*\(') {
        $errors.Add("$workshopPath`: workshop hook must remain published and use only its data gate")
    }
}
if ($errors.Count -ne 0) {
    $errors | ForEach-Object { Write-Error $_ }
    throw "Permanent native runtime regression check failed with $($errors.Count) finding(s)."
}

Write-Host "PASS: workspace runtime sources contain no unsafe executable-memory toggles, and audited live-setting MonoMod hooks remain permanently published."
