param([Parameter(Mandatory)][string]$AssemblyPath, [Parameter(Mandatory)][string]$GameDirectory)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Split-Path -Parent $AssemblyPath
$hashes=@()
foreach($state in @('Normal','Hover','Pressed')) {
 $path=Join-Path $root "Override/Assets/GUI/Sprites/LordOverviewTest_Crown$state.png"
 $image=[Drawing.Bitmap]::new($path)
 try {
  if($image.Width -ne 68 -or $image.Height -ne 68){throw "Wrong button dimensions: $state"}
  if($image.GetPixel(0,0).A -ge 255){throw "Button corner lost transparency: $state"}
  $colors=[Collections.Generic.HashSet[int]]::new()
  for($y=0;$y -lt $image.Height;$y++){for($x=0;$x -lt $image.Width;$x++){[void]$colors.Add($image.GetPixel($x,$y).ToArgb())}}
  if($colors.Count -lt 100){throw "Button artwork is empty or lost texture: $state"}
 } finally {$image.Dispose()}
 $hashes+=([BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash([IO.File]::ReadAllBytes($path))))
}
if(@($hashes | Select-Object -Unique).Count -ne 3){throw 'Normal, hover and pressed assets must remain distinct.'}
# Validate the compiled factory assigns all four Vanilla sprite slots; no game-native UI is created here.
[void][Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $GameDirectory 'BepInEx/core/Mono.Cecil.dll')))
$assembly=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($AssemblyPath)
try {
 $type=$assembly.MainModule.GetType('LordOverviewTest.OverviewAppearance')
 $method=$type.Methods | Where-Object Name -eq 'CreateButton'
 $calls=@($method.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference]} | ForEach-Object {$_.Operand.Name})
 foreach($name in @('SetSprite1','SetSprite2','SetSprite3','SetSprite4')){if($name -notin $calls){throw "Missing Vanilla sprite assignment: $name"}}
 if($assembly.MainModule.GetType('LordOverviewTest.OverviewCrownButton')){throw 'Obsolete custom crown-template path remains.'}
 # Guard the consumer's actual compiled opt-in. This does not prove Noesis rendering, but catches
 # the previous failure class where only an inherited Extender tooltip style owned the size.
 $runtime=$assembly.MainModule.GetType('LordOverviewTest.OverviewRuntime')
 # Compiled fast-path guardrail: the source cache must return before any lookup/allocation.
 # This is bounded IL inspection; visual/resource availability still needs game acceptance.
 $cache=$runtime.Methods | Where-Object Name -eq 'EnsureMetricIcons'
 $cacheCode=@($cache.Body.Instructions);$firstAllocation=-1;$firstReturn=-1
 for($i=0;$i -lt $cacheCode.Count;$i++){if($firstReturn -lt 0 -and $cacheCode[$i].OpCode.Name -eq 'ret'){$firstReturn=$i};if($firstAllocation -lt 0 -and $cacheCode[$i].OpCode.Name -eq 'newarr'){$firstAllocation=$i}}
 if($firstReturn -lt 0 -or $firstAllocation -le $firstReturn -or -not ($cacheCode[0..$firstReturn] | Where-Object {$_.Operand -is [Mono.Cecil.FieldReference] -and $_.Operand.Name -eq 'builtinIcons'})){throw 'Icon source initialization lost its cached early return.'}
 $render=$runtime.Methods | Where-Object Name -eq 'Render'
 if($render.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -in @('TryFindResource','GetIcon')}){throw 'Recurring renderer performs direct icon resource/provider lookups.'}
 # Cropping belongs exclusively to HUD cache initialization, never a warm icon factory/render.
 $icon=$runtime.Methods | Where-Object Name -eq 'MetricIcon'
 foreach($method in @($icon,$render)){
  if($method.Body.Instructions | Where-Object {$_.OpCode.Name -eq 'newobj' -and $_.Operand.DeclaringType.FullName -eq 'Noesis.CroppedBitmap'}){throw 'Icon crop escaped the HUD source cache.'}
 }
 foreach($property in @('set_Width','set_Height')){
  if(-not ($icon.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq $property -and $_.Previous.Operand -eq 28})){throw "Shared metric icon lost its 28-unit extent: $property"}
 }
 $tipFactory=$runtime.Methods | Where-Object Name -eq 'CreateTip'
 $sizingCalls=@($tipFactory.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.FullName -eq 'APIShared.ModSettings.SharedToolTipSizing'} | ForEach-Object {$_.Operand.Name})
 foreach($name in @('SetReferenceElement','SetReferenceFontSize','SetEnabled')){if($name -notin $sizingCalls){throw "Owned statistics tooltips lost their public sizing opt-in: $name"}}
 # The game consumes any Noesis visual hit before routed handlers. Check the compiled
 # creation boundaries, not only an unhandled Click event. Real Noesis hit tests remain in-game QA.
 foreach($pair in @(@('LordOverviewTest.OverviewRuntime','CreateTip'),@('LordOverviewTest.OverviewRuntime','CreatePassiveBubble'),@('LordOverviewTest.OverviewAppearance','PlayerText'),@('LordOverviewTest.OverviewAppearance','Portrait'))){
  $creator=$assembly.MainModule.GetType($pair[0]).Methods | Where-Object Name -eq $pair[1]
  $passive=@($creator.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq 'set_IsHitTestVisible' -and $_.Previous.OpCode.Name -eq 'ldc.i4.0'})
  if(-not $passive.Count){throw "Passive visual lost its hit-test exclusion: $($pair -join '.')"}
 }
 # Follow owned call targets (including dispatcher delegate bodies) from the presentation callback.
 # This protects the independent sampling boundary without trying to simulate Noesis or native game reads.
 $pending=[Collections.Generic.Stack[Mono.Cecil.MethodDefinition]]::new()
 $pending.Push(($runtime.Methods | Where-Object Name -eq 'OnHoverGui'))
 $seen=[Collections.Generic.HashSet[string]]::new()
 while($pending.Count){
  $method=$pending.Pop();if(-not $method.HasBody -or -not $seen.Add($method.FullName)){continue}
  foreach($instruction in $method.Body.Instructions){
   if($instruction.Operand -isnot [Mono.Cecil.MethodReference]){continue}
   $call=$instruction.Operand
   if(($call.DeclaringType.FullName -eq 'LordOverviewTest.OverviewData' -and $call.Name -eq 'Capture') -or ($call.DeclaringType.FullName -eq 'EngineInterface' -and $call.Name -in @('GetMeritData','GetMPScoreData','DLL_GetMeritData','DLL_GetMPScoreData'))){throw 'Presentation hover must not sample statistics.'}
   if($call -is [Mono.Cecil.MethodDefinition] -and $call.Module -eq $assembly.MainModule){$pending.Push($call)}
  }
 }
 # The regular GUI/tick paths must not allocate a closure before their due/visibility guard.
 # These are compiled IL allocation checks, not claims about native API/Noesis allocations.
 foreach($pair in @(@('LordOverviewTest.OverviewRuntime','OnTick'),@('LordOverviewTest.OverviewRuntime','Refresh'),@('LordOverviewTest.OverviewRuntime','OnOverviewGui'),@('LordOverviewTest.OverviewData','CopyRaw'),@('LordOverviewTest.OverviewData','CopyNative'))){
  $method=$assembly.MainModule.GetType($pair[0]).Methods | Where-Object Name -eq $pair[1]
  foreach($instruction in $method.Body.Instructions){
   if($instruction.OpCode.Name -in @('newarr','box') -or ($instruction.OpCode.Name -eq 'newobj' -and -not $instruction.Operand.DeclaringType.IsValueType)){throw "Managed allocation on critical/fast boundary: $($pair -join '.')"}
  }
 }
 $bindCrown=$runtime.Methods | Where-Object Name -eq 'BindCrownButton'
 foreach($name in @('SetInitialShowDelay','SetBetweenShowDelay')){
  if(-not ($bindCrown.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq $name -and $_.Previous.OpCode.Name -eq 'ldc.i4.0'})){throw "Crown delay must be zero: $name"}
 }
 $attach=$runtime.Methods | Where-Object Name -eq 'Attach'
 $buttonTemplates=@($attach.Body.Instructions | Where-Object {$_.OpCode.Name -eq 'ldstr' -and $_.Operand -match "<Style .*TargetType='\{x:Type Button\}'"} | ForEach-Object {$_.Operand})
 if($buttonTemplates.Count -ne 1 -or $buttonTemplates[0] -notmatch "Padding='\{TemplateBinding Padding\}'"){throw 'Header template must honor two-unit padding so a 28-unit icon fits a 32-unit reorder cell.'}
 $templates=@($attach.Body.Instructions | Where-Object {$_.OpCode.Name -eq 'ldstr' -and $_.Operand -match "TargetType='\{x:Type ToolTip\}'"} | ForEach-Object {$_.Operand})
 if($templates.Count -ne 1){throw 'Expected one explicit statistics popup style.'}
 [xml]$template=$templates[0]
 foreach($name in @('FontSize','MaxWidth','Padding','BorderThickness')){if($templates[0] -notmatch "\{TemplateBinding $name\}"){throw "Tooltip template does not honor its size owner: $name"}}
 $metrics=@($assembly.MainModule.GetType('LordOverviewTest.Metric').Fields | Where-Object IsLiteral | ForEach-Object Name)
 $settings=$assembly.MainModule.GetType('LordOverviewTest.OverviewSettings')
 foreach($name in @('EnableMod','AllowEnemyDetails','Columns','Design','LordGrid','ValueGrid')){
  $property=$settings.Properties | Where-Object Name -eq $name
  $scope=if($name -eq 'AllowEnemyDetails'){'SyncHostOnlyAttribute'}else{'PresetLocalAttribute'}
  if(-not $property -or -not ($property.CustomAttributes | Where-Object {$_.AttributeType.Name -eq $scope})){throw "Wrong settings ownership: $name"}
 }
} finally {$assembly.Dispose()}
$localeRoot=Join-Path $root 'Locales'
$reference=$null
foreach($file in Get-ChildItem -LiteralPath $localeRoot -Filter '*.txt'){
 $keys=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
 foreach($line in [IO.File]::ReadAllLines($file.FullName)){
  if(-not $line.Trim() -or $line.StartsWith('#')){continue}
  $pair=$line -split '=',2
  if($pair.Count -ne 2 -or -not $pair[1].Trim() -or -not $keys.Add($pair[0])){throw "Invalid/duplicate locale entry in $($file.Name): $line"}
 }
 foreach($metric in $metrics){if(-not $keys.Contains($metric)){throw "Missing metric label $metric in $($file.Name)"}}
 if($reference -and -not $reference.SetEquals($keys)){throw "Locale key parity failed: $($file.Name)"}
 $reference=$keys
}
[xml]$view=[IO.File]::ReadAllText((Join-Path $root 'Override/ScriptExtenderUI/LordOverviewTestSettings.xaml'))
foreach($control in $view.SelectNodes("//*[local-name()='Button' or local-name()='CheckBox' or local-name()='ComboBox' or local-name()='TextBox']")){
 if(-not $control.GetAttribute('ToolTip') -or $control.GetAttribute('ToolTipService.ShowDuration') -ne '60000'){throw 'Settings control lost its tooltip/duration.'}
}
# Verify the real unmodified game/SE boundary, not only a successful compile against a publicized assembly.
$gameAssembly=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDirectory 'Stronghold Crusader Definitive Edition_Data/Managed/Assembly-CSharp.dll'))
$extender=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameDirectory 'BepInEx/plugins/000shcdese/SHCDESE.dll'))
try {
 $engine=$gameAssembly.MainModule.GetType('EngineInterface')
 # This checks the actual game input policy, not just Noesis visual hit-test flags.
 $input=$gameAssembly.MainModule.GetType('FatControler').Methods | Where-Object Name -eq 'overNoesisGUI'
 $tags=@($input.Body.Instructions | Where-Object {$_.OpCode.Name -eq 'ldstr'} | ForEach-Object Operand)
 foreach($tag in @('Ignore','IgnoreSelf')){if($tag -notin $tags){throw "Vanilla world-input exemption changed: $tag"}}
 $inputCalls=@($input.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference]} | ForEach-Object {$_.Operand.Name})
 foreach($name in @('HitTest','get_Tag','get_Parent','GetParent')){if($name -notin $inputCalls){throw "Vanilla world-input ancestry contract changed: $name"}}
 $gameCheck=$gameAssembly.MainModule.GetType('FatControler').Methods | Where-Object Name -eq 'NoesisGUIUpdateChecksInGame'
 if(-not ($gameCheck.Body.Instructions | Where-Object {$_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq 'set_BookPopularityColour'})){throw 'Overall popularity colour producer changed.'}
 if(-not ($gameCheck.Body.Instructions | Where-Object {$_.OpCode.Name -eq 'ldc.i4.s' -and $_.Operand -eq 50})){throw 'Re-audit overall popularity colour threshold.'}
 $lock=$engine.Fields | Where-Object Name -eq 'threadLock'
 if(-not $lock.IsStatic -or $lock.IsPublic -or $lock.FieldType.FullName -ne 'System.Object'){throw 'Engine lock reflection contract changed.'}
 foreach($name in @('GetMeritData','GetMPScoreData','DLL_GetMeritData','DLL_GetMPScoreData')){if(-not ($engine.Methods | Where-Object {$_.Name -eq $name -and $_.IsPublic -and $_.IsStatic})){throw "Vanilla public snapshot unavailable: $name"}}
 $export=$gameAssembly.MainModule.GetType('EngineInterface/multiplayer_stats_export')
 if(-not $export.IsNestedPublic -or -not $export.IsExplicitLayout){throw 'Public native statistics export layout unavailable.'}
 foreach($name in @('valid','gold_acquired','max_population','food_produced','wood_produced','stone_produced','iron_produced','pitch_produced','weapons_produced','goods_received','goods_sent','troops_produced','who_killed_who','enemy_buildings_destroyed','buildings_lost','lords_killed')){
  $field=$export.Fields | Where-Object Name -eq $name
  $attribute=$field.CustomAttributes | Where-Object {$_.AttributeType.FullName -eq 'System.Runtime.CompilerServices.FixedBufferAttribute'}
  $count=if($name -eq 'who_killed_who'){81}else{9}
  if(-not $field.IsPublic -or -not $attribute -or $attribute.ConstructorArguments[0].Value.FullName -ne 'System.Int32' -or $attribute.ConstructorArguments[1].Value -ne $count){throw "Native export field changed: $name"}
 }
 $resources=$extender.MainModule.GetType('SHCDESE.Interop.GamePlayerResources')
 foreach($pair in @(@('r_CurrentPopularity','System.UInt32'),@('r_GoodBadThingBoost','System.Int32'))){
  $field=$resources.Fields | Where-Object Name -eq $pair[0]
  if(-not $field.IsPublic -or $field.FieldType.FullName -ne $pair[1]){throw "SE resource contract changed: $($pair[0])"}
 }
 if(-not ($resources.Fields | Where-Object {$_.Name -eq 'r_WinLossState' -and $_.IsPublic -and $_.FieldType.FullName -eq 'SHCDESE.Interop.Enums.WinLossState'})){throw 'Official eliminated-player status contract changed.'}
 $origin=$extender.MainModule.GetType('SHCDESE.API.GameXAMLManagerAPI').Properties | Where-Object Name -eq 'CurrentLobbyModSettingsChangeOrigin'
 if(-not $origin.GetMethod.IsPublic){throw 'Settings change-origin contract unavailable.'}
} finally {$gameAssembly.Dispose();$extender.Dispose()}
Write-Host 'PASS: packaged crown states, metric locales, settings/tooltips and real Vanilla/SE access contracts (actual rendering still requires in-game validation).'
