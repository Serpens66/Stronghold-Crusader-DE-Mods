# Stronghold Crusader DE Mods and Code

## Download:
- Releases: https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases (see this [table](#latest-mod-releases) for specific mods)   
- Nexusmods: https://www.nexusmods.com/profile/Serpens66/mods?gameId=7959

### Trail (Un-)packer and modded/vanilla Launcher bat files:
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

| Mod | Latest release | Source commit | ZIP SHA-256 |
| --- | --- | --- | --- |
| BugfixesAndQoL | [1.0.13](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/BugfixesAndQoL/v1.0.13) | [2a690c1](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/2a690c15c2eacca74ab2d7b5717f3e65927a648e) | `d2b2fc75a1c9878d3a609108b4012f8a5f1bb0625c6fbb54b828582e3e12ba9e` |
| BuildingCosts | [1.0.8](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/BuildingCosts/v1.0.8) | [d295ae3](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/d295ae3123f95f533888b4fc31f58647cd285cee) | `e954b5f81f336453fe3680fccc0582ecf606749c297d4194c24d1ee6f91f7a51` |
| BuildingLimit | [1.0.7](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/BuildingLimit/v1.0.7) | [35f5fea](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/35f5fea09f2c5e45e938f24ad17ae1e8b086dd16) | `7c3a7134b0a5138efb5fe7ad8ced076f11fbbcfc6b6b4930a6969a86185dea02` |
| ExtraFeatures | [1.0.10](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/ExtraFeatures/v1.0.10) | [c109882](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/c1098821de5f1a9c50f483cd22ec8f72ff59e495) | `46f764a3b495666647c48425fdb511cddfafc90c4da3e9a060e813bfab90b8bd` |
| RandomEvents | [1.0.8](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/RandomEvents/v1.0.8) | [1cf3e58](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/1cf3e5834a660b1d348146e32b8efac00a910626) | `9c1d34c9fe6419a8713465a118897284bfd6612cb0aa78a160c3c6715d5b80e0` |
| SpawnCastle | [0.4.3](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/SpawnCastle/v0.4.3) | [6d0eb03](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/6d0eb037e460635a71bf1c2de185aa0518b795cb) | `cde627b18cfedf7e181f262884221732b48a4677496794a46be3f9eb4917bc7d` |
| StartConditions | [1.0.10](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/StartConditions/v1.0.10) | [95d36a3](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/95d36a387bbf5c57e896e37bd96f515d959b4dd9) | `18e6b6f47a8e07f2bc49fd8113dfb2fcc48ec48b5178d864901db6febb1c4c0a` |
| UnitCosts | [1.0.11](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/UnitCosts/v1.0.11) | [d839591](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/d83959131745162bbc23ec30fd79540b4a5cb15d) | `35acd59386fcc94913c9f358d8640a5dd04e7f993f50619a35e03286c5f9a9fd` |
| UnitLimit | [1.0.8](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/UnitLimit/v1.0.8) | [bf0ce94](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/bf0ce94056cc0b0eaadf41f41818461d1b71381c) | `5e16386cc9860833e2a28e2ae8abc98ca7eb185d6f03fa4ec0f7ba345229710f` |

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
