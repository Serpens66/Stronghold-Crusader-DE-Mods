#Requires -Version 7.0
[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$modRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$production = [IO.File]::ReadAllText((Join-Path $modRoot 'src\ForeignTroopPlayerColors.cs'))
# Exercise the production resolver in isolation; no mod build or game assemblies are loaded.
$fixture = @'
public static class SpriteMapping
{
    public static int[] remapColours;
    public static int[] Loaded;
    public static int Calls;
    public static int RemapMPLoadedColour(int owner)
    {
        Calls++;
        return Loaded == null ? owner : Loaded[owner];
    }
}
public sealed class Platform_Multiplayer
{
    public static Platform_Multiplayer Instance;
    public int[] UIColourRemap;
}
public static class Enums { public enum GM { GM_BODY_ARCHER } }
namespace UnityEngine
{
    public struct Color { public float r, g, b; }
}
public sealed class spriteLoader
{
    public static spriteLoader instance;
    public static int Calls;
    public static int LastPalette;
    public object GetGMMaterial(Enums.GM file, int palette, int chop,
        out UnityEngine.Color tint, int transparency)
    {
        if (file != Enums.GM.GM_BODY_ARCHER || chop != 0 || transparency != 0)
            throw new Exception("Wrong material contract");
        Calls++;
        LastPalette = palette;
        tint = new UnityEngine.Color { r = palette / 255f, g = 100 / 255f, b = 200 / 255f };
        return null;
    }
}
public static class ForeignTroopPlayerColorTests
{
    private static int checks;
    private static void Check(bool passed, string name)
    {
        checks++;
        if (!passed) throw new Exception(name);
    }
    private static void Defaults()
    {
        SpriteMapping.remapColours = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
        SpriteMapping.Loaded = null;
        SpriteMapping.Calls = 0;
        spriteLoader.Calls = 0;
        spriteLoader.instance = new spriteLoader();
        Platform_Multiplayer.Instance = new Platform_Multiplayer {
            UIColourRemap = new[] { 0, 1, 4, 2, 3, 6, 5, 7, 8 }
        };
    }
    public static string Run()
    {
        Defaults();
        var cache = new BugfixesAndQoL.ForeignTroopPlayerColors();
        int[] expectedPortraits = { 0, 1, 4, 2, 3, 6, 5, 7, 8 };
        for (int owner = 1; owner <= 8; owner++)
        {
            var color = cache.Get(owner);
            Check(color.PortraitColorId == expectedPortraits[owner], "All eight portrait colors");
            Check(spriteLoader.LastPalette == owner, "Material receives palette ID, not UI ID");
            Check(color.LabelArgb == (0xFF0064C8u | ((uint)owner << 16)), "Exact material tint");
            Check(cache.Get(owner).LabelArgb == color.LabelArgb, "Same owner across groups");
        }
        Check(spriteLoader.Calls == 8 && SpriteMapping.Calls == 8, "One capture per owner per refresh");

        // Map/save/color changes with exactly the same selected owner.
        SpriteMapping.remapColours[2] = 7;
        Check(cache.Get(2).PortraitColorId == 4, "Stable snapshot within one refresh");
        cache.Reset();
        Check(cache.Get(2).PortraitColorId == 7, "Next refresh adopts color change");
        Check(cache.Get(2).LabelArgb == 0xFF0764C8u, "Label also changes without selection change");
        SpriteMapping.Loaded = new[] { 0, 1, 8, 3, 4, 5, 6, 7, 2 };
        cache.Reset();
        Check(cache.Get(2).PortraitColorId == 8, "MP loaded-player mapping before palette mapping");
        Check(cache.Get(2).LabelArgb == 0xFF0864C8u, "MP label mapping");
        Defaults();
        cache.Reset();
        Check(cache.Get(2).PortraitColorId == 4, "New session does not retain previous mapping");

        int calls = SpriteMapping.Calls;
        foreach (int owner in new[] { -1, 0, 9, int.MaxValue })
        {
            var color = cache.Get(owner);
            Check(color.PortraitColorId == 0 && color.LabelArgb == 0xFFD2CCBCu, "Neutral/invalid owner");
        }
        Check(SpriteMapping.Calls == calls, "Invalid owners never enter Vanilla lookup");
        for (int broken = 0; broken < 9; broken++)
        {
            Defaults();
            switch (broken)
            {
                case 0: SpriteMapping.remapColours = null; break;
                case 1: SpriteMapping.remapColours = new int[2]; break;
                case 2: SpriteMapping.remapColours[2] = -1; break;
                case 3: SpriteMapping.remapColours[2] = 9; break;
                case 4: Platform_Multiplayer.Instance = null; break;
                case 5: Platform_Multiplayer.Instance.UIColourRemap = null; break;
                case 6: Platform_Multiplayer.Instance.UIColourRemap = new int[2]; break;
                case 7: Platform_Multiplayer.Instance.UIColourRemap[2] = 9; break;
                case 8: spriteLoader.instance = null; break;
            }
            cache.Reset();
            Check(cache.Get(2).PortraitColorId == 0, "Missing mapping uses base portrait");
            Check(cache.Get(2).LabelArgb == 0xFFD2CCBCu, "Missing mapping uses neutral label");
            Defaults();
            cache.Reset();
            Check(cache.Get(2).PortraitColorId == 4, "Readiness failure retried on next refresh");
        }
        foreach (int mappedOwner in new[] { -1, 0, 9 })
        {
            Defaults();
            SpriteMapping.Loaded = new[] { 0, 1, mappedOwner, 3, 4, 5, 6, 7, 8 };
            cache.Reset();
            Check(cache.Get(2).PortraitColorId == 0, "Invalid MP mapping");
        }
        return checks + " color behavior checks passed";
    }
}
'@
Add-Type -TypeDefinition ($production + [Environment]::NewLine + $fixture)
Write-Host ([ForeignTroopPlayerColorTests]::Run())

$runtime = [IO.File]::ReadAllText((Join-Path $modRoot 'src\ForeignTroopHudRuntime.cs'))
$view = [IO.File]::ReadAllText((Join-Path $modRoot 'src\ForeignTroopHudView.cs'))
foreach ($required in @(
    'playerColors.Reset\(\);\s+view.ResetForMap\(\);',
    'entries.Clear\(\);\s+playerColors.Reset\(\);',
    'ForeignTroopPlayerColor color = playerColors.Get\(owner\);',
    'entry.ColorId = color.PortraitColorId;',
    'entry.OwnerColorArgb = color.LabelArgb;'
)) {
    if ($runtime -notmatch $required) { throw "Missing runtime integration: $required" }
}
if ($runtime -match 'entry.ColorId = \(int\)unit.r_SpritePlayerColorId') {
    throw 'Unit sprite color must not determine player presentation.'
}
foreach ($required in @(
    'entries\[i\].OwnerColorArgb != visibleEntries\[i\].OwnerColorArgb',
    'copy.OwnerColorArgb = entry.OwnerColorArgb;',
    'drawn.OwnerColorArgb != entry.OwnerColorArgb',
    'PlayerBrush\(entry.OwnerColorArgb\)',
    'drawn.OwnerColorArgb = entry.OwnerColorArgb;',
    'drawn.ColorId != entry.ColorId',
    'LordPortraitPalette.Get\(entry.ColorId\)',
    'FindPortrait\(portraitCode, entry.ColorId\)',
    'Array.Clear\(drawnEntries, 0, drawnEntries.Length\)'
)) {
    if ($view -notmatch $required) { throw "Missing view integration: $required" }
}
Write-Host 'Runtime/view source contracts passed (refresh, session reset, label/portrait invalidation, HUD recreation).'
