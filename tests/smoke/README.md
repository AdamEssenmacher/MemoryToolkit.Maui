# Smoke Tests

This folder contains opt-in, local smoke tests that exercise the MAUI sample app outside the pure unit test suite.

`run-maccatalyst-smoke.sh` builds the Shell sample for `net10.0-maccatalyst`, launches the generated app bundle, waits long enough to catch immediate startup crashes, and then quits the app. Run it from a GUI-capable macOS session:

```sh
tests/smoke/run-maccatalyst-smoke.sh
```

The smoke test is intentionally not a leak-lab or device-audit harness, and it is not wired into CI. It exists to sanity-check the V2 package and sample app startup path locally without making normal `dotnet test` runs depend on Mac Catalyst UI launch behavior.
