# Contributing to APIShared

APIShared is maintained independently of the SerpsMods workspace. Open an issue
with a reproducible problem or submit a pull request against `main`. Explain the
behavior changed, the public contract affected, and the checks you ran. Community
changes are reviewed and explicitly imported into the mod workspace.

Install Stronghold Crusader Definitive Edition, BepInEx and the Script Extender.
Use Visual Studio Build Tools with the .NET Framework 4.8.1 targeting pack and
the .NET 10 SDK for the isolated UnitAccess tests. No SerpsModsHost or mod workspace
checkout is needed. Do not upload game assemblies or installed dependencies.

Set `SHCDE_GAME_DIR` to your installation directory. Optional overrides are
`SHCDESE_EXTENDER_DIR` and `SHCDE_MSBUILD`. From an elevated PowerShell session:

```powershell
& '.\build.bat' /nopause /noinstall
```

Omit `/noinstall` to install the tested package. Close the game first. The driver
runs runtime/dependency/CRLF/XAML/interop checks, API and preset regressions,
UnitAccess tests, and public consumer examples before packaging.

Keep generated binaries, proprietary assemblies and local configuration out of
Git. Public consumer references use `Private=false`. Keep versions unchanged
while a change is being tested. See [development rules](AGENTS.md),
[architecture](ARCHITECTURE.md) and [API contracts](docs/API_CATALOG.md).

Internal helpers with historical workspace provenance are independently owned
here. A mod-only feature does not belong in these helpers. Review fixes to common
basic algorithms separately for each implementation; there is no automatic sync.

Preserve serialized settings, registration IDs, runtime lifetime and native hook
contracts. Changes to native behavior require a complete feature-specific audit
against the selected installed game and hook backend, followed by appropriate
tests. Clearly identify game tests that have not yet been performed.
