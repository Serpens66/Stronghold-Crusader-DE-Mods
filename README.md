# Stronghold Crusader DE Mods and Code

Preview Video: https://youtu.be/Jdz_aAA7CE4  

## Download:
- Recommended Steam auto updates: https://steamcommunity.com/sharedfiles/filedetails/?id=3788821961  
- Releases: https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases (see this [table](#latest-mod-releases) for specific mods)   
- Nexusmods: https://www.nexusmods.com/profile/Serpens66/mods?gameId=7959

#### Trail (Un-)packer and modded/vanilla Launcher bat files:
- can be found here: https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/1.0.1  

## Installation:
- Make sure you installed the script extender below.
- - This is done by downloading the latest "Loader" from here first: https://gitlab.com/rawra-stronghold-crusader/shcde-bepinex/-/releases and copy pasting the content of it into your game install folder "Steam\steamapps\common\Stronghold Crusader Definitive Edition".  
- - Then download the latest "SHCDESE_X.XX.X.zip" from : https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/releases and copy paste it also in your install folder, replace if sth. already exists.
- Copy paste the mod folder into your "\Stronghold Crusader Definitive Edition\BepInEx\plugins" folder.

## Script Extender: 
https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender / https://www.nexusmods.com/strongholdcrusaderdefinitiveedition/mods/35  

### Script Extender Docu:
- Mod https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/main/docs/guides/bepinex-mod-guide.md?ref_type=heads
- lua doku: https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/blob/main/docs/guides/lua-reference.md?ref_type=heads
- enums: https://rawra-stronghold-crusader.gitlab.io/shcde-script-extender/api/SHCDESE.Interop.eTroops.html?q=etroop
- event types: https://rawra-stronghold-crusader.gitlab.io/shcde-script-extender/api/SHCDESE.EventAPI.Buildings.BuildStructureEventArgs.html

## Building:
They all have a build.bat file. You need to adjust them to your paths though and need the required programs installed.

## Verified releases

Release-enabled mods contain a `release.bat`. It only publishes a clean commit that is already present on `origin/main`, then uploads the ZIP, its SHA-256 file, and a provenance manifest to GitHub. Run `setup-check.bat` to verify the local tools and game dependencies. Machine-specific path overrides can be copied from `release.local.example.json` to the ignored `release.local.json`.

The provenance records the public source commit, packaged files, build tools, and dependency hashes. It is a documented statement by the repository owner, not an independently executed build. Upload the exact GitHub ZIP unchanged to Steam or NexusMods so its published SHA-256 remains verifiable.

<!-- RELEASE-INDEX:START -->
## Latest Mod Releases

These archives are produced by the repository release scripts from the linked public commit. The provenance file records the exact package, tool, and dependency hashes. This is a documented statement by the repository owner, not an independently executed build.

Where shown, the code-status badge compares a release with the current relevant mod sources on `main`. Click it to open the mod-specific filtered diff report.

| Mod | Latest release | Code status | Source commit | ZIP SHA-256 |
| --- | --- | --- | --- | --- |
| SerpsMods (Modpack) | [1.0.13](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/download/SerpsMods/v1.0.13/SerpsMods-v1.0.13.zip) | — | [21a581b](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/21a581b97b4fec7c0ec9eb0e14f375773981f611) | `aa4f99f7bdb172ce7c582a59be2116029fdd17697d17b780b77c0acdae0a8f86` |
| APIShared | [0.3.7](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/APIShared/v0.3.7) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FAPIShared.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/APIShared.md) | [d34061e](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/d34061efa42f0d90490cf3f222e217ff7954c8fb) | `8daa3f3efa9049a626ebc0eea36059d09db59877dd4307378039be78ed661b65` |
| BugfixesAndQoL | [1.0.154](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/BugfixesAndQoL/v1.0.154) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FBugfixesAndQoL.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/BugfixesAndQoL.md) | [f674e9a](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/f674e9aee425a42cc0350f86ec97e0148cb5cc1b) | `79bc7871985e62626b3d25a37f02bdc526b4475930897af997827ecc27c4bc57` |
| BuildingCosts | [1.0.106](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/BuildingCosts/v1.0.106) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FBuildingCosts.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/BuildingCosts.md) | [4185561](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/4185561afee63b403600e7b5c9bde604b2885a04) | `1f1c4df02803348dcf7e1adf1ca9d4ded990a7c4e64d5702fc6a5d1331794af2` |
| BuildingLimit | [1.0.24](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/BuildingLimit/v1.0.24) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FBuildingLimit.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/BuildingLimit.md) | [9f580bb](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/9f580bbc7e32072f9ef32e380997af25767d09f8) | `ab8872fbc72eefdf67a85a81af3c1f1b216b5472d0bf5793bdc24adebadf8bab` |
| ExtraFeatures | [1.0.96](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/ExtraFeatures/v1.0.96) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FExtraFeatures.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/ExtraFeatures.md) | [a2d4ecf](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/a2d4ecf375a650f836cc75e6c4acd401afab6ba4) | `84ba97499883e54ed41bcf1f2be23dcf547d1bdd6ef761ac3ab580ef9cd61f33` |
| RandomEvents | [1.0.40](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/RandomEvents/v1.0.40) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FRandomEvents.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/RandomEvents.md) | [27ac1b3](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/27ac1b346e4b77fcc3212c148f7712d3b6ae9cf4) | `749bf81d9abc58afed17a1521bba2e9a192adce79b1beeeffa62d6fd52c9b09f` |
| CastlePlanner | [0.8.27](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/CastlePlanner/v0.8.27) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FCastlePlanner.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/CastlePlanner.md) | [65256ba](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/65256bad7ecb7a02f28e5e4dc6e94929c9243985) | `335f24a4f7a3f4d8b1c2105619192fca4ca7505392f90b8c0ddd23cb8702b9d9` |
| StartConditions | [1.0.26](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/StartConditions/v1.0.26) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FStartConditions.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/StartConditions.md) | [87bfb14](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/87bfb14f76aabf1f33b339ce7f2741959036aab7) | `94810d4d97982bebf0b8df78a124d976cf0a474c837e1e0c570cde1add4c66e9` |
| UnitCosts | [1.0.28](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/UnitCosts/v1.0.28) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FUnitCosts.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/UnitCosts.md) | [da4162e](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/da4162e17d80b141388a8269052679a62f7c1426) | `5fa9a8cef1f2e33b19cb8ecf1755620b27463cded0125bd03d390fdb0596a3fb` |
| UnitLimit | [1.0.98](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/UnitLimit/v1.0.98) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FUnitLimit.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/UnitLimit.md) | [db8d426](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/db8d426d5c3d5ed9fe586ccd8e5b180474264112) | `4fcf88ec3ece2ad39c3bc90501affa6b89f3ade45583663500308b4efd6ae799` |

