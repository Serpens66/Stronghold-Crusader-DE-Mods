#!/usr/bin/env bash

set -u

timestamp() {
    date '+%H:%M:%S.%3N'
}

log() {
    printf '[%s] LinuxModding: %s\n' "$(timestamp)" "$*"
}

fail() {
    log "ERROR: $*" >&2
    exit 1
}

if [[ "${SHCDE_LINUX_COMPAT_REEXECUTED-}" != '1' ]]; then
    ORIGINAL_SCRIPT_DIR=$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P) || exit 1
    TEMP_LAUNCHER=$(mktemp "${TMPDIR:-/tmp}/shcde-linux-launcher.XXXXXX") || exit 1
    cp -- "${BASH_SOURCE[0]}" "$TEMP_LAUNCHER" || exit 1
    export SHCDE_LINUX_COMPAT_REEXECUTED=1
    export SHCDE_LINUX_COMPAT_ORIGINAL_SCRIPT_DIR="$ORIGINAL_SCRIPT_DIR"
    export SHCDE_LINUX_COMPAT_TEMP_LAUNCHER="$TEMP_LAUNCHER"
    exec bash "$TEMP_LAUNCHER" "$@"
fi

SCRIPT_DIR=${SHCDE_LINUX_COMPAT_ORIGINAL_SCRIPT_DIR:?Missing original launcher directory}
TEMP_LAUNCHER=${SHCDE_LINUX_COMPAT_TEMP_LAUNCHER:?Missing temporary launcher path}
trap 'rm -f -- "$TEMP_LAUNCHER"' EXIT
GAME_DIR=$(CDPATH= cd -- "$SCRIPT_DIR/../../.." && pwd -P) || exit 1
SE_DIR="$GAME_DIR/_SE"
STAGING_DIR="$SE_DIR/.staging"
REQUEST_FILE="$SE_DIR/.linux-compat-update-request"
DELETE_MANIFEST="$STAGING_DIR/delete_list.txt"
GAME_EXE="$GAME_DIR/Stronghold Crusader Definitive Edition.exe"

[[ -f "$GAME_EXE" ]] || fail "Game executable not found. Keep this launcher in BepInEx/plugins/LinuxModding_Serp/."
[[ $# -gt 0 ]] || fail "No Steam game command received. Use the launch option printed by install-linux.sh."

case ";${WINEDLLOVERRIDES-};" in
    *';winhttp.dll='*) ;;
    *) export WINEDLLOVERRIDES="winhttp.dll=n,b${WINEDLLOVERRIDES:+;$WINEDLLOVERRIDES}" ;;
esac
export SHCDE_LINUX_COMPAT_LAUNCHER=1

snapshot_manifests() {
    MANIFEST_BACKUP=$(mktemp -d "${TMPDIR:-/tmp}/shcde-linux-metadata.XXXXXX") || return 1
    mkdir -p -- "$SE_DIR" || return 1
    find "$SE_DIR" -maxdepth 1 -type f -name '*.json' -exec cp -p -- '{}' "$MANIFEST_BACKUP/" \; || return 1
}

restore_manifests() {
    find "$SE_DIR" -maxdepth 1 -type f -name '*.json' -delete
    cp -p -- "$MANIFEST_BACKUP"/*.json "$SE_DIR/" 2>/dev/null || true
}

cleanup_snapshot() {
    rm -rf -- "$MANIFEST_BACKUP"
}

safe_delete_manifest_targets() {
    [[ -f "$DELETE_MANIFEST" ]] || return 0

    local raw normalized relative target
    while IFS= read -r raw || [[ -n "$raw" ]]; do
        normalized=${raw//$'\r'/}
        normalized=${normalized//\\//}
        [[ -n "$normalized" ]] || continue

        case "$normalized" in
            */BepInEx/plugins/*)
                relative=${normalized##*/BepInEx/plugins/}
                [[ -n "$relative" && "$relative" != */* && "$relative" != '.' && "$relative" != '..' ]] || {
                    log "Rejected unsafe plugin deletion entry: $raw"
                    return 1
                }
                target="$GAME_DIR/BepInEx/plugins/$relative"
                ;;
            */_SE/*.json)
                relative=${normalized##*/_SE/}
                [[ -n "$relative" && "$relative" != */* && "$relative" != '.' && "$relative" != '..' ]] || {
                    log "Rejected unsafe manifest deletion entry: $raw"
                    return 1
                }
                target="$SE_DIR/$relative"
                ;;
            *)
                log "Rejected deletion outside the supported game folders: $raw"
                return 1
                ;;
        esac

        if [[ -e "$target" || -L "$target" ]]; then
            log "Removing $target"
            rm -rf -- "$target" || return 1
        fi
    done < "$DELETE_MANIFEST"
}

apply_staged_update() {
    [[ -d "$STAGING_DIR" ]] || {
        log "Update request exists, but the staging directory is missing."
        return 1
    }

    safe_delete_manifest_targets || return 1
    rm -f -- "$DELETE_MANIFEST" || return 1

    log "Applying staged Workshop files."
    cp -a -- "$STAGING_DIR"/. "$GAME_DIR"/ || return 1
    rm -rf -- "$STAGING_DIR" || return 1
    rm -f -- "$REQUEST_FILE" || return 1
    return 0
}

while true; do
    snapshot_manifests || fail "Could not snapshot Script Extender metadata."

    log "Starting Stronghold Crusader Definitive Edition through Steam/Proton."
    "$@"
    GAME_STATUS=$?

    if [[ ! -f "$REQUEST_FILE" ]]; then
        cleanup_snapshot
        exit "$GAME_STATUS"
    fi

    log "The Script Extender requested deployment of a Workshop update."
    if ! apply_staged_update; then
        restore_manifests
        cleanup_snapshot
        fail "Workshop deployment failed. Metadata was restored and the game was not restarted."
    fi

    cleanup_snapshot
    log "Workshop update applied successfully; restarting the game."
done
