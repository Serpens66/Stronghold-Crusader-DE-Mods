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
| ExtraFeatures | [1.0.98](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/ExtraFeatures/v1.0.98) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FExtraFeatures.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/ExtraFeatures.md) | [cf337ec](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/cf337ec2315cc710d95815d27b012ee188809ca3) | `cbd80389fb207e0428cf99d47c2eb1fc1840514449914fe368bc441f79e09bc2` |
| RandomEvents | [1.0.42](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/RandomEvents/v1.0.42) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FRandomEvents.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/RandomEvents.md) | [3729ba2](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/3729ba2988d6a63e4c7aeeddfa0fc7f550c501a2) | `408e56cd0142e2e7ac09ebf2c1f55ea3c87f9ac21c4d0ca12b77fd53a9141256` |
| CastlePlanner | [0.8.29](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/CastlePlanner/v0.8.29) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FCastlePlanner.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/CastlePlanner.md) | [1a8523d](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/1a8523d6ce340ab457ec6a8d45477cf096b9c722) | `b2bc3f6501ee51f4fb85b170de662a6b618b03bc0b76c83c7a59cecddd7c037d` |
| StartConditions | [1.0.27](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/StartConditions/v1.0.27) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FStartConditions.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/StartConditions.md) | [b30c092](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/b30c092997dfbda06efc55cf7fd8b331a5ab572a) | `a76a46c36b87ac1ccccf003e49cc46891d45dc330d9487f68369f850d6e0137d` |
| UnitCosts | [1.0.29](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/UnitCosts/v1.0.29) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FUnitCosts.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/UnitCosts.md) | [597b3db](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/597b3dbd1b9e87466ea96ba04aa0485d048c9555) | `9a56af611b463edd87a086cc4ecd6eb2840ae2d5b23cbc128b26ff4f86492e42` |
| UnitLimit | [1.0.99](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/UnitLimit/v1.0.99) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FUnitLimit.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/UnitLimit.md) | [4e5980a](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/4e5980a3956ec75766d62f5e7771f93fe36be1bd) | `7b97b430cf83dbd6ddc245c84f5a1fa15856e2b2a11ae0257558c2f0801e2b2f` |

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
