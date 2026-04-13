#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/../.." && pwd)"
PROJECT="$REPO_ROOT/samples/ShellSample/ShellSample.csproj"
CONFIGURATION="${CONFIGURATION:-Debug}"
TARGET_FRAMEWORK="${TARGET_FRAMEWORK:-net10.0-maccatalyst}"
SMOKE_SECONDS="${SMOKE_SECONDS:-20}"
APP_NAME="${APP_NAME:-ShellSample}"
LOG_DIR="${TMPDIR:-/tmp}/memorytoolkit-smoke"
LOG_FILE="$LOG_DIR/maccatalyst-smoke-$$.log"

mkdir -p "$LOG_DIR"

dotnet build "$PROJECT" -c "$CONFIGURATION" -f "$TARGET_FRAMEWORK"

APP_ROOT="$REPO_ROOT/samples/ShellSample/bin/$CONFIGURATION/$TARGET_FRAMEWORK"
APP_BUNDLE="$(find "$APP_ROOT" -type d -name "$APP_NAME.app" -print -quit)"

if [[ -z "$APP_BUNDLE" ]]; then
    echo "Could not find $APP_NAME.app under $APP_ROOT." >&2
    exit 1
fi

open -n -W "$APP_BUNDLE" >"$LOG_FILE" 2>&1 &
LAUNCHER_PID=$!

cleanup() {
    pkill -x "$APP_NAME" >/dev/null 2>&1 || true

    if kill -0 "$LAUNCHER_PID" >/dev/null 2>&1; then
        kill "$LAUNCHER_PID" >/dev/null 2>&1 || true
        wait "$LAUNCHER_PID" >/dev/null 2>&1 || true
    fi
}

trap cleanup EXIT

sleep "$SMOKE_SECONDS"

if ! kill -0 "$LAUNCHER_PID" >/dev/null 2>&1; then
    set +e
    wait "$LAUNCHER_PID"
    EXIT_CODE=$?
    set -e

    echo "Mac Catalyst smoke app exited before ${SMOKE_SECONDS}s with exit code $EXIT_CODE." >&2
    echo "Smoke log: $LOG_FILE" >&2
    tail -n 200 "$LOG_FILE" >&2 || true
    exit 1
fi

echo "Mac Catalyst smoke app stayed alive for ${SMOKE_SECONDS}s."
echo "Smoke log: $LOG_FILE"
