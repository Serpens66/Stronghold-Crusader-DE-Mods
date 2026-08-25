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
    printf 'Extract the package into the game directory, then run this script again.\n' >&2
    exit 1
fi

PLUGIN_DIR="$GAME_DIR/BepInEx/plugins/LinuxModding_Serp"
LAUNCHER="$PLUGIN_DIR/shcde-linux-launcher.sh"
ERRORS=0

check_file() {
    if [[ -f "$1" ]]; then
        printf '[OK] %s\n' "$2"
    else
        printf '[MISSING] %s: %s\n' "$2" "$1"
        ERRORS=$((ERRORS + 1))
    fi
}

printf 'SHCDE Linux Modding compatibility check\n\n'
check_file "$GAME_DIR/winhttp.dll" 'BepInEx proxy (winhttp.dll)'
check_file "$GAME_DIR/BepInEx/core/BepInEx.dll" 'BepInEx core'
check_file "$GAME_DIR/BepInEx/plugins/000shcdese/SHCDESE.dll" 'SHCDE Script Extender'
check_file "$GAME_DIR/msvcp140.dll" 'Script Extender Proton dependency (msvcp140.dll)'
check_file "$PLUGIN_DIR/LinuxModding.dll" 'Linux compatibility plugin'
check_file "$LAUNCHER" 'Linux compatibility launcher'

printf '\n'
if [[ "$ERRORS" -ne 0 ]]; then
    printf 'Installation is incomplete (%d missing file(s)).\n' "$ERRORS" >&2
    exit 1
fi

printf 'Installation files look complete.\n'
printf 'Set this exact Steam launch option for the game:\n\n'
printf 'bash "./BepInEx/plugins/LinuxModding_Serp/shcde-linux-launcher.sh" %%command%%\n\n'
printf 'Steam launch options cannot be changed reliably by this script while Steam is running.\n'
