# Quality and Tests Gate - v1.0.0-rc.1

## Gate Owner
Release automation

## Date
2026-08-20

## Evidence Source

| Run | Purpose |
|-----|---------|
| [32419602393](https://github.com/MarketDataApp/sdk-csharp/actions/runs/32419602393) | PR #71 — unit, coverage, live integration |
| [32419842329](https://github.com/MarketDataApp/sdk-csharp/actions/runs/32419842329) | `main` @ `b00d3d7` — full OS matrix |

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
