#!/usr/bin/env bash
# Einmalig ausführbar machen: chmod +x "SCDE Vanilla.sh"
set -u

APP_ID="3024040"
GAME_NAME="Stronghold Crusader Definitive Edition"
EXE_NAME="$GAME_NAME.exe"
SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
GAME_DIR=""

accept_game_dir() {
    local candidate="$1"
    if [[ -f "$candidate/$EXE_NAME" ]]; then
        GAME_DIR="$candidate"
        return 0
    fi
    return 1
}

check_steam_root() {
    local steam_root="$1" library_file path
    [[ -d "$steam_root" ]] || return 1
    accept_game_dir "$steam_root/steamapps/common/$GAME_NAME" && return 0

    library_file="$steam_root/steamapps/libraryfolders.vdf"
    [[ -f "$library_file" ]] || return 1
    while IFS= read -r path; do
        path="${path//\\\\/\\}"
        accept_game_dir "$path/steamapps/common/$GAME_NAME" && return 0
    done < <(sed -nE 's/^[[:space:]]*"path"[[:space:]]*"(.*)"[[:space:]]*$/\1/p' "$library_file")
    return 1
}

find_game() {
    local steam_root
    [[ "$(uname -s)" == Linux* ]] || return 1

    for steam_root in "$HOME/.steam/root" "$HOME/.local/share/Steam" "$HOME/.var/app/com.valvesoftware.Steam/data/Steam"; do
        check_steam_root "$steam_root" && return 0
    done
    accept_game_dir "$SCRIPT_DIR"
}

launch_game() {
    if command -v steam >/dev/null 2>&1; then
        steam -applaunch "$APP_ID" >/dev/null 2>&1 &
    elif command -v flatpak >/dev/null 2>&1 && flatpak info com.valvesoftware.Steam >/dev/null 2>&1; then
        flatpak run com.valvesoftware.Steam -applaunch "$APP_ID" >/dev/null 2>&1 &
    else
        printf 'Steam wurde nicht gefunden.\n' >&2
        return 1
    fi
}

if ! find_game; then
    printf '%s wurde nicht gefunden.\n' "$GAME_NAME" >&2
    printf 'Lege dieses Skript in den Spielordner oder prüfe deine Steam-Bibliothek.\n' >&2
    exit 1
fi

if [[ -e "$GAME_DIR/winhttp.dll" && -e "$GAME_DIR/winhttp.dll.mods-disabled" ]]; then
    printf 'Sowohl winhttp.dll als auch winhttp.dll.mods-disabled sind vorhanden.\n' >&2
    printf 'Aus Sicherheitsgründen wurde nichts umbenannt.\n' >&2
    exit 1
fi

if [[ -e "$GAME_DIR/winhttp.dll" ]]; then
    mv -- "$GAME_DIR/winhttp.dll" "$GAME_DIR/winhttp.dll.mods-disabled" || {
        printf 'winhttp.dll konnte nicht deaktiviert werden. Prüfe die Schreibrechte.\n' >&2
        exit 1
    }
fi

if [[ ! -e "$GAME_DIR/winhttp.dll.mods-disabled" ]]; then
    printf 'Weder winhttp.dll noch winhttp.dll.mods-disabled wurde in "%s" gefunden.\n' "$GAME_DIR" >&2
    exit 1
fi

launch_game
