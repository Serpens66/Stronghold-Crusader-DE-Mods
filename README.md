# Stronghold Crusader DE Mods and Code

## Download:
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

The code-status badge compares each release with the current relevant mod sources on `main`. Click it to open the mod-specific filtered diff report.

| Mod | Latest release | Code status | Source commit | ZIP SHA-256 |
| --- | --- | --- | --- | --- |
| BugfixesAndQoL | [1.0.69](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/BugfixesAndQoL/v1.0.69) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FBugfixesAndQoL.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/BugfixesAndQoL.md) | [5781a09](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/5781a095509f5a0f35d5ef41e65b585c0e434fc8) | `72d7028f179c47baf3adbb52f8e5774a2688298113c0ee267b6fc0d942b2245b` |
| BuildingCosts | [1.0.95](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/BuildingCosts/v1.0.95) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FBuildingCosts.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/BuildingCosts.md) | [fea4dba](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/fea4dbaa21f9599f305db173a10a1e0531f7b683) | `4a38ad38ba124379118f617c4cdcc965f25987fd4bf86dadd1182769fafe8e4e` |
| BuildingLimit | [1.0.12](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/BuildingLimit/v1.0.12) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FBuildingLimit.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/BuildingLimit.md) | [8e91d80](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/8e91d800622adf96eb8dfd326bfc60251686fdf1) | `2a2f436c02f4f9ef5b375a43cfaedce29e89df424ddbdbb2b4b81400c272d954` |
| CustomCustomTrail | [1.3.31](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/CustomCustomTrail/v1.3.31) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FCustomCustomTrail.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/CustomCustomTrail.md) | [97ad5db](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/97ad5db622ea02559c072dab6a50226eab93213d) | `cbdbfdbfb35571553422afba44d9323689418afd83bf8b2016cdeaaac145f639` |
| ExtraFeatures | [1.0.35](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/ExtraFeatures/v1.0.35) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FExtraFeatures.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/ExtraFeatures.md) | [4359eb9](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/4359eb9b2df9f68f6f7001bbb7a9e028c76e3baa) | `b56b8d971e233654693831b4a8ad8590dcfc93633d9b8202c8a6a73660439ea2` |
| RandomEvents | [1.0.22](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/RandomEvents/v1.0.22) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FRandomEvents.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/RandomEvents.md) | [905ac74](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/905ac7492dfc502d3892b0e1f9becfe31febe0c0) | `19f7985a813f808a1040c162dff4d6f383f3177837e366bf552e942d8358cdc1` |
| CastlePlanner | [0.4.7](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/CastlePlanner/v0.4.7) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FCastlePlanner.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/CastlePlanner.md) | [cfadf89](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/cfadf899d2bf9118109fc970f657d3b554c5f101) | `9db88493f7b235c61e3400f4605b506ab06772c98a230137fe9b4de38d4c22a3` |
| StartConditions | [1.0.16](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/StartConditions/v1.0.16) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FStartConditions.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/StartConditions.md) | [cc57413](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/cc57413132fac92915a0407d8b7cfb2c8f99b539) | `a613fafa1bc583d4d94943e3acbd35ad3a2f754dd9306f8728b8971c659b2483` |
| UnitCosts | [1.0.16](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/UnitCosts/v1.0.16) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FUnitCosts.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/UnitCosts.md) | [ef98f18](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/ef98f186af158e4668ea16684dcb57d5c4cf5a71) | `ceecc14e5e481e126246e97ccfc949ae0014ba26e5603b29ea60be6d8584d50c` |
| UnitLimit | [1.0.83](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/UnitLimit/v1.0.83) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FUnitLimit.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/UnitLimit.md) | [ef854b5](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/ef854b58e94a7e01295d301781c1ab9103f08d81) | `dd5f6f9067d28ae0659fc8c9f1fc7110b2a675f8674d213411fc43954a4c1438` |

Verify a downloaded archive with `Get-FileHash <archive.zip> -Algorithm SHA256` and compare it with the release asset and table above.
<!-- RELEASE-INDEX:END -->

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
