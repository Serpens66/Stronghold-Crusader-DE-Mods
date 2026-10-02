# BuildingCosts release status

**Status:** code newer

- Release: [v1.0.109](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/BuildingCosts/v1.0.109)
- Release commit: [1c02d19](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/1c02d195b1c694da6bfc2e802698d8f4ce9d13ff)
- Current main commit: [dbad4e4](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/dbad4e42416b359357ac423585f7ee6a46384534)

## Relevant changed files

- `Shared/SerpLocalization.cs`

Relevant localization keys: `BuildingCosts.Vanilla`

## Diff

```diff
diff --git a/Shared/SerpLocalization.cs b/Shared/SerpLocalization.cs
index 33966a16..8a310a98 100644
--- a/Shared/SerpLocalization.cs
+++ b/Shared/SerpLocalization.cs
@@ -558,7 +560,9 @@ public static class SerpLocalization
         { BuildingLimitsHelp, "Only for Human! -1 = unlimited. Allowed range: -1 to 10000. Variants such as gardens, statues, shrines and ponds are counted together." },
         { BuildingLimitCrusaderDeTweakerWarning, "Crusader DE Tweaker is loaded. Configure building limits in only one of the two mods; if both define limits, the stricter limit applies." },
         { UnitCostsTitle, "Base Costs (Human and AI)" },
-        { UnitCostsHelp, "Good slots apply to European units. unchanged keeps the vanilla slot; gold -1 stays unchanged." },
+        { UnitCostsHelp, "Gold -1 keeps the original cost. No Weapons removes all four European recruitment requirements, including the knight's horse, for human and AI players." },
+        { UnitCostsNoWeapons, "No Weapons" },
+        { UnitCostsNoWeaponsHelp, "{0}: Removes all four weapon and horse requirements for human and AI recruitment. Uncheck to restore the original requirements." },
         { UnitCostsExtraTitle, "Additional Costs (Human only)" },
         { UnitCostsExtraHelp, "0 = no extra cost. Positive values are charged in addition; negative gold refunds up to the current gold cost. A checked horse reserves one available stable horse for the recruited unit. AI players ignore this table." },
         { UnitHeader, "Unit" },
```
