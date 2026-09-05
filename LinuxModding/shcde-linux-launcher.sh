#!/usr/bin/env bash

set -u

SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P) || exit 1
GAME_DIR=$(CDPATH= cd -- "$SCRIPT_DIR/../../.." && pwd -P) || exit 1
GAME_EXE="$GAME_DIR/Stronghold Crusader Definitive Edition.exe"

if [[ ! -f "$GAME_EXE" ]]; then
    printf 'ERROR: Stronghold Crusader Definitive Edition.exe was not found.\n' >&2
    printf 'Keep this helper in BepInEx/tools/LinuxModding/.\n' >&2
    exit 1
fi
if [[ $# -eq 0 ]]; then
    printf 'ERROR: No Steam game command received. Use the launch option printed by install-linux.sh.\n' >&2
    exit 1
fi

# The official Script Extender owns staging, deletion, waiting, and restart.
# This helper only makes BepInEx's winhttp proxy visible to Wine/Proton.
export WINEDLLOVERRIDES="winhttp=n,b${WINEDLLOVERRIDES:+;$WINEDLLOVERRIDES}"
exec "$@"
