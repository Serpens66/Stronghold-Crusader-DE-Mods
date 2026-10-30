[CmdletBinding()]
param([string]$ModDirectory)

$ErrorActionPreference = 'Stop'
Write-Host '[Preflight] Checking permanent published hooks and executable-memory mutations...'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..\..'))
$roots = @('.')
if ($ModDirectory) {
    $modRoot = [IO.Path]::GetFullPath((Join-Path $workspace $ModDirectory))
    if (-not $modRoot.StartsWith($workspace + '\', [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $modRoot -PathType Container)) { throw 'Invalid mod scope.' }
    $roots = @($ModDirectory)
}
$excludedSegments = @(
    '\.git\', '\bin\', '\obj\', '\tests\', '\.inspect\', '\_inspect\',
    '\.work\', '\.release-output\', '\BepInEx\plugins\', '\packages\', '\shcde-script-extender\'
)
$files = foreach ($relativeRoot in $roots) {
    $root = Join-Path $workspace $relativeRoot
    if (-not (Test-Path -LiteralPath $root)) { continue }
    Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.cs' -ErrorAction SilentlyContinue | Where-Object {
        $path = '\' + $_.FullName.Substring($workspace.Length).TrimStart('\')
        (-not ($excludedSegments | Where-Object { $path.Contains($_) })) -and
        (-not $path.Contains('\Testmods\') -or $path.Contains('\Testmods\MoatMove\'))
    }
}

if ($ModDirectory) {
    # Assembly references do not authorize auditing an unchanged provider's entire source tree.
    # Include the mod's actual source links, which are compiled into this consumer.
    $linked = @(foreach ($project in Get-ChildItem -LiteralPath $modRoot -Filter '*.csproj' -File) {
        [xml]$projectXml = [IO.File]::ReadAllText($project.FullName)
        foreach ($entry in $projectXml.SelectNodes('//*[local-name()="Compile"][@Include]')) {
            if ([string]$entry.Include -match '\$\(|[*?]') { continue }
            $target = [IO.Path]::GetFullPath((Join-Path $project.Directory.FullName ([string]$entry.Include)))
            if ([IO.Path]::GetExtension($target) -ieq '.cs' -and (Test-Path -LiteralPath $target -PathType Leaf)) { Get-Item -LiteralPath $target }
        }
    })
    $files = @(@($files) + $linked | Sort-Object FullName -Unique)
    Write-Host "[Preflight] Permanent-hook scope: $ModDirectory and $($files.Count) runtime source files including explicit links."
}
$selectedFiles = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($file in $files) { [void]$selectedFiles.Add($file.FullName) }
$errors = [System.Collections.Generic.List[string]]::new()
$wheatMemory = Join-Path $workspace 'APIShared\src\Compatibility\FixesWheatMemory.cs'
$spectatorGateway = Join-Path $workspace 'Spectator\src\NativePowerContext.cs'
if ((-not $ModDirectory -or $selectedFiles.Contains($wheatMemory)) -and (Test-Path -LiteralPath $wheatMemory)) {
    # APIShared owns the expressly approved startup-only exception and its checks.
    & (Join-Path $workspace 'APIShared\tools\Validation\Test-FixesWheatBoundary.ps1')
    if (-not $?) { throw 'APIShared wheat startup boundary verification failed.' }
}

# Returns the first/last line of the brace block that starts at the only line containing $Signature.
function Get-SourceBlock([string[]]$Lines, [string]$Signature) {
    $starts = @(for ($i = 0; $i -lt $Lines.Length; $i++) { if ($Lines[$i].Contains($Signature)) { $i } })
    if ($starts.Count -ne 1) { return $null }
    $depth = 0; $opened = $false
    for ($i = $starts[0]; $i -lt $Lines.Length; $i++) {
        foreach ($character in $Lines[$i].ToCharArray()) {
            if ($character -eq '{') { $depth++; $opened = $true } elseif ($character -eq '}') { $depth-- }
        }
        if ($opened -and $depth -eq 0) { return [pscustomobject]@{ Start = $starts[0]; End = $i } }
    }
    return $null
}

# Fresh private stub pages: written once while unreachable, then made executable (RW -> RX)
# before any hook can reach them, and never freed or rewritten after publication. These are
# not rewrites of live code. Each exception covers only VirtualProtect/FlushInstructionCache
# declarations and calls inside the named blocks of one file, and only while its structural
# publication contract holds; anything else stays a normal finding.
function Get-FreshPageAllowance([string]$Path, [string[]]$Lines) {
    $text = [string]::Join("`n", $Lines)
    if ($Path -eq (Join-Path $workspace 'APIShared\src\Projectiles\PlagueNativeCode.cs')) {
        # Mirrors APIShared's own audited SourceModel guardrail (tools/SourceContracts): changes
        # only in the constructor, VirtualFree only for an unpublished helper.
        $constructor = Get-SourceBlock $Lines 'internal PlagueNativeCode(Action<Assembler> generate)'
        $rollback = Get-SourceBlock $Lines 'internal void RollbackUnpublished()'
        if (-not $constructor -or -not $rollback) { return $null }
        $rollbackText = [string]::Join("`n", $Lines[$rollback.Start..$rollback.End])
        $guard = $rollbackText.IndexOf('if (published) throw')
        if ($guard -lt 0 -or $rollbackText.IndexOf('VirtualFree(') -lt $guard -or
            [regex]::Matches($text, '\bVirtualFree\s*\(').Count -ne 2 -or
            $text -notmatch 'VirtualAlloc\(IntPtr\.Zero, \(UIntPtr\)4096, 0x3000, 0x04\)') { return $null }
        return @($constructor)
    }
    if ($Path -eq (Join-Path $workspace 'Testmods\AICoarsePathComponentFixTest\src\WoodSiteGuardExperiment.cs')) {
        # Opt-in experiment: an 8-byte branch site that no RedBird hook type can replace. The
        # live site is written once; only an unpublished candidate is restored.
        $constructor = Get-SourceBlock $Lines 'internal WoodSiteGuardExperiment('
        $writer = Get-SourceBlock $Lines 'private static void WriteInitialPatch(IntPtr site, byte[] bytes)'
        if (-not $constructor -or -not $writer) { return $null }
        $calls = @(for ($i = 0; $i -lt $Lines.Length; $i++) {
            if (($i -lt $writer.Start -or $i -gt $writer.End) -and $Lines[$i] -match '\bWriteInitialPatch\s*\(') { $i }
        })
        if ($calls.Count -ne 2 -or $Lines[$calls[0]].Trim() -cne 'WriteInitialPatch(site, jump);' -or
            -not $Lines[$calls[1]].Trim().StartsWith('WriteInitialPatch(site, Original);', [StringComparison]::Ordinal) -or
            -not (@($Lines[[Math]::Max(0, $calls[1] - 4)..$calls[1]]) -match '^\s*if \(patchAttempted && !published\)$') -or
            $calls[0] -lt $constructor.Start -or $calls[1] -gt $constructor.End -or
            $text -match 'class WoodSiteGuardExperiment\s*:|\.(?:Undo|Disable|Uninstall)\s*\(' -or
            $text -notmatch 'VirtualAlloc\(new IntPtr\(address\), new UIntPtr\(\(uint\)StubCapacity\), 0x3000, 0x04\)') { return $null }
        # Constructor calls may only target the fresh candidate stub.
        foreach ($i in $constructor.Start..$constructor.End) {
            if ($Lines[$i] -match '\b(?:VirtualProtect|FlushInstructionCache)\s*\(' -and $Lines[$i] -notmatch '\(\s*(?:GetCurrentProcess\(\),\s*)?candidate,') { return $null }
        }
        return @($constructor, $writer)
    }
    return $null
}

foreach ($file in $files) {
    if (-not $file.FullName.Contains('\APIShared\')) {
        $source = [IO.File]::ReadAllText($file.FullName)
        $retiredTarget = '"(?:NoesisGUIUpdateChecksInGame|ButtonEnterCreateTroop|ButtonLeaveCreateTroop|ButtonTroopPanelMouseEnter|ButtonTroopPanelMouseLeave|ButtonUnitRechargeRock)"|nameof\(FatControler\.NoesisGUIUpdateChecksInGame\)|nameof\(HUD_Main\.UpdateRollover\)|nameof\(EngineInterface\.GameAction\)'
        if ($source -match $retiredTarget) {
            $errors.Add("$($file.FullName): shared managed interception target must use APIShared's publisher")
        }
    }
    $lines = [System.IO.File]::ReadAllLines($file.FullName)
    $freshPageBlocks = @(Get-FreshPageAllowance $file.FullName $lines | Where-Object { $_ })
    for ($index = 0; $index -lt $lines.Length; $index++) {
        $line = $lines[$index]
        if ($line -match '\.Hook\.(Enable|Disable)\s*\(' -or
            $line -match '\bCodePatch\.Write\s*\(' -or
            $line -match '\bGatehouseNativeMutation\b' -or
            $line -match '\bVirtualProtect\s*\(' -or
            $line -match '\bFlushInstructionCache\s*\(') {
            # A fresh, unpublished process-lived selection stub is not a rewrite of live code.
            # The narrow exception covers only its publication flush, never another address/call.
            $freshSpectatorStub = $file.FullName -eq $spectatorGateway -and
                ($line.Trim() -ceq 'if (!FlushInstructionCache(GetCurrentProcess(), gateway, (UIntPtr)code.Length))' -or
                 $line.Trim() -ceq '[DllImport("kernel32", SetLastError = true)] private static extern bool FlushInstructionCache(IntPtr process, IntPtr address, UIntPtr length);') -and
                $source.Contains('IntPtr gateway = NativeMemoryManager.AllocateStub(image + 0x105548, 128);') -and
                $source.Contains('Marshal.Copy(code, 0, gateway, code.Length);') -and
                $source.Contains('Roots.Add(this);') -and
                $source -notmatch 'VirtualFree|FreeStub|\.Undo\(|transaction\??\.Dispose\('
            $freshPage = $freshPageBlocks.Count -gt 0 -and $line -notmatch '\bCodePatch\.|\.Hook\.|GatehouseNativeMutation' -and
                ($line -match '^\s*(?:\[DllImport\([^\]]*\)\]\s*)?private static extern bool (?:VirtualProtect|FlushInstructionCache)\(' -or
                 @($freshPageBlocks | Where-Object { $index -ge $_.Start -and $index -le $_.End }).Count -gt 0)
            if ($file.FullName -ne $wheatMemory -and -not $freshSpectatorStub -and -not $freshPage) {
                $errors.Add("$($file.FullName):$($index + 1): runtime executable-memory mutation: $($line.Trim())")
            }
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
        # Backend/relocation checks express this audited native contract. Publication
        # failure behavior belongs to APIShared's executable NativeHookPublicationTests;
        # do not require a particular helper name or spelling of the state assignment.
        # The relocation checks moved into APIShared's shared Indirect-entry validator (next entry).
        Required = @('AllowedSchemes = DetourScheme.Indirect', 'FollowJumps = false', 'Validate(buy.Hook', 'Validate(sell.Hook', 'NativeSites.NativeIndirectEntry.Validate(hook, target, expected)')
        Forbidden = @('transaction.Dispose(', 'transaction?.Dispose(', 'buy.Hook.Disable(', 'sell.Hook.Disable(')
    },
    @{
        Path = 'APIShared\src\Core\NativeSites\NativeIndirectEntry.cs'
        Required = @('native.Scheme != DetourScheme.Indirect', 'native.TrampolineAddress', 'jump.NearBranchTarget')
        Forbidden = @('.Disable(', '.Undo(')
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
        Path = 'APIShared\src\Presentation\TroopActions\TroopActionHud.cs'
        Required = @('GenerateTrampoline<SetupTroopActionsDelegate>()', 'setupHook = candidate;', 'candidate?.Dispose();')
        Forbidden = @('public void Dispose()', 'setupHook.Dispose(', 'setupHook?.Dispose(', 'setupHook.Undo(', 'setupHook?.Undo(')
    },
    @{
        Path = 'APIShared\src\Units\Recruitment\RecruitmentAvailability.cs'
        Required = @('PresentationEvents.TryRegisterWhenActive(PresentationOperation.GuiChecks', 'installed = true;')
        Forbidden = @('public void Dispose()', 'new Hook(', 'new ILHook(', 'installed = false')
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
            'RecruitmentAvailability.TryRegister(',
            'Volatile.Read(ref published) != 0',
            'Volatile.Write(ref published, 1)'
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
            'IsNoWeaponsUiActive, IsRecruitmentAvailabilityActive, IsRecruitmentBlockedByExtraCosts',
            'noWeaponsUiMask = appliedNoWeaponsMask;',
            # Tooltip ownership moved from the UI adapter into the central presentation service.
            # These are source guardrails for permanent registration and logical activation, not runtime proofs.
            'TooltipPresentation.TryRegisterContent(',
            'context.IsActive',
            'TooltipPlacement.InlineAfterCosts'
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
        Path = 'UnitLimit\src\UnitLimitRuntime.Tooltips.cs'
        Required = @('TooltipPresentation.TryRegisterContent(', 'context.IsActive', 'TooltipPlacement.BeforeMainLine')
        Forbidden = @('public void Dispose()', 'new Hook(', 'new ILHook(')
    },
    @{
        Path = 'UnitLimit\src\RecruitmentAvailabilityUiHook.cs'
        Required = @('PresentationEvents.TryRegister(', 'args.OriginalCompleted', 'RecruitmentAvailability.TryRegister(')
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
    if ($ModDirectory -and -not $selectedFiles.Contains($path)) { continue }
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
if ((-not $ModDirectory -or $selectedFiles.Contains($workshopPath)) -and (Test-Path -LiteralPath $workshopPath)) {
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

Write-Host "PASS: scoped source guardrails. This is not a runtime publication proof; APIShared NativeHookPublicationTests checks failure behavior."
