#!/usr/bin/env bash

set -u

die() {
    printf 'FAIL: %s\n' "$*" >&2
    exit 1
}

[[ $# -eq 1 ]] || die 'Expected the LinuxModding source directory as the only argument.'
SOURCE_DIR=$(cygpath -u "$1") || die 'Could not translate the source path for Git Bash.'
SOURCE_DIR=$(CDPATH= cd -- "$SOURCE_DIR" && pwd -P) || die 'Source directory does not exist.'
SOURCE_LAUNCHER="$SOURCE_DIR/shcde-linux-launcher.sh"
FAKE_GAME="$SOURCE_DIR/tests/fake-game.sh"

[[ -f "$SOURCE_LAUNCHER" ]] || die "Launcher missing: $SOURCE_LAUNCHER"
[[ -f "$FAKE_GAME" ]] || die "Fake game missing: $FAKE_GAME"

TEST_PARENT=$(CDPATH= cd -- "${TMPDIR:-/tmp}" && pwd -P) || die 'Could not resolve the temporary directory.'
TEST_ROOT=$(mktemp -d "$TEST_PARENT/shcde-linux-tests.XXXXXX") || die 'Could not create test root.'
TEST_ROOT=$(CDPATH= cd -- "$TEST_ROOT" && pwd -P) || die 'Could not resolve test root.'

cleanup() {
    case "$TEST_ROOT" in
        "$TEST_PARENT"/shcde-linux-tests.*)
            rm -rf -- "$TEST_ROOT"
            ;;
        *)
            printf 'Refusing to remove unexpected test path: %s\n' "$TEST_ROOT" >&2
            ;;
    esac
}
trap cleanup EXIT

create_fixture() {
    local name=$1
    local game="$TEST_ROOT/$name/Stronghold Crusader Definitive Edition"
    mkdir -p -- "$game/BepInEx/plugins/LinuxModding_Serp"
    mkdir -p -- "$game/BepInEx/plugins/000shcdese"
    mkdir -p -- "$game/BepInEx/core"
    mkdir -p -- "$game/_SE"
    printf 'test executable\n' > "$game/Stronghold Crusader Definitive Edition.exe"
    printf 'test proxy\n' > "$game/winhttp.dll"
    printf 'test native dependency\n' > "$game/msvcp140.dll"
    printf 'test core\n' > "$game/BepInEx/core/BepInEx.dll"
    printf 'test extender\n' > "$game/BepInEx/plugins/000shcdese/SHCDESE.dll"
    printf 'test plugin\n' > "$game/BepInEx/plugins/LinuxModding_Serp/LinuxModding.dll"
    cp -- "$SOURCE_LAUNCHER" "$game/BepInEx/plugins/LinuxModding_Serp/shcde-linux-launcher.sh"
    cp -- "$SOURCE_DIR/install-linux.sh" "$game/BepInEx/plugins/LinuxModding_Serp/install-linux.sh"
    cp -- "$SOURCE_DIR/README.md" "$game/BepInEx/plugins/LinuxModding_Serp/README.md"
    printf '%s\n' "$game"
}

run_launcher() {
    local game=$1
    local scenario=$2
    local state=$3
    FAKE_SCENARIO="$scenario" \
    FAKE_GAME_DIR="$game" \
    FAKE_STATE_DIR="$state" \
    FAKE_SOURCE_LAUNCHER="$SOURCE_LAUNCHER" \
        bash "$game/BepInEx/plugins/LinuxModding_Serp/shcde-linux-launcher.sh" \
        bash "$FAKE_GAME"
}

printf 'TEST 1/4: successful staging, deletion, self-update and restart\n'
SUCCESS_GAME=$(create_fixture success)
SUCCESS_STATE="$TEST_ROOT/success-state"
mkdir -p -- "$SUCCESS_GAME/BepInEx/plugins/TargetMod"
printf 'old plugin\n' > "$SUCCESS_GAME/BepInEx/plugins/TargetMod/old.dll"
printf '{"Version":"1.0.0"}\n' > "$SUCCESS_GAME/_SE/OldMap.json"
run_launcher "$SUCCESS_GAME" success "$SUCCESS_STATE" || die 'Successful update scenario returned an error.'
[[ -f "$SUCCESS_STATE/success" ]] || die 'The restarted fake game did not verify the deployed update.'
[[ $(<"$SUCCESS_STATE/invocations") == '2' ]] || die 'Successful update did not restart exactly once.'

printf 'TEST 2/4: unsafe deletion rejection and manifest rollback\n'
ROLLBACK_GAME=$(create_fixture rollback)
ROLLBACK_STATE="$TEST_ROOT/rollback-state"
printf '{"Version":"1.0.0"}\n' > "$ROLLBACK_GAME/_SE/Existing.json"
set +e
run_launcher "$ROLLBACK_GAME" rollback "$ROLLBACK_STATE"
ROLLBACK_STATUS=$?
set -e
[[ "$ROLLBACK_STATUS" -eq 1 ]] || die "Rollback scenario returned $ROLLBACK_STATUS instead of 1."
grep -q '"Version":"1.0.0"' "$ROLLBACK_GAME/_SE/Existing.json" || die 'Existing manifest was not restored.'
[[ ! -e "$ROLLBACK_GAME/_SE/FailedMap.json" ]] || die 'New premature manifest was not removed during rollback.'
[[ -e "$ROLLBACK_GAME/_SE/.staging" ]] || die 'Failed staging was unexpectedly discarded.'
[[ -e "$ROLLBACK_GAME/_SE/.linux-compat-update-request" ]] || die 'Failed update request was unexpectedly discarded.'
[[ $(<"$ROLLBACK_STATE/invocations") == '1' ]] || die 'Game restarted after a failed deployment.'

printf 'TEST 3/4: normal game exit status passthrough\n'
NORMAL_GAME=$(create_fixture normal)
NORMAL_STATE="$TEST_ROOT/normal-state"
set +e
run_launcher "$NORMAL_GAME" normal "$NORMAL_STATE"
NORMAL_STATUS=$?
set -e
[[ "$NORMAL_STATUS" -eq 23 ]] || die "Normal exit status changed from 23 to $NORMAL_STATUS."
[[ $(<"$NORMAL_STATE/invocations") == '1' ]] || die 'Normal game exit unexpectedly restarted.'

printf 'TEST 4/4: installer verification and launch-option output\n'
INSTALL_GAME=$(create_fixture installer)
INSTALL_OUTPUT=$(bash "$INSTALL_GAME/BepInEx/plugins/LinuxModding_Serp/install-linux.sh") || die 'Installer rejected a complete fixture.'
grep -Fq 'bash "./BepInEx/plugins/LinuxModding_Serp/shcde-linux-launcher.sh" %command%' <<< "$INSTALL_OUTPUT" || die 'Installer did not print the expected Steam launch option.'

printf 'PASS: all Windows/Git-Bash launcher tests succeeded.\n'
