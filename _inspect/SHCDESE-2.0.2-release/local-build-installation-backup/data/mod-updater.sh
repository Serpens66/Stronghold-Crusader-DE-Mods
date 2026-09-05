#!/bin/sh
# Linux/Proton counterpart to mod-updater.ps1.
# Deliberately POSIX sh and deliberately boring (ayy)
set -eu

GAME_PROC=""; STAGING=""; TARGET=""; DELETE_MANIFEST=""; APPID="3024040"

while [ $# -gt 0 ]; do
  case "$1" in
    --proc)            GAME_PROC="$2";        shift 2 ;;
    --staging)         STAGING="$2";          shift 2 ;;
    --target)          TARGET="$2";           shift 2 ;;
    --delete-manifest) DELETE_MANIFEST="$2";  shift 2 ;;
    *) echo "unknown argument: $1" >&2; exit 2 ;;
  esac
done

[ -n "$TARGET" ] && [ -d "$TARGET" ] || { echo "target dir invalid" >&2; exit 2; }

# --- wait for the game to exit (name-based; the Wine PID is not our PID) ---
if [ -n "$GAME_PROC" ]; then
  i=0
  while [ "$i" -lt 120 ]; do
    pgrep -f "$GAME_PROC" >/dev/null 2>&1 || break
    sleep 1
    i=$((i + 1))
  done
fi
sleep 1

# --- containment guard: never delete outside the game directory ---
TARGET_REAL=$(cd "$TARGET" && pwd -P)

inside_target() {
  p=$1
  d=$(dirname "$p")
  [ -d "$d" ] || return 1
  d=$(cd "$d" && pwd -P)
  case "$d/" in
    "$TARGET_REAL"/*) return 0 ;;
    *) return 1 ;;
  esac
}

# --- uninstalls ---
if [ -n "$DELETE_MANIFEST" ] && [ -f "$DELETE_MANIFEST" ]; then
  while IFS= read -r victim; do
    [ -n "$victim" ] || continue
    [ -e "$victim" ] || continue
    if inside_target "$victim"; then
      echo "removing: $victim"
      rm -rf -- "$victim"
    else
      echo "REFUSED (outside game dir): $victim" >&2
    fi
  done < "$DELETE_MANIFEST"
  rm -f -- "$DELETE_MANIFEST"
fi

# --- apply staged files ---
if [ -d "$STAGING" ]; then
  ( cd "$STAGING" && find . -type f -print ) | while IFS= read -r rel; do
    rel=${rel#./}
    mkdir -p -- "$TARGET/$(dirname "$rel")"
    cp -f -- "$STAGING/$rel" "$TARGET/$rel"
    echo "updated: $rel"
  done
  rm -rf -- "$STAGING"
fi

# --- restart ---
if command -v xdg-open >/dev/null 2>&1; then
  xdg-open "steam://run/$APPID" >/dev/null 2>&1 &
elif command -v steam >/dev/null 2>&1; then
  steam -applaunch "$APPID" >/dev/null 2>&1 &
else
  echo "could not locate xdg-open or steam; restart manually" >&2
fi