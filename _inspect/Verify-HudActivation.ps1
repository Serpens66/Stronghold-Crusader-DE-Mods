$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = [IO.File]::ReadAllText((Join-Path $root 'APIShared/src/UnitHudPresentationCapability.cs'))
function Block([string]$marker) {
    $start = $source.IndexOf($marker, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Missing block: $marker" }
    $open = $source.IndexOf('{', $start)
    $depth = 1
    $end = $open + 1
    while ($depth -gt 0 -and $end -lt $source.Length) {
        if ($source[$end] -eq '{') { $depth++ }
        if ($source[$end] -eq '}') { $depth-- }
        $end++
    }
    return $source.Substring($start, $end - $start)
}
$contracts = [IO.File]::ReadAllText((Join-Path $root 'APIShared/src/UnitHudContracts.cs'))
$code = @'
using System;
using System.Collections.Generic;
using System.Linq;
using Noesis;
namespace Noesis { public class ImageSource {} }
namespace APIShared { public class NativeCapabilityDiagnostic {} }
'@
$code += $contracts.Substring($contracts.IndexOf('namespace APIShared'))
$code += @'
namespace APIShared {
public class ActivationHarness {
 private enum eChimps { CHIMP_TYPE_ARCHER = 22 }
 private readonly object sync = new object();
 private readonly List<CategoryRegistration> categories = new List<CategoryRegistration>();
 private readonly List<ImageRegistration> imageOverrides = new List<ImageRegistration>();
 private readonly List<InteractionRegistration> interactions = new List<InteractionRegistration>();
 private readonly List<RecruitmentRegistration> recruitment = new List<RecruitmentRegistration>();
 private CategoryRegistration[] categoryView = Array.Empty<CategoryRegistration>();
 private ImageRegistration[] imageView = Array.Empty<ImageRegistration>();
 private InteractionRegistration[] interactionView = Array.Empty<InteractionRegistration>();
 private readonly Dictionary<int, RecruitmentRegistration[]> recruitmentViews = new Dictionary<int, RecruitmentRegistration[]>();
 private readonly Dictionary<string, bool> ownerActivation = new Dictionary<string, bool>();
 private UnitHudSurface activeSurfaces, restoreSurfaces;
 private bool activeImages, activeRecruitmentHandlers, refreshRequested, pendingPresentation, restoreImages;
 private bool OwnerActive(string owner) => !ownerActivation.TryGetValue(owner, out bool active) || active;
 private static bool HasSurface(CategoryRegistration c, UnitHudSurface surface) => (c.Definition.Surfaces & surface) != 0;
 private bool Fail(string message, out NativeCapabilityDiagnostic d) {d = new NativeCapabilityDiagnostic(); return false;}
 private static NativeCapabilityDiagnostic Available(string message) => new NativeCapabilityDiagnostic();
'@
foreach ($marker in @('private bool RegisterCategory(', 'private bool RegisterInteraction(', 'private bool RegisterImage(', 'private bool RegisterRecruitment(',
    'private void RebuildActiveViews(', 'private void SetOwnerActive(', 'private bool SetRegistrationActive(', 'private void RequestRefresh(',
    'private static int Compare(', 'private sealed class CategoryRegistration', 'private sealed class ImageRegistration',
    'private sealed class InteractionRegistration', 'private sealed class RecruitmentRegistration')) {
    $code += (Block $marker) + "`r`n"
}
$sortStart = $source.IndexOf('private void SortRegistrations()')
$sortEnd = $source.IndexOf('});', $sortStart) + 3
$code += $source.Substring($sortStart, $sortEnd - $sortStart)
$code += @'
 private static void Check(bool value, string name) {if (!value) throw new Exception(name);}
 public static string Run() {
  var h = new ActivationHarness();
  h.RequestRefresh("passive"); Check(!h.pendingPresentation && !h.refreshRequested,"passive lookup");
  h.SetOwnerActive("lord",false);
  h.RegisterCategory("lord",new UnitHudCategoryDefinition("lord","Lord",55,UnitHudSurface.All), u=>true,out _);
  Check(h.activeSurfaces==UnitHudSurface.None && !h.pendingPresentation,"inactive registration");
  h.SetOwnerActive("lord",true);
  Check((h.activeSurfaces & UnitHudSurface.ArmyReport)!=0 && !h.activeRecruitmentHandlers,"no fictitious recruitment");
  h.RegisterCategory("archer",new UnitHudCategoryDefinition("archer","Archer",22,UnitHudSurface.All),u=>true,out _);
  h.RegisterRecruitment("archer","archer", t=>true,out _);
  Check(h.activeRecruitmentHandlers,"recruitment active");
  h.SetOwnerActive("lord",false); Check(h.categoryView.Length==1 && h.activeRecruitmentHandlers,"other owner retained");
  Check(!h.SetRegistrationActive("lord","archer",false,false),"owner isolation");
  h.SetRegistrationActive("archer","archer",false,false);
  Check(h.activeSurfaces==UnitHudSurface.None && !h.activeRecruitmentHandlers && h.pendingPresentation,"last category disable");
  h.SetRegistrationActive("archer","archer",true,false); Check(h.activeRecruitmentHandlers,"reactivation");
  h.SetOwnerActive("archer",false);
  h.RegisterImage("skin",new UnitHudImageOverrideDefinition("skin",UnitHudImageSlot.UIBuildingsO011), c=>null,out _);
  Check(h.activeImages && h.activeSurfaces==UnitHudSurface.None,"image-only");
  h.SetRegistrationActive("skin","skin",false,true);
  Check(!h.activeImages && h.restoreImages,"image restoration");
  h.refreshRequested=false; h.pendingPresentation=false;
  for(int i=0;i<10000;i++) h.RequestRefresh("archer");
  Check(!h.refreshRequested && !h.pendingPresentation,"inactive refresh storm");
  h.RegisterInteraction("lord","click", c=>{},out _);
  Check(h.interactionView.Length==0,"inactive interaction");
  h.SetOwnerActive("lord",true); Check(h.interactionView.Length==1,"interaction reactivation");
  return "PASS: extracted production activation methods; multi-owner, legacy, registration, recruitment, image-only, restoration flags, inactive refresh storm and interactions.";
 }
}}
'@
Add-Type -TypeDefinition $code -Language CSharp
[APIShared.ActivationHarness]::Run()
$render = Block 'private void OnBeforeRender()'
if ($render.IndexOf('activeSurfaces == UnitHudSurface.None') -gt $render.IndexOf('Time.frameCount')) { throw 'Idle guard is too late.' }
if ($render.IndexOf('ExpireRecruitment();') -gt $render.IndexOf('MainViewModel.viewModelLoaded')) { throw 'Ticket expiry depends on HUD readiness.' }
$army = Block 'private void ApplyArmyReport('
if ($army.IndexOf('RefReportsArmy4Panel.Visibility') -gt $army.IndexOf('GetAllAliveUnits()')) { throw 'Army scan precedes visibility guard.' }
if ($army.Contains('new Dictionary') -or $army.Contains('new List')) { throw 'Army scratch collections allocated per frame.' }
Write-Output 'PASS: render idle/expiry/visibility order and reusable army buffers.'
