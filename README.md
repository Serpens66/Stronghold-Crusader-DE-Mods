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
| BugfixesAndQoL | [1.0.20](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/BugfixesAndQoL/v1.0.20) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FBugfixesAndQoL.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/BugfixesAndQoL.md) | [24dcc84](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/24dcc841fb4245c6c65b8b97b2cc82dbe03c3029) | `d37336cc936f24c83a09be31d4673c946b954766d4d0bca3d0933122248ace94` |
| BuildingCosts | [1.0.9](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/BuildingCosts/v1.0.9) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FBuildingCosts.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/BuildingCosts.md) | [dffef76](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/dffef763e76a07ad288b89bdffcd2d891b599677) | `6b47bdd56f1abb40b6ca29d4da6fd1419d127efb83fb09e478f310085aad6f3d` |
| BuildingLimit | [1.0.8](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/BuildingLimit/v1.0.8) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FBuildingLimit.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/BuildingLimit.md) | [ff54ce4](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/ff54ce47e85252f0b87482de557df5a19ab7259e) | `54bbf163c0e1ff7d2c886b3aa0e0a9a867338023265c91e09e51220a73f7da3b` |
| ExtraFeatures | [1.0.14](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/ExtraFeatures/v1.0.14) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FExtraFeatures.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/ExtraFeatures.md) | [db47e8c](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/db47e8c54be1af8eb9a92691d7b2820fec66521f) | `6668fe88cab9f98cdff5f05adddf9008b28c28b16e5454cbfb88b33d59aab83c` |
| RandomEvents | [1.0.9](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/RandomEvents/v1.0.9) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FRandomEvents.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/RandomEvents.md) | [5e5aafb](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/5e5aafb4dc48bfb069f61dd38ee28c6d4770a02b) | `9c526d50f7bb1c172785a5f13c85608f4f95cb888d69cdf1eaec2633a5471339` |
| StartConditions | [1.0.11](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/StartConditions/v1.0.11) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FStartConditions.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/StartConditions.md) | [079fc38](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/079fc3807ad1ce4eab974b2911c60f5f16328e34) | `f31c5e951bc1ee3d89ca405a31601ec059d83b9f6654cf7afc87ba62681fe5a2` |
| UnitCosts | [1.0.12](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/UnitCosts/v1.0.12) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FUnitCosts.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/UnitCosts.md) | [fc1ab47](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/fc1ab475a1588f25dc49f4cafd4bf2a669a1b0f2) | `dc901a04f21a51a7dca22472c77336c53f53ff3f3dd8f7f72f96b3540f09421a` |
| UnitLimit | [1.0.81](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/releases/tag/UnitLimit/v1.0.81) | [![release status](https://img.shields.io/endpoint?url=https%3A%2F%2Fraw.githubusercontent.com%2FSerpens66%2FStronghold-Crusader-DE-Mods%2Frelease-status%2Fbadges%2FUnitLimit.json&cacheSeconds=300)](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/blob/release-status/reports/UnitLimit.md) | [dfaef21](https://github.com/Serpens66/Stronghold-Crusader-DE-Mods/commit/dfaef21f04d9857aec4f49051973d327e44d1ad9) | `5dcd06fc86b8a595c6c4d2b8d5a20f76491386f0355a72c5e04ace26204b4282` |

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
