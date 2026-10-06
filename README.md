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
| SerpsMods (Modpack) | [1.0.21](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/download/SerpsMods/v1.0.21/SerpsMods-v1.0.21.zip) | — | [6254960](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/6254960511a38d8aab682cd72191582a91af0cd0) | `2a6908386e628b23775ae0322bd4591f5e8536fffda7caae3f6509a1a00aa7cb` |
| APIShared | [0.4.9](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/APIShared/v0.4.9) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FAPIShared.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/APIShared.md) | [4d571e8](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/4d571e8745e585c688d82cd5f70c2a8b90d3a7d2) | `362b88bff75ef77454174cb2fe62b150418484fb3a46c0556a16d121dff63824` |
| BugfixesAndQoL | [1.0.173](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/BugfixesAndQoL/v1.0.173) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FBugfixesAndQoL.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/BugfixesAndQoL.md) | [4fa738a](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/4fa738a24d1e84ebca910fe5fd1424a592674ab2) | `82ec2d3c0cb47088a78aa364c37345d6a0ba0db328c77a968e50442e86e699a7` |
| BuildingCosts | [1.0.109](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/BuildingCosts/v1.0.109) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FBuildingCosts.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/BuildingCosts.md) | [1c02d19](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/1c02d195b1c694da6bfc2e802698d8f4ce9d13ff) | `456ea10ef0d5ff4b8f4e37222f42fa9bf956bc86ed2ff15a6f25a9900eddf8aa` |
| BuildingLimit | [1.0.26](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/BuildingLimit/v1.0.26) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FBuildingLimit.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/BuildingLimit.md) | [1c3bc01](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/1c3bc01ebf7e6428e6c61f12b69e9b9926a0e679) | `b2c7086acb44a50cd307d37a6f22e3f434288638f08f3dfcbb1982f5b4c36d32` |
| ExtendedData | [1.0.5](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/ExtendedData/v1.0.5) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FExtendedData.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/ExtendedData.md) | [f4ccc82](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/f4ccc82eb2c83716054bf34f151be591ccb70756) | `89a41ac22abfe560ab0c0d60117e271b364ea3d0d9dc1829e3e675c816ed582e` |
| ExtraFeatures | [1.0.107](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/ExtraFeatures/v1.0.107) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FExtraFeatures.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/ExtraFeatures.md) | [2d7f182](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/2d7f182d4a8dd5f8275aa7f4033c474eea2ea53b) | `ba1102bc8d980866dfadc46555c9859c5c93a902f835c9a82d54edce3dccb1c0` |
| RandomEvents | [1.0.47](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/RandomEvents/v1.0.47) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FRandomEvents.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/RandomEvents.md) | [07e059a](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/07e059adde5b40307b8b1ce8af1ea437c13e421e) | `71b7d965882a57979e4bbb88c7eac4b835fcbaede407ae04b6c3fe1558274d7f` |
| CastlePlanner | [0.8.35](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/CastlePlanner/v0.8.35) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FCastlePlanner.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/CastlePlanner.md) | [61499c8](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/61499c8bfea89e2ac836468be41c64f040195f3f) | `16adec50c3eb8d1fee71b0192a7170a62f6a2c278228beefceb3641f328ccfa1` |
| StartConditions | [1.0.28](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/StartConditions/v1.0.28) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FStartConditions.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/StartConditions.md) | [0f0470a](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/0f0470a7a2ee00f42246a19c18a402f28fef54bd) | `09af11fdcc4627875bd8e8ff1fef88b2b38e1e8b49d793a66e5afc0d9710d3f9` |
| UnitCosts | [1.0.32](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/UnitCosts/v1.0.32) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FUnitCosts.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/UnitCosts.md) | [1a5f090](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/1a5f09017f48d96c66f63959736452c57e8f649e) | `7befce1a2bc213d5410007305fc749a88acaf91e734c77cc361bebd79188b34e` |
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
