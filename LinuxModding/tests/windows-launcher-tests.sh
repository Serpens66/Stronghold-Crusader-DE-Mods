#!/usr/bin/env bash

set -u

die() {
    printf 'FAIL: %s\n' "$*" >&2
    exit 1
}

[[ $# -eq 1 ]] || die 'Expected the LinuxModding helper directory as the only argument.'
SOURCE_DIR=$(cygpath -u "$1") || die 'Could not translate the source path for Git Bash.'
SOURCE_DIR=$(CDPATH= cd -- "$SOURCE_DIR" && pwd -P) || die 'Source directory does not exist.'
SOURCE_LAUNCHER="$SOURCE_DIR/shcde-linux-launcher.sh"
SOURCE_CHECKER="$SOURCE_DIR/install-linux.sh"
FAKE_GAME="$SOURCE_DIR/tests/fake-game.sh"

[[ -f "$SOURCE_LAUNCHER" ]] || die "Launcher missing: $SOURCE_LAUNCHER"
[[ -f "$SOURCE_CHECKER" ]] || die "Checker missing: $SOURCE_CHECKER"
[[ -f "$FAKE_GAME" ]] || die "Fake game missing: $FAKE_GAME"

TEST_PARENT=$(CDPATH= cd -- "${TMPDIR:-/tmp}" && pwd -P) || die 'Could not resolve the temporary directory.'
TEST_ROOT=$(mktemp -d "$TEST_PARENT/shcde-linux-helper-tests.XXXXXX") || die 'Could not create test root.'
TEST_ROOT=$(CDPATH= cd -- "$TEST_ROOT" && pwd -P) || die 'Could not resolve test root.'

cleanup() {
    case "$TEST_ROOT" in
        "$TEST_PARENT"/shcde-linux-helper-tests.*) rm -rf -- "$TEST_ROOT" ;;
        *) printf 'Refusing to remove unexpected test path: %s\n' "$TEST_ROOT" >&2 ;;
    esac
}
trap cleanup EXIT

create_fixture() {
    local name=$1
    local version=${2:-2.2.0}
    local game="$TEST_ROOT/$name/Stronghold Crusader Definitive Edition"
    mkdir -p -- "$game/BepInEx/tools/LinuxModding"
    mkdir -p -- "$game/BepInEx/plugins/000shcdese/data"
    mkdir -p -- "$game/BepInEx/core"
    printf 'test executable\n' > "$game/Stronghold Crusader Definitive Edition.exe"
    printf 'test proxy\n' > "$game/winhttp.dll"
    printf 'test core\n' > "$game/BepInEx/core/BepInEx.dll"
    printf 'test extender\n' > "$game/BepInEx/plugins/000shcdese/SHCDESE.dll"
    printf '{"Version":"%s"}\n' "$version" > "$game/BepInEx/plugins/000shcdese/info.json"
    printf '#!/usr/bin/env bash\n' > "$game/BepInEx/plugins/000shcdese/data/mod-updater.sh"
    printf 'test native patch\n' > "$game/BepInEx/plugins/000shcdese/libredbird_thread_patch.so"
    cp -- "$SOURCE_LAUNCHER" "$game/BepInEx/tools/LinuxModding/shcde-linux-launcher.sh"
    cp -- "$SOURCE_CHECKER" "$game/BepInEx/tools/LinuxModding/install-linux.sh"
    printf '%s\n' "$game"
}

run_launcher() {
    local game=$1
    local state=$2
    shift 2
    set +e
    FAKE_STATE_DIR="$state" "$@" bash "$game/BepInEx/tools/LinuxModding/shcde-linux-launcher.sh" bash "$FAKE_GAME"
    local status=$?
    set -e
    [[ "$status" -eq 23 ]] || die "Launcher changed the game exit status to $status."
    [[ $(<"$state/invocations") == '1' ]] || die 'Launcher did not execute the game exactly once.'
}

printf 'TEST 1/7: launcher sets only the required winhttp override and executes once\n'
BASIC_GAME=$(create_fixture basic)
BASIC_STATE="$TEST_ROOT/basic-state"
mkdir -p -- "$BASIC_STATE"
run_launcher "$BASIC_GAME" "$BASIC_STATE" env -u WINEDLLOVERRIDES

printf 'TEST 2/7: launcher preserves unrelated Wine overrides\n'
PRESERVE_GAME=$(create_fixture preserve)
PRESERVE_STATE="$TEST_ROOT/preserve-state"
mkdir -p -- "$PRESERVE_STATE"
run_launcher "$PRESERVE_GAME" "$PRESERVE_STATE" env WINEDLLOVERRIDES=dxgi=n,b FAKE_REQUIRE_EXISTING=1

printf 'TEST 3/7: checker accepts official 2.2.0 updater files\n'
CHECK_GAME=$(create_fixture checker)
CHECK_OUTPUT=$(bash "$CHECK_GAME/BepInEx/tools/LinuxModding/install-linux.sh") || die 'Checker rejected a complete official fixture.'
grep -Fq 'bash "./BepInEx/tools/LinuxModding/shcde-linux-launcher.sh" %command%' <<< "$CHECK_OUTPUT" || die 'Checker did not print the launcher-only Steam option.'
grep -Fq 'installs no plugin and replaces no updater' <<< "$CHECK_OUTPUT" || die 'Checker did not state its limited ownership.'

printf 'TEST 4/7: checker accepts a newer Script Extender version\n'
NEWER_GAME=$(create_fixture newer-version 2.3.0)
if ! bash "$NEWER_GAME/BepInEx/tools/LinuxModding/install-linux.sh" >/dev/null; then
    die 'Checker rejected a newer Script Extender version.'
fi

printf 'TEST 5/7: checker rejects an Extender older than 2.2.0\n'
WRONG_GAME=$(create_fixture wrong-version 2.0.2)
if bash "$WRONG_GAME/BepInEx/tools/LinuxModding/install-linux.sh" >/dev/null 2>&1; then
    die 'Checker accepted the wrong Script Extender version.'
fi

printf 'TEST 6/7: checker rejects a missing official Unix updater\n'
NO_UPDATER_GAME=$(create_fixture no-updater)
rm -- "$NO_UPDATER_GAME/BepInEx/plugins/000shcdese/data/mod-updater.sh"
if bash "$NO_UPDATER_GAME/BepInEx/tools/LinuxModding/install-linux.sh" >/dev/null 2>&1; then
    die 'Checker accepted an installation without the official Unix updater.'
fi

printf 'TEST 7/7: checker rejects a missing RedBird Proton thread patch\n'
NO_PATCH_GAME=$(create_fixture no-thread-patch)
rm -- "$NO_PATCH_GAME/BepInEx/plugins/000shcdese/libredbird_thread_patch.so"
if bash "$NO_PATCH_GAME/BepInEx/tools/LinuxModding/install-linux.sh" >/dev/null 2>&1; then
    die 'Checker accepted an installation without the RedBird Proton thread patch.'
fi

printf 'PASS: all launcher-only Windows/Git-Bash tests succeeded.\n'
