$ErrorActionPreference = 'Stop'
$gameDirectory = 'E:\ProgrammeE\Steam\steamapps\common\Stronghold Crusader Definitive Edition'
# Metadata-only reader; loading its bytes avoids mutating Windows Zone.Identifier on game files.
[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $gameDirectory 'BepInEx\core\Mono.Cecil.dll')))
# Optional reflection contracts must match real access modifiers, not just the fixture signatures.
foreach ($providerFile in Get-ChildItem -LiteralPath (Join-Path $gameDirectory 'BepInEx\plugins') -Recurse -Filter 'ExtraFeatures.dll' -File) {
    $provider = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($providerFile.FullName)
    try {
        $gate = $provider.MainModule.GetType('Shared.GameplayModActivationGate')
        $methods = @($gate.Methods | Where-Object { $_.Name -eq 'IsEnabled' -and $_.IsStatic -and $_.IsAssembly -and
            $_.ReturnType.FullName -eq 'System.Boolean' -and $_.Parameters.Count -eq 1 -and $_.Parameters[0].ParameterType.FullName -eq 'System.Boolean' })
        if ($methods.Count -ne 1) { throw 'Installed ExtraFeatures internal activation contract changed.' }
        Write-Host 'PASS: installed ExtraFeatures activation gate is internal static bool IsEnabled(bool).'
    } finally { $provider.Dispose() }
}
$managed = Join-Path $gameDirectory 'Stronghold Crusader Definitive Edition_Data\Managed\Assembly-CSharp.dll'
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($managed)
try {
    foreach ($entry in @(
        @('CrusaderDE.MainViewModel','viewModelLoaded','field'),
        @('CrusaderDE.MainViewModel','buildScreenID','field'),
        @('CrusaderDE.MainViewModel','get_MEMode','method'),
        @('CrusaderDE.HUD_Main','NewBuildScreenBlank','method'),
        @('CrusaderDE.MainViewModel','get_AlliesPanelVisible','method'),
        @('CrusaderDE.MainViewModel','get_MeritPanelVisible','method'),
        @('CrusaderDE.MainViewModel','get_MPChatVisible','method'),
        @('CrusaderDE.MainViewModel','getAIFace','method'),
        @('CrusaderDE.MainViewModel','getAlliesGoodsIcon','method'),
        @('CrusaderDE.MainViewModel','SetNoesisKeyboardState','method'),
        @('FatControler','get_NoesisHasKeyboard','method'),
        @('FatControler','SetNoesisKeyboardState','method'),
        @('CrusaderDE.Translate','lookUpText','method'),
        @('Platform_Multiplayer','getSkirmishName','method'),
        @('OnScreenText','getComputerName','method'),
        @('CrusaderDE.MainViewModel','setUpInbuilding','method'),
        @('CrusaderDE.MainViewModel','InBuildingGameAction','method'),
        @('CrusaderDE.MainViewModel','TroopsSelectedGameAction','method'),
        @('CrusaderDE.MainViewModel','DefaultGameUIGameAction','method'),
        @('CrusaderDE.MainViewModel','DefaultMapEditorUIGameAction','method'),
        @('GameData','setGameState','method'),
        @('CrusaderDE.PropEx','Sprite1Property','field'),
        @('CrusaderDE.PropEx','Sprite2Property','field'),
        @('CrusaderDE.HUD_Buildings','RefHUDBuildingFullClickMask','field')
    )) {
        $type = $assembly.MainModule.GetType($entry[0])
        if (-not $type -or -not $type.IsPublic) { throw "Missing public game type: $($entry[0])" }
        $members = if ($entry[2] -eq 'field') { @($type.Fields) } else { @($type.Methods) }
        $member = @($members | Where-Object { $_.Name -eq $entry[1] -and $_.IsPublic })
        if ($member.Count -eq 0) { throw "Missing public game member: $($entry -join '.')" }
        $member | ForEach-Object { Write-Host ('Validated public member: ' + $_.FullName) }
    }
    $monitor = @($assembly.MainModule.GetType('EngineInterface').Fields | Where-Object Name -eq 'threadLock')
    if ($monitor.Count -ne 1 -or -not $monitor[0].IsStatic -or -not $monitor[0].IsPrivate -or $monitor[0].FieldType.FullName -ne 'System.Object') { throw 'Vanilla monitor reflection contract changed.' }
    Write-Host 'Validated private static object EngineInterface.threadLock (encapsulated reflection).'
    $nameResolver = @($assembly.MainModule.GetType('OnScreenText').Methods | Where-Object { $_.Name -eq 'getComputerName' -and $_.IsPublic -and $_.IsStatic -and $_.Parameters.Count -eq 2 -and $_.ReturnType.FullName -eq 'System.String' })
    if ($nameResolver.Count -ne 1 -or @($nameResolver[0].Parameters | Where-Object { $_.ParameterType.FullName -ne 'System.Int32' }).Count) { throw 'Computer-name resolver contract changed.' }
    $state = $assembly.MainModule.GetType('EngineInterface/PlayState')
    foreach ($name in @('computer_names','computer_register','skirmish_order','skirmish_order_player','skirmish_order_from_player')) {
        if (-not @($state.Fields | Where-Object { $_.Name -eq $name -and $_.IsPublic }).Count) { throw "Missing public snapshot field: $name" }
    }
} finally { $assembly.Dispose() }
$noesis = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $gameDirectory 'Stronghold Crusader Definitive Edition_Data\Managed\Noesis.NoesisGUI.dll'))
try {
    foreach ($entry in @(@('Noesis.UIElement','PreviewTextInput'),@('Noesis.UIElement','LostFocus'),@('Noesis.UIElement','GotKeyboardFocus'),@('Noesis.UIElement','LostKeyboardFocus'),@('Noesis.UIElement','IsVisibleChanged'),@('Noesis.TextBoxBase','TextChanged'),@('Noesis.TextBoxBase','SelectionChanged'))) {
        $type = $noesis.MainModule.GetType($entry[0])
        if (-not @($type.Events | Where-Object { $_.Name -eq $entry[1] -and $_.AddMethod.IsPublic -and $_.RemoveMethod.IsPublic }).Count) { throw "Missing public input event: $($entry -join '.')" }
    }
    foreach ($name in @('SelectionStart','SelectionLength')) {
        if (-not @($noesis.MainModule.GetType('Noesis.TextBox').Properties | Where-Object { $_.Name -eq $name -and $_.GetMethod.IsPublic -and $_.SetMethod.IsPublic }).Count) { throw "Missing public selection property: $name" }
    }
    $binding = $noesis.MainModule.GetType('Noesis.Binding')
    if (-not @($binding.Properties | Where-Object { $_.Name -eq 'Source' -and $_.GetMethod.IsPublic -and $_.SetMethod.IsPublic }).Count -or
        -not @($binding.Methods | Where-Object { $_.IsConstructor -and $_.IsPublic -and $_.Parameters.Count -eq 1 -and $_.Parameters[0].ParameterType.FullName -eq 'System.String' }).Count) { throw 'Missing public explicit-source Binding contract.' }
    Write-Host 'PASS: Noesis input, paste rollback, focus and explicit-source binding contracts.'
    foreach ($entry in @(@('Noesis.BindingOperations','GetBindingBase'),@('Noesis.BindingOperations','SetBinding'),@('Noesis.DependencyObject','ReadLocalValue'),@('Noesis.Keyboard','ClearFocus'),@('Noesis.Keyboard','get_FocusedElement'),@('Noesis.VisualTreeHelper','GetChild'),@('Noesis.VisualTreeHelper','GetChildrenCount'),@('Noesis.Visual','PointToScreen'),@('Noesis.Grid','SetRow'),@('Noesis.Grid','SetColumn'),@('Noesis.FrameworkElement','add_Loaded'),@('Noesis.FrameworkElement','add_SizeChanged'),@('Noesis.ToolTip','add_Opened'),@('Noesis.ToolTip','add_Closed'),@('Noesis.ToolTip','get_PlacementTarget'))) {
        if (-not @($noesis.MainModule.GetType($entry[0]).Methods | Where-Object { $_.Name -eq $entry[1] -and $_.IsPublic }).Count) { throw "Missing public toolbar/focus member: $($entry -join '.')" }
    }
} finally { $noesis.Dispose() }
$extender = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $gameDirectory 'BepInEx\plugins\000shcdese\SHCDESE.dll'))
try {
    $player = $extender.MainModule.GetType('SHCDESE.API.GamePlayerManagerAPI')
    foreach ($name in @('SubtractIncomingGood','RemoveGood','GetCurrentGameTypeMode','AddIncomingGood','AddPlayerGold','GetPlayerGold','TryGetPlayerResourcesById','SetLocalPlayerExtremePowersEnabled','IsLocalPlayerExtremePowersEnabled','GetLordUnitId','GetLordUnitGlobalId')) {
        $method = @($player.Methods | Where-Object { $_.Name -eq $name -and $_.IsPublic })
        if ($method.Count -ne 1) { throw "Missing or ambiguous Extender player API: $name" }
        Write-Host ('Validated Extender API: ' + $method[0].FullName)
    }
    $power = $extender.MainModule.GetType('SHCDESE.Interop.Enums.ExtremePower')
    $powerNames = @('ArrowVolley','PartialHeal','SpawnSpearmen','SpawnEngineers','SpawnMacemen','GrantGold','SpawnKnights','RockVolley')
    for ($id=0; $id -lt 8; $id++) {
        $field = @($power.Fields | Where-Object Name -eq $powerNames[$id])
        if ($field.Count -ne 1 -or $field[0].Constant -ne $id) { throw 'Power enum ordering changed.' }
    }
    foreach ($entry in @(
        @('SHCDESE.Interop.GamePlayerResources','r_ExtremePowersMana'),
        @('SHCDESE.Interop.GamePlayerResources','r_IsPaused'),
        @('SHCDESE.Interop.GamePlayerResources','r_KeepTilePositionX'),
        @('SHCDESE.Interop.GamePlayerResources','r_KeepTilePositionY'),
        @('SHCDESE.Interop.GameUnit','r_CurrentTilePositionX'),
        @('SHCDESE.Interop.GameUnit','r_CurrentTilePositionY'),
        @('SHCDESE.Interop.GameBuilding','N00008642_2'),
        @('SHCDESE.Interop.GameBuilding','N0000182B'),
        @('SHCDESE.Interop.GameBuilding','r_GateState'),
        @('SHCDESE.Interop.GameBuilding','r_GateState2')
    )) {
        if (-not @($extender.MainModule.GetType($entry[0]).Fields | Where-Object { $_.Name -eq $entry[1] -and $_.IsPublic }).Count) { throw "Missing action field: $($entry -join '.')" }
    }
    foreach ($name in @('IsTileInsideMapBounds','IsValidTileId','GetTileId','GetTileVectorFromId','GetTileBuildingId','GetTilePropertyFlag','GetPathConnectionLayer')) {
        if (-not @($extender.MainModule.GetType('SHCDESE.API.GameTileManagerAPI').Methods | Where-Object { $_.Name -eq $name -and $_.IsPublic }).Count) { throw "Missing tile API: $name" }
    }
    $goods = $extender.MainModule.GetType('SHCDESE.Interop.eGoods')
    $symbols = @('STORED_WOOD_PLANKS','STORED_RAW_HOPS','STORED_STONE_BLOCKS','STORED_IRON_INGOTS','STORED_PITCH_RAW','STORED_RAW_WHEAT','STORED_FOOD_BREAD','STORED_FOOD_CHEESE','STORED_FOOD_MEAT','STORED_FOOD_FRUIT','STORED_FOOD_ALE','STORED_GOLD','STORED_FLOUR','STORED_BOWS','STORED_CROSSBOWS','STORED_SPEARS','STORED_PIKES','STORED_MACES','STORED_SWORDS','STORED_LEATHER_ARMOUR','STORED_METAL_ARMOUR')
    $values = @(2,3,4,6,7,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24)
    for ($index=0; $index -lt $symbols.Count; $index++) {
        $field = @($goods.Fields | Where-Object Name -eq $symbols[$index])
        if ($field.Count -ne 1 -or $field[0].Constant -ne $values[$index]) { throw "Vanilla goods-button ABI mismatch: $($symbols[$index])" }
    }
    foreach ($eventName in @('PlayerAIEvaluateAttackOrder','PlayerAIEvaluateDefendOrder')) {
        $type = $extender.MainModule.GetType('SHCDESE.EventAPI.Player.' + $eventName)
        foreach ($property in @('TargetPlayerId','SourcePlayerId','ReturnValue')) {
            if (-not @($type.Properties | Where-Object { $_.Name -eq $property -and $_.GetMethod.IsPublic -and $_.SetMethod.IsPublic }).Count) { throw "Missing AI acceptance event member: $eventName.$property" }
        }
    }
    Write-Host 'PASS: installed game visibility, Extender APIs, events and all Vanilla goods-button IDs.'
} finally { $extender.Dispose() }
