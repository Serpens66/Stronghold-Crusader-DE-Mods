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
MIN_EXTENDER_VERSION="2.2.0"
ERRORS=0

check_file() {
    if [[ -f "$1" ]]; then
        printf '[OK] %s\n' "$2"
    else
        printf '[MISSING] %s: %s\n' "$2" "$1"
        ERRORS=$((ERRORS + 1))
    fi
}

version_at_least() {
    local actual=$1 minimum=$2
    local actual_major actual_minor actual_patch minimum_major minimum_minor minimum_patch

    IFS=. read -r actual_major actual_minor actual_patch <<< "$actual"
    IFS=. read -r minimum_major minimum_minor minimum_patch <<< "$minimum"
    [[ "$actual_major" =~ ^[0-9]+$ && "$actual_minor" =~ ^[0-9]+$ && "$actual_patch" =~ ^[0-9]+$ ]] || return 1

    (( actual_major > minimum_major )) ||
        (( actual_major == minimum_major && actual_minor > minimum_minor )) ||
        (( actual_major == minimum_major && actual_minor == minimum_minor && actual_patch >= minimum_patch ))
}

printf 'SHCDE Linux/Proton setup check (latest Script Extender, minimum %s)\n\n' "$MIN_EXTENDER_VERSION"
check_file "$GAME_DIR/winhttp.dll" 'BepInEx proxy (winhttp.dll)'
check_file "$GAME_DIR/BepInEx/core/BepInEx.dll" 'BepInEx core'
check_file "$EXTENDER_DIR/SHCDESE.dll" 'SHCDE Script Extender'
check_file "$EXTENDER_DIR/info.json" 'SHCDE Script Extender manifest'
check_file "$EXTENDER_DIR/data/mod-updater.sh" 'official Script Extender shell updater'
check_file "$EXTENDER_DIR/libredbird_thread_patch.so" 'RedBird Proton thread patch'
check_file "$TOOL_DIR/shcde-linux-launcher.sh" 'winhttp-only compatibility launcher'

if [[ -f "$EXTENDER_DIR/info.json" ]]; then
    MANIFEST_VERSION=$(sed -nE 's/.*"Version"[[:space:]]*:[[:space:]]*"([0-9]+\.[0-9]+\.[0-9]+)".*/\1/p' "$EXTENDER_DIR/info.json" | head -n 1)
    if [[ -z "$MANIFEST_VERSION" ]] || ! version_at_least "$MANIFEST_VERSION" "$MIN_EXTENDER_VERSION"; then
        printf '[WRONG VERSION] SHCDE Script Extender must be %s or newer; found %s.\n' "$MIN_EXTENDER_VERSION" "${MANIFEST_VERSION:-unknown}"
        ERRORS=$((ERRORS + 1))
    else
        printf '[OK] SHCDE Script Extender version: %s\n' "$MANIFEST_VERSION"
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
