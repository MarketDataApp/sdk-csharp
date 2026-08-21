# Docs/DX Gate - v1.0.0-rc.2

## Gate Owner
Release automation

## Date
2026-08-20

## Documentation Alignment

| Document | Messaging | Status |
|----------|-----------|--------|
| `README.md` | `status-release candidate` badge; RC note explaining `--prerelease`; install shows `MarketDataApp` | PASS |
| `CHANGELOG.md` | `## [1.0.0-rc.2] - 2026-08-20` present with link references rewritten | PASS |
| `MarketDataApp.csproj` | `Description` and `PackageReleaseNotes` carry RC wording; both ship to the NuGet listing page | PASS |
| `CONTRIBUTING.md` | RC wording | PASS |
| `.github/ISSUE_TEMPLATE/bug.yml` | RC wording; version placeholder `1.0.0-rc.2` | PASS |
| `docs/` (synced) | Package ID and RC wording updated at source in `MarketDataApp/documentation` PR #168 | PASS (pending docs-sync) |
| `.github/RELEASE_PROCESS.md` | Documents this exact release path, one-time setup, and rollback | PASS |

No `MarketDataApp` package-ID references remain in any install instruction.

## Version Consistency

There is no hand-maintained version literal to drift. MinVer derives the version from the
git tag (`MinVerTagPrefix=v`), and the release workflow additionally passes
`MinVerVersionOverride`. Verified:

```
dotnet pack -p:MinVerVersionOverride=1.0.0-rc.2
  → MarketDataApp.1.0.0-rc.2.nupkg
  → MarketDataApp.1.0.0-rc.2.snupkg

nuspec: id = MarketDataApp
        version = 1.0.0-rc.2
        description = RELEASE CANDIDATE. C#/.NET SDK for the marketdata.app API...
lib/net8.0/MarketDataApp.dll  + .xml
lib/net10.0/MarketDataApp.dll + .xml
```

XML documentation ships for both target frameworks.

## Executable Examples — live smoke runs

From run 32419602393, against the live API:

### Watchlist (`--once`)
```
AAPL        311.94     +0.64    +0.21%      39,939,348  17:29:33
MSFT        481.26     +0.11    +0.02%      19,374,801  17:29:33
```

### Watchlist CSV export
```
Saved 30 daily AAPL candles to TestResults/aapl-daily.csv
```

### OptionsChainMonitor (`--once`)
```
AAPL @ 311.29 — expiration 2026-09-18 (29 DTE, requested ~30)
```

All three exited zero. These exercise the full user path end to end — one-line client
construction, startup token validation, a live data request, and rendering/export — which
no unit test reproduces.

## Gate Result

| Check | Status |
|-------|--------|
| README aligned | PASS |
| CHANGELOG complete | PASS |
| Package metadata aligned | PASS |
| Install instructions use the final package ID | PASS |
| XML docs ship for both TFMs | PASS |
| Examples run successfully against the live API | PASS |

**GATE STATUS: PASS**
