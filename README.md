# Stronghold Crusader DE Mods

Optional fixes and configurable gameplay improvements for Stronghold Crusader
Definitive Edition. [Watch the preview](https://youtu.be/Jdz_aAA7CE4).

## Download

- [Serps Mods on Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3788821961): recommended for automatic updates.
- [GitHub releases](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases): complete packs and individual mods.
- [Nexus Mods](https://www.nexusmods.com/profile/Serpens66/mods?gameId=7959).
- [Trail tools and modded/Vanilla launchers](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/1.0.1).

## Installation

1. Install the [BepInEx loader](https://gitlab.com/rawra-stronghold-crusader/shcde-bepinex/-/releases)
   and [Script Extender](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/releases)
   into your game directory.
2. Subscribe to [APIShared](https://steamcommunity.com/workshop/filedetails/?id=3818609220)
   separately, then subscribe to the Serps Mods pack. For manual installation,
   extract the complete official APIShared and mod ZIPs into the game directory
   while the game is closed, retaining their `BepInEx` folder structure.
3. Open the mod settings before starting a match. Choose the features and presets
   you want. The host controls gameplay options; personal interface options are
   configured independently by each player.

Use the same gameplay-mod versions as the other players in multiplayer. Each mod's
page lists its requirements and any limits for particular game modes.

On Linux/Proton, follow the [Linux modding guide](Guides/Linux%20and%20Proton%20Modding.md).
If Script Extender cannot complete APIShared's first Workshop installation, download
the separate [Proton Workshop Fix ZIP (0.27.0)](https://github.com/SHCDE-APIShared/APIShared/releases/download/APIShared/v0.27.0/APIShared-ProtonWorkshopFix-v0.27.0.zip)
and follow the guide's manual repair steps. This patcher is installed separately
from APIShared and the modpack.

## Mods in this repository

| Mod | What it offers |
| --- | --- |
| [Bugfixes and QoL](BugfixesAndQoL/README.md) | Configurable game fixes, AI improvements, unit commands and interface conveniences. |
| [Building Costs](BuildingCosts/README.md) | Custom construction costs. |
| [Building Limit](BuildingLimit/README.md) | Individual building limits for human players. |
| [Unit Costs](UnitCosts/README.md) | Recruitment costs and additional resource requirements. |
| [Unit Limit](UnitLimit/README.md) | Individual troop and siege-unit limits for human players. |
| [Extra Features](ExtraFeatures/README.md) | Economy, unit, gatehouse and plague options. |
| [Random Events](RandomEvents/README.md) | Configurable positive and negative skirmish events. |
| [Start Conditions](StartConditions/README.md) | Starting gold, goods and armies. |
| [CastlePlanner](CastlePlanner/README.md) | Castle blueprints, starting castles and AI placement checks. |
| [Extended Data](ExtendedData/README.md) | Custom Lords, Trails, map data and saved mod settings. |
| [Spectator](Spectator/README.md) | Player perspectives, live statistics, Lord overview and optional local AI testing tools. |

<!-- RELEASE-INDEX:START -->
## Latest Mod Releases

Choose a published release below. APIShared is installed separately from its [official releases](https://github.com/SHCDE-APIShared/APIShared/releases) or [Steam Workshop page](https://steamcommunity.com/workshop/filedetails/?id=3818609220).

The code-status badge shows whether newer changes are waiting for release. Use the linked download; unreleased source changes are not included in older packages.

| Mod | Latest release | Code status | Source commit | ZIP SHA-256 |
| --- | --- | --- | --- | --- |
| SerpsMods (Modpack) | [1.0.35](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/download/SerpsMods/v1.0.35/SerpsMods-v1.0.35.zip) | — | [6f46170](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/6f461700f6187c54afb12449c9229f7ed1665dfa) | `ab86d90d885a4511046317e55da1c9edf58beada49da67005e4308e5c5bc25a0` |
| BugfixesAndQoL | [1.0.211](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/BugfixesAndQoL/v1.0.211) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FBugfixesAndQoL.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/BugfixesAndQoL.md) | [08fe05d](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/08fe05ddc84a2fe70cff893fe448f8e67f713fc2) | `7a837cbb6b687d5e4228c5035793638f6a0248354b5df976bbf19bc16a822dc2` |
| BuildingCosts | [1.0.117](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/BuildingCosts/v1.0.117) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FBuildingCosts.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/BuildingCosts.md) | [08fe05d](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/08fe05ddc84a2fe70cff893fe448f8e67f713fc2) | `49c8f48c68e7b28db70327358f73d9d3d4bcd76b158144d97c3ef3fe239dbd13` |
| BuildingLimit | [1.0.34](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/BuildingLimit/v1.0.34) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FBuildingLimit.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/BuildingLimit.md) | [08fe05d](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/08fe05ddc84a2fe70cff893fe448f8e67f713fc2) | `c658f23e2b47d52c30b5c43d0f455bc1132fd3a7a01543f86e2a76f3c0f0b3a9` |
| ExtendedData | [1.0.24](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/ExtendedData/v1.0.24) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FExtendedData.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/ExtendedData.md) | [08fe05d](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/08fe05ddc84a2fe70cff893fe448f8e67f713fc2) | `3b3a083ae63af6a73eab54ee70e60768f267274902c5b0f01a6f0c0eec4897b3` |
| ExtraFeatures | [1.0.129](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/ExtraFeatures/v1.0.129) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FExtraFeatures.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/ExtraFeatures.md) | [08fe05d](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/08fe05ddc84a2fe70cff893fe448f8e67f713fc2) | `e882676495658bd760953a83211b4306f405a4bd613900450329081a67110f44` |
| RandomEvents | [1.0.57](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/RandomEvents/v1.0.57) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FRandomEvents.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/RandomEvents.md) | [08fe05d](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/08fe05ddc84a2fe70cff893fe448f8e67f713fc2) | `b8aca88d1f712ec7edc6ac117ee59568eba02de81e0ff2259db66e26cc4398cf` |
| CastlePlanner | [0.8.50](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/CastlePlanner/v0.8.50) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FCastlePlanner.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/CastlePlanner.md) | [08fe05d](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/08fe05ddc84a2fe70cff893fe448f8e67f713fc2) | `73e078d48325c8e37cd17fd581399ea601db3343ad9183545866461878d6cf3c` |
| Spectator | [0.1.0](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/Spectator/v0.1.0) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FSpectator.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/Spectator.md) | [08fe05d](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/08fe05ddc84a2fe70cff893fe448f8e67f713fc2) | `23c5c9525259a9942740cbf2df6b2539657c3d54a0f4cb3f7b4198f551dd76f2` |
| StartConditions | [1.0.34](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/StartConditions/v1.0.34) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FStartConditions.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/StartConditions.md) | [08fe05d](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/08fe05ddc84a2fe70cff893fe448f8e67f713fc2) | `89e82a09a64df08bf002d0f6e4b770fd8eb447b1dfd9027ccc0616eb44d72458` |
| UnitCosts | [1.0.39](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/UnitCosts/v1.0.39) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FUnitCosts.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/UnitCosts.md) | [08fe05d](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/08fe05ddc84a2fe70cff893fe448f8e67f713fc2) | `8e4b77fd4138f2e64526d818a9edf9c481e71759fd357b44ffa4b19530916ef6` |
| UnitLimit | [1.0.109](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/UnitLimit/v1.0.109) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FUnitLimit.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/UnitLimit.md) | [08fe05d](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/08fe05ddc84a2fe70cff893fe448f8e67f713fc2) | `425887e41c9c45063d119da8ac3db0a4cd6758d74362dbef041654da6bf9a175` |

Verify a downloaded archive with `Get-FileHash <archive.zip> -Algorithm SHA256` and compare it with the release asset and table above.
<!-- RELEASE-INDEX:END -->

## Help

See the [guides](Guides), including [how to report errors](Guides/How%20to%20Report%20Errors.md).
Download executable mods only from official sources and authors you trust.

## Other mods

- [Crusader DE Tweaker](https://gitlab.com/ensrick7/crusader-de-tweaker).
- [AI Buff](https://github.com/richardbinder/Stronghold-Crusader-DE-AI-Buff/releases).

## Credits

Thanks to Rawra for the Script Extender, the BepInEx authors, and the
[Unofficial Crusader Patch team](https://github.com/UnofficialCrusaderPatch) for
their work on the HD game. Other source references include
[sourcehold](https://github.com/sourcehold). These mods were developed with AI
assistance; Serpens66 provided the ideas, direction and in-game testing.