Verify a downloaded archive with `Get-FileHash <archive.zip> -Algorithm SHA256` and compare it with the release asset and table above.
<!-- RELEASE-INDEX:END -->

## Some small Guides:
- https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/tree/main/Guides

## Other Mods:
- https://gitlab.com/rawra-stronghold-crusader/shcde-fixes
- https://gitlab.com/ensrick7/crusader-de-tweaker
- https://github.com/richardbinder/Stronghold-Crusader-DE-AI-Buff/releases

## Attention:
Script Extender itself and also Mods using it **may in theory contain malicious code**, so only download from the official source and from modders you trust. See this repo for my source code. See here for the script extender official release: https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/releases

## Disclaimer:
I don't know C#, only lua/python. So most code here was created by ChatGPT 5.6 Sol in the Visual Studio Code Codex extension. I provided several open source projects of Stronghold Crusader as information source to Chatgpt. So besides ideas and prompts/instructions and ingame testing for the mods, I did not contribute any code myself.  
Directly used sources:  
- https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender
- https://github.com/sourcehold
- https://github.com/UnofficialCrusaderPatch/UnofficialCrusaderPatch3

## Credits:
- Big thank to "Rawra" the creator of the script extender, which makes modding possible :)  
- Thanks to the creators of "BepInEx", without the script extender would not be possiible.  
- Thanks to the **UCP Modding team**, who made so many fixes and features for the HD version of the game! Fixing the same issues in DE was easier, because I was able to check how the UCP team did it. UCP for HD version of the game: **https://github.com/UnofficialCrusaderPatch**
