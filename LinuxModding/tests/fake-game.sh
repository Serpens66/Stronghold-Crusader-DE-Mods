#!/usr/bin/env bash

set -u

: "${FAKE_SCENARIO:?FAKE_SCENARIO is required}"
: "${FAKE_GAME_DIR:?FAKE_GAME_DIR is required}"
: "${FAKE_STATE_DIR:?FAKE_STATE_DIR is required}"

mkdir -p -- "$FAKE_STATE_DIR"
COUNT_FILE="$FAKE_STATE_DIR/invocations"
COUNT=0
if [[ -f "$COUNT_FILE" ]]; then
    read -r COUNT < "$COUNT_FILE"
fi
COUNT=$((COUNT + 1))
printf '%s\n' "$COUNT" > "$COUNT_FILE"

[[ "${SHCDE_LINUX_COMPAT_LAUNCHER-}" == '1' ]] || exit 81
case ";${WINEDLLOVERRIDES-};" in
    *';winhttp.dll=n,b;'*) ;;
    *) exit 82 ;;
esac

SE_DIR="$FAKE_GAME_DIR/_SE"
STAGING_DIR="$SE_DIR/.staging"
REQUEST_FILE="$SE_DIR/.linux-compat-update-request"

case "$FAKE_SCENARIO" in
    success)
        if [[ "$COUNT" -eq 1 ]]; then
            mkdir -p -- "$STAGING_DIR/BepInEx/plugins/TargetMod"
            mkdir -p -- "$STAGING_DIR/BepInEx/plugins/LinuxModding_Serp"
            printf 'new plugin\n' > "$STAGING_DIR/BepInEx/plugins/TargetMod/new.dll"
            cp -- "$FAKE_SOURCE_LAUNCHER" "$STAGING_DIR/BepInEx/plugins/LinuxModding_Serp/shcde-linux-launcher.sh"
            printf '\n# staged self-update marker\n' >> "$STAGING_DIR/BepInEx/plugins/LinuxModding_Serp/shcde-linux-launcher.sh"
            printf '{"Version":"2.0.0"}\n' > "$SE_DIR/NewMap.json"

            local_plugin="$FAKE_GAME_DIR/BepInEx/plugins/TargetMod"
            local_manifest="$SE_DIR/OldMap.json"
            windows_plugin="Z:${local_plugin//\//\\}"
            windows_manifest="Z:${local_manifest//\//\\}"
            printf '%s\r\n%s\r\n' "$windows_plugin" "$windows_manifest" > "$STAGING_DIR/delete_list.txt"
            printf 'protocol=1\n' > "$REQUEST_FILE"
            exit 137
        fi

        [[ "$COUNT" -eq 2 ]] || exit 83
        [[ -f "$FAKE_GAME_DIR/BepInEx/plugins/TargetMod/new.dll" ]] || exit 84
        [[ ! -f "$FAKE_GAME_DIR/BepInEx/plugins/TargetMod/old.dll" ]] || exit 85
        [[ ! -f "$SE_DIR/OldMap.json" ]] || exit 86
        [[ -f "$SE_DIR/NewMap.json" ]] || exit 87
        [[ ! -e "$STAGING_DIR" ]] || exit 88
        [[ ! -e "$REQUEST_FILE" ]] || exit 89
        grep -q 'staged self-update marker' "$FAKE_GAME_DIR/BepInEx/plugins/LinuxModding_Serp/shcde-linux-launcher.sh" || exit 90
        printf 'verified\n' > "$FAKE_STATE_DIR/success"
        exit 0
        ;;
    rollback)
        [[ "$COUNT" -eq 1 ]] || exit 91
        mkdir -p -- "$STAGING_DIR/BepInEx/plugins/FailedMod"
        printf 'partial update\n' > "$STAGING_DIR/BepInEx/plugins/FailedMod/plugin.dll"
        printf '{"Version":"999.0.0"}\n' > "$SE_DIR/Existing.json"
        printf '{"Version":"1.0.0"}\n' > "$SE_DIR/FailedMap.json"
        printf 'C:\\outside\\must-not-delete\r\n' > "$STAGING_DIR/delete_list.txt"
        printf 'protocol=1\n' > "$REQUEST_FILE"
        exit 137
        ;;
    normal)
        exit 23
        ;;
    *)
        exit 92
        ;;
esac
