#!/usr/bin/env bash

set -u

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P) || exit 1

if [[ -f "$SCRIPT_DIR/Stronghold Crusader Definitive Edition.exe" ]]; then
    GAME_DIR="$SCRIPT_DIR"
elif [[ -f "$SCRIPT_DIR/../Stronghold Crusader Definitive Edition.exe" ]]; then
    GAME_DIR=$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P) || exit 1
elif [[ -f "$SCRIPT_DIR/../../../Stronghold Crusader Definitive Edition.exe" ]]; then
    GAME_DIR=$(CDPATH= cd -- "$SCRIPT_DIR/../../.." && pwd -P) || exit 1
else
    printf 'ERROR: Stronghold Crusader Definitive Edition.exe was not found.\n' >&2
    printf 'Copy this helper to BepInEx/tools/LinuxModding/ and run it again.\n' >&2
    exit 1
fi

TOOL_DIR="$GAME_DIR/BepInEx/tools/LinuxModding"
EXTENDER_DIR="$GAME_DIR/BepInEx/plugins/000shcdese"
ERRORS=0

check_file() {
    if [[ -f "$1" ]]; then
        printf '[OK] %s\n' "$2"
    else
        printf '[MISSING] %s: %s\n' "$2" "$1"
        ERRORS=$((ERRORS + 1))
    fi
}

printf 'SHCDE Linux/Proton setup check (latest Script Extender)\n\n'
check_file "$GAME_DIR/winhttp.dll" 'BepInEx proxy (winhttp.dll)'
check_file "$GAME_DIR/BepInEx/core/BepInEx.dll" 'BepInEx core'
check_file "$EXTENDER_DIR/SHCDESE.dll" 'SHCDE Script Extender'
check_file "$EXTENDER_DIR/info.json" 'SHCDE Script Extender manifest'
check_file "$EXTENDER_DIR/data/mod-updater.sh" 'official Script Extender shell updater'
check_file "$EXTENDER_DIR/libredbird_thread_patch.so" 'RedBird Proton thread patch'
check_file "$TOOL_DIR/shcde-linux-launcher.sh" 'winhttp-only compatibility launcher'

if [[ -f "$EXTENDER_DIR/info.json" ]]; then
    MANIFEST_VERSION=$(sed -nE 's/.*"Version"[[:space:]]*:[[:space:]]*"([0-9]+\.[0-9]+\.[0-9]+)".*/\1/p' "$EXTENDER_DIR/info.json" | head -n 1)
    if [[ -n "$MANIFEST_VERSION" ]]; then
        printf '[OK] SHCDE Script Extender version: %s\n' "$MANIFEST_VERSION"
    else
        printf '[INFO] SHCDE Script Extender version is not declared; no local version constraint is applied.\n'
    fi
fi

printf '\n'
if [[ "$ERRORS" -ne 0 ]]; then
    printf 'Installation is incomplete (%d problem(s)).\n' "$ERRORS" >&2
    exit 1
fi

printf 'The official Script Extender installation looks complete.\n'
printf 'This helper installs no plugin and replaces no updater; Script Extender owns the complete update process.\n'
printf 'Set this exact Steam launch option for the game:\n\n'
printf 'bash "./BepInEx/tools/LinuxModding/shcde-linux-launcher.sh" %%command%%\n\n'
