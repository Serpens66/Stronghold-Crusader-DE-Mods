param([string]$Config, [string]$Manifest, [Parameter(Mandatory)][string]$Output, [string]$Framework = 'true')
$ErrorActionPreference = 'Stop'
[void][IO.Directory]::CreateDirectory($Output)
function Save-Xml($Document, [string]$Path) {
    $settings = [Xml.XmlWriterSettings]::new()
    $settings.Indent = $true; $settings.NewLineChars = "`r`n"; $settings.Encoding = [Text.UTF8Encoding]::new($false)
    $writer = [Xml.XmlWriter]::Create($Path, $settings)
    try { $Document.Save($writer) } finally { $writer.Dispose() }
}
if ($Framework -eq 'true') {
    $xml = [Xml.XmlDocument]::new()
    if ($Config -and [IO.File]::Exists($Config)) { $xml.Load($Config) } else { $xml.LoadXml('<configuration/>') }
    $runtime = $xml.SelectSingleNode('/configuration/runtime')
    if (-not $runtime) { $runtime = $xml.DocumentElement.AppendChild($xml.CreateElement('runtime')) }
    $switches = $runtime.SelectSingleNode('AppContextSwitchOverrides')
    if (-not $switches) { $switches = $runtime.AppendChild($xml.CreateElement('AppContextSwitchOverrides')) }
    $values = @($switches.GetAttribute('value') -split ';' | Where-Object { $_ -and $_ -notmatch '^\s*Switch\.System\.IO\.(UseLegacyPathHandling|BlockLongPaths)\s*=' })
    $switches.SetAttribute('value', (($values + @('Switch.System.IO.UseLegacyPathHandling=false','Switch.System.IO.BlockLongPaths=false')) -join ';'))
    Save-Xml $xml (Join-Path $Output 'app.config')
}
$xml = [Xml.XmlDocument]::new()
if ($Manifest -and [IO.File]::Exists($Manifest)) { $xml.Load($Manifest) }
else { $xml.LoadXml('<assembly xmlns="urn:schemas-microsoft-com:asm.v1" manifestVersion="1.0"><assemblyIdentity version="1.0.0.0" name="Workspace.Tool"/></assembly>') }
$ns = [Xml.XmlNamespaceManager]::new($xml.NameTable)
$ns.AddNamespace('a','urn:schemas-microsoft-com:asm.v3'); $ns.AddNamespace('w','http://schemas.microsoft.com/SMI/2016/WindowsSettings')
$application = $xml.SelectSingleNode('/*/a:application',$ns)
if (-not $application) { $application = $xml.DocumentElement.AppendChild($xml.CreateElement('application','urn:schemas-microsoft-com:asm.v3')) }
$settings = $application.SelectSingleNode('a:windowsSettings',$ns)
if (-not $settings) { $settings = $application.AppendChild($xml.CreateElement('windowsSettings','urn:schemas-microsoft-com:asm.v3')) }
$aware = $settings.SelectSingleNode('w:longPathAware',$ns)
if (-not $aware) { $aware = $settings.AppendChild($xml.CreateElement('longPathAware','http://schemas.microsoft.com/SMI/2016/WindowsSettings')) }
$aware.InnerText = 'true'
Save-Xml $xml (Join-Path $Output 'app.manifest')
