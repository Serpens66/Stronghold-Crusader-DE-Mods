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
} finally {$assembly.Dispose()}
Write-Host 'PASS: packaged crown PNGs, alpha corners, three distinct states and compiled Vanilla sprite assignments (actual rendering still requires in-game validation).'
