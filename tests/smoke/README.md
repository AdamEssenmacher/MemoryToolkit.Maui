# Smoke Tests

This folder contains opt-in, local smoke tests that exercise the MAUI sample app outside the pure unit test suite.

`run-maccatalyst-smoke.sh` builds the Shell sample for `net10.0-maccatalyst`, launches the generated app bundle, waits long enough to catch immediate startup crashes, and then quits the app. Run it from a GUI-capable macOS session:

```sh
tests/smoke/run-maccatalyst-smoke.sh
```

`run-maccatalyst-e2e.sh` builds and launches the focused MAUI e2e test app. It verifies that `LeakMonitorBehavior` and `TearDownBehavior` run from real MAUI `Unloaded`/navigation lifecycle events and that a popped page graph is released after compartmentalization:

```sh
tests/smoke/run-maccatalyst-e2e.sh
```

These smoke tests are intentionally not a leak-lab or device-audit harness, and they are not wired into CI. They exist to sanity-check the V2 package and sample/test app startup paths locally without making normal `dotnet test` runs depend on Mac Catalyst UI launch behavior.
