#!/usr/bin/env bash

set -u

: "${FAKE_STATE_DIR:?FAKE_STATE_DIR is required}"
mkdir -p -- "$FAKE_STATE_DIR"

case ";${WINEDLLOVERRIDES-};" in
    *';winhttp=n,b;'*) ;;
    *) exit 82 ;;
esac

if [[ "${FAKE_REQUIRE_EXISTING-0}" == '1' ]]; then
    case ";${WINEDLLOVERRIDES-};" in
        *';dxgi=n,b;'*) ;;
        *) exit 83 ;;
    esac
fi

COUNT_FILE="$FAKE_STATE_DIR/invocations"
COUNT=0
if [[ -f "$COUNT_FILE" ]]; then
    read -r COUNT < "$COUNT_FILE"
fi
printf '%s\n' "$((COUNT + 1))" > "$COUNT_FILE"
exit 23
