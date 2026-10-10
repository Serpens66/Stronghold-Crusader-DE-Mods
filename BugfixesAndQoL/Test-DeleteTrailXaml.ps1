#requires -Version 7.0
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$workspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$engine = Join-Path $workspace 'shcde-script-extender\src\SHCDESE.BepInEx\API\Components\Noesis\XAML'
Add-Type -Path @(
    (Join-Path $engine 'XamlPatcher.cs'),
    (Join-Path $engine 'XmlPatchOperation.cs'),
    (Join-Path $engine 'XmlPatchType.cs'),
    (Join-Path $PSScriptRoot 'tests\TrailDeletion.Tests\XamlBridge.cs')) -CompilerOptions '/nowarn:8632'
$current = Get-Content -Raw -LiteralPath (Join-Path $workspace '_inspect\CrusaderDE-Native-Baseline\CURRENT.json') | ConvertFrom-Json
$baseline = Join-Path $workspace ('_inspect\CrusaderDE-Native-Baseline\' + $current.semanticDirectory)
$original = [IO.File]::ReadAllText((Join-Path $baseline 'resources\xaml\Assets\GUI\XAMLResources\FRONT_ManageTrail.xaml'))
$mine = Join-Path $PSScriptRoot 'Patches\Assets\GUI\XAMLResources\FRONT_ManageTrail.xaml'
$extended = Join-Path $workspace 'ExtendedData\Patches\Assets\GUI\XAMLResources\FRONT_ManageTrail.xaml'
function Apply-Patch([string]$source, [string]$path) {
    [xml]$patch = [IO.File]::ReadAllText($path)
    $operations = [Collections.Generic.List[SHCDESE.API.Components.Noesis.XAML.XmlPatchOperation]]::new()
    foreach ($node in $patch.Patch.Operation) {
        $operation = [SHCDESE.API.Components.Noesis.XAML.XmlPatchOperation]::new()
        $operation.Type = [Enum]::Parse([SHCDESE.API.Components.Noesis.XAML.XmlPatchType], [string]$node.Type)
        $operation.XPath = [string]$node.XPath
        $operation.AttributeName = [string]$node.AttributeName
        $operation.Value = [string]$node.Value
        if ($node.Content) {
            $children = @($node.Content.ChildNodes | Where-Object NodeType -EQ Element)
            if ($children.Count -ne 1) { throw 'Content must have exactly one element.' }
            $operation.Content = $children[0].OuterXml
        }
        $operations.Add($operation)
    }
    return [TrailXamlBridge]::Apply($source, $operations)
}
foreach ($order in @(@($mine), @($mine, $extended), @($extended, $mine))) {
    $result = $original
    foreach ($path in $order) { $result = Apply-Patch $result $path }
    [xml]$document = $result
    $ns = [Xml.XmlNamespaceManager]::new($document.NameTable)
    $ns.AddNamespace('n', $document.DocumentElement.NamespaceURI)
    $ns.AddNamespace('x', 'http://schemas.microsoft.com/winfx/2006/xaml')
    $ns.AddNamespace('bugfixes', 'clr-namespace:BugfixesAndQoL;assembly=BugfixesAndQoL')
    foreach ($query in @(
        '//n:Grid[@x:Name="BugfixesAndQoLDeleteTrailHost"]/n:Button[@Command="{Binding DeleteTrailCommand}"]',
        '//n:ListView[@Name="ImportList"][@bugfixes:TrailDeletionListBehavior.IsEnabled="True"]',
        '//n:CheckBox[@x:Name="ImportBackup"][@IsChecked="True"]',
        '//n:Button[@Name="ImportImportButton"][@CommandParameter="DoImport"]')) {
        if ($document.SelectNodes($query, $ns).Count -ne 1) { throw "Missing or duplicate UI contract: $query" }
    }
    Write-Output ('Trail XAML passed with ' + $order.Count + ' patch set(s).')
}
