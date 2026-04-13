#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/../.." && pwd)"
PROJECT="$REPO_ROOT/tests/MemoryToolkit.Maui.E2ETests/MemoryToolkit.Maui.E2ETests.csproj"
CONFIGURATION="${CONFIGURATION:-Debug}"
TARGET_FRAMEWORK="${TARGET_FRAMEWORK:-net10.0-maccatalyst}"
APP_BUNDLE_NAME="${APP_BUNDLE_NAME:-MemoryToolkit E2E Tests}"
APP_PROCESS_NAME="${APP_PROCESS_NAME:-MemoryToolkit.Maui.E2ETests}"
TIMEOUT_SECONDS="${TIMEOUT_SECONDS:-120}"
LOG_DIR="${TMPDIR:-/tmp}/memorytoolkit-e2e"
REPORT_FILE="$LOG_DIR/maccatalyst-e2e-$$.json"
STDOUT_LOG="$LOG_DIR/maccatalyst-e2e-$$.stdout.log"
STDERR_LOG="$LOG_DIR/maccatalyst-e2e-$$.stderr.log"

mkdir -p "$LOG_DIR"

dotnet build "$PROJECT" -c "$CONFIGURATION" -f "$TARGET_FRAMEWORK"

APP_ROOT="$REPO_ROOT/tests/MemoryToolkit.Maui.E2ETests/bin/$CONFIGURATION/$TARGET_FRAMEWORK"
APP_BUNDLE="$(find "$APP_ROOT" -type d -name "$APP_BUNDLE_NAME.app" -print -quit)"

if [[ -z "$APP_BUNDLE" ]]; then
    echo "Could not find $APP_BUNDLE_NAME.app under $APP_ROOT." >&2
    exit 1
fi

open -n -W \
    --stdout "$STDOUT_LOG" \
    --stderr "$STDERR_LOG" \
    --env "MEMORYTOOLKIT_E2E_EXIT=1" \
    --env "MEMORYTOOLKIT_E2E_OUTPUT=$REPORT_FILE" \
    "$APP_BUNDLE" &
LAUNCHER_PID=$!

cleanup() {
    pkill -x "$APP_PROCESS_NAME" >/dev/null 2>&1 || true

    if kill -0 "$LAUNCHER_PID" >/dev/null 2>&1; then
        kill "$LAUNCHER_PID" >/dev/null 2>&1 || true
        wait "$LAUNCHER_PID" >/dev/null 2>&1 || true
    fi
}

trap cleanup EXIT

for _ in $(seq 1 "$TIMEOUT_SECONDS"); do
    if ! kill -0 "$LAUNCHER_PID" >/dev/null 2>&1; then
        break
    fi

    sleep 1
done

if kill -0 "$LAUNCHER_PID" >/dev/null 2>&1; then
    echo "Mac Catalyst e2e app did not finish within ${TIMEOUT_SECONDS}s." >&2
    echo "stdout: $STDOUT_LOG" >&2
    echo "stderr: $STDERR_LOG" >&2
    exit 1
fi

set +e
wait "$LAUNCHER_PID"
EXIT_CODE=$?
set -e

if [[ ! -f "$REPORT_FILE" ]]; then
    echo "Mac Catalyst e2e app did not write a report." >&2
    echo "Exit code: $EXIT_CODE" >&2
    echo "stdout: $STDOUT_LOG" >&2
    cat "$STDOUT_LOG" >&2 2>/dev/null || true
    echo "stderr: $STDERR_LOG" >&2
    cat "$STDERR_LOG" >&2 2>/dev/null || true
    exit 1
fi

if ! grep -q '^  "passed": true$' "$REPORT_FILE"; then
    echo "Mac Catalyst e2e tests failed." >&2
    echo "Exit code: $EXIT_CODE" >&2
    echo "Report: $REPORT_FILE" >&2
    cat "$REPORT_FILE" >&2
    exit 1
fi

echo "Mac Catalyst e2e tests passed."
echo "Report: $REPORT_FILE"
