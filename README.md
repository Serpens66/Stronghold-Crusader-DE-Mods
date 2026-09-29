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
| SerpsMods (Modpack) | [1.0.19](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/download/SerpsMods/v1.0.19/SerpsMods-v1.0.19.zip) | — | [dffa698](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/dffa698afffa8b54d28d78f7420853bc06667b94) | `b0d4a558b1f5d3b7783eecaeb60fa842ae46db0d05f0068f81ed510f0311883a` |
| APIShared | [0.4.6](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/APIShared/v0.4.6) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FAPIShared.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/APIShared.md) | [4db2244](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/4db2244473507880501dd5dd48b259c4ed6547d9) | `e0ef9b82f966b432e9a48319d7dcfacfd5d64ccb9173b6f48378de0a25493db4` |
| BugfixesAndQoL | [1.0.172](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/BugfixesAndQoL/v1.0.172) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FBugfixesAndQoL.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/BugfixesAndQoL.md) | [244f11f](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/244f11f5a687cc2dcc57cc05bcba8f7f394add58) | `69a65f356430f9ebcf71b1b7bc1cc30fbdbac05879e35df76f72f9f9b51265c7` |
| BuildingCosts | [1.0.109](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/BuildingCosts/v1.0.109) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FBuildingCosts.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/BuildingCosts.md) | [1c02d19](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/1c02d195b1c694da6bfc2e802698d8f4ce9d13ff) | `456ea10ef0d5ff4b8f4e37222f42fa9bf956bc86ed2ff15a6f25a9900eddf8aa` |
| BuildingLimit | [1.0.26](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/BuildingLimit/v1.0.26) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FBuildingLimit.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/BuildingLimit.md) | [1c3bc01](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/1c3bc01ebf7e6428e6c61f12b69e9b9926a0e679) | `b2c7086acb44a50cd307d37a6f22e3f434288638f08f3dfcbb1982f5b4c36d32` |
| ExtendedData | [1.0.4](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/ExtendedData/v1.0.4) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FExtendedData.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/ExtendedData.md) | [6df9db7](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/6df9db7b1e36953794eb44da611155135d405c77) | `c23de6e7201bfb5603b17da5b336a7a89eb3e83031bbadc791db9f1f0ca68d59` |
| ExtraFeatures | [1.0.106](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/ExtraFeatures/v1.0.106) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FExtraFeatures.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/ExtraFeatures.md) | [a4b4c0d](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/a4b4c0db44877143ca3915db4d632810ad4d12fc) | `e53f24fb59b4bb651500101207b86108e733ca467124ae42bc8d766402ef5745` |
| RandomEvents | [1.0.46](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/RandomEvents/v1.0.46) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FRandomEvents.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/RandomEvents.md) | [1ca9871](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/1ca98715a08454b45e1d719f23d2e3254b167082) | `7458f47ce2e3dbde24410668bbf9bfa1f70a07f3892162fa611f0882a2710f9c` |
| CastlePlanner | [0.8.33](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/CastlePlanner/v0.8.33) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FCastlePlanner.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/CastlePlanner.md) | [c571dd4](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/c571dd436d7de0b96c2fb23716da0b41445f20aa) | `bd195dbddee9c62d8127a71a2d95968aaf1f804eb1f4f468c9f0270b3e6c723c` |
| StartConditions | [1.0.28](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/StartConditions/v1.0.28) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FStartConditions.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/StartConditions.md) | [0f0470a](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/0f0470a7a2ee00f42246a19c18a402f28fef54bd) | `09af11fdcc4627875bd8e8ff1fef88b2b38e1e8b49d793a66e5afc0d9710d3f9` |
| UnitCosts | [1.0.31](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/UnitCosts/v1.0.31) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FUnitCosts.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/UnitCosts.md) | [57ecbdb](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/57ecbdbb9c3eb1266e19902152860d91a766e831) | `662d189a7070b624da3340c312a9c3f97df62e68452f68388ea72ae64b66c126` |
| UnitLimit | [1.0.101](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/UnitLimit/v1.0.101) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FUnitLimit.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/UnitLimit.md) | [4800d86](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/4800d86ec2a9003929a52704c8188a01bb0b2409) | `3508414c6c88fa65c6a67527e416ac124503bfdb884b440019041931fc73d537` |

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
