# Quality and Tests Gate - v1.0.0

## Gate Owner
Release automation

## Date
2026-08-21

## Evidence Source

| Run | Purpose |
|-----|---------|
| [32515931454](https://github.com/MarketDataApp/sdk-csharp/actions/runs/32515931454) | PR #88 — unit, coverage, live integration |
| [32516092418](https://github.com/MarketDataApp/sdk-csharp/actions/runs/32516092418) | `main` @ `31e11d7` — full OS matrix |

## Build

```
0 Warning(s)
```

`TreatWarningsAsErrors` is enabled, so any warning fails the build outright.

## Unit Tests — both target frameworks

```
Passed!  - Failed: 0, Passed: 307, Skipped: 0, Total: 307 - MarketDataApp.Tests.dll (net8.0)
Passed!  - Failed: 0, Passed: 307, Skipped: 0, Total: 307 - MarketDataApp.Tests.dll (net10.0)
```

Each target framework is invoked separately so the runtimes are gated independently.

## Coverage — gated at 100%

```
net8.0                          net10.0
| Total   | Line | Branch |     | Total   | Line | Branch |
| Total   | 100% | 100%   |     | Total   | 100% | 100%   |
| Average | 100% | 100%   |     | Average | 100% | 100%   |
```

Enforced by coverlet.msbuild with `Threshold=100`, `ThresholdType=line,branch`,
`ThresholdStat=total`. Below 100% the step fails and takes the job with it. Method
coverage also reports 100%.

## Integration Tests — live API

```
Passed!  - Failed: 0, Passed: 21, Skipped: 0, Total: 21, Duration: 22 s
          - MarketDataApp.IntegrationTests.dll (net10.0)
```

Zero skipped is the meaningful number here: `IntegrationFactAttribute` silently skips
every test when `MARKETDATA_TOKEN` is absent, so a skipped suite would look green while
proving nothing. All 21 executed against the live API.

## Formatting

`dotnet format MarketDataApp.slnx --verify-no-changes` — clean, both locally and in CI.

## Coverage Gate Determinism

The 100% branch gate previously failed intermittently on an unchanged tree (#74), blocking
releases on `windows-latest` and later on `ubuntu-latest`, both on `net10.0`.

Root cause: `ActivitySource.AddActivityListener` is process-global while xUnit runs test
classes in parallel, so a listener owned by one class was live inside another class's test.
That decided whether `StartActivity(...)` returned an activity or null, which moved the
`activity?.` branches in `ApiClient`'s catch arms. Assertions never noticed — all 307 tests
passed either way — but one branch side went unvisited.

Diagnosed from the coverage report the release gate now uploads: exactly two uncovered
branch points, `ApiClient.cs:293` and `:294`, inside
`<SendOnceWithinGateAsync>d__20::MoveNext()`. Line coverage stayed at 100%, so the block
ran; only one activity state was ever observed.

Fixed by placing the three listener-registering classes in a shared xUnit collection, which
runs them sequentially. Verified by running the `net10.0` coverage gate 8 consecutive times
locally — 100% branch every run — plus the full cross-OS matrix on `main`.

## Workflow Lint

`actionlint` 1.7.12 — clean across all six workflow files.

## Gate Result

| Check | Status |
|-------|--------|
| Build with zero warnings | PASS |
| Unit tests pass on net8.0 | PASS (307/307) |
| Unit tests pass on net10.0 | PASS (307/307) |
| Coverage threshold met | PASS (100% line, 100% branch, both TFMs) |
| Integration tests pass | PASS (21/21, live, none skipped) |
| Formatting clean | PASS |

**GATE STATUS: PASS**
