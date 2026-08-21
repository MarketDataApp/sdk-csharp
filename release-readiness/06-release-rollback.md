# Release/Rollback Gate - v1.0.0-rc.2

## Gate Owner
Release automation

## Date
2026-08-20

## Open Blockers

```
Issue #70: Point the docs-liveness check at production once the docs site
           promotes staging to main
Status:    Open, non-blocking. The C# docs pages currently live on staging;
           ci.yml and release.yml set MARKETDATA_DOCS_HOST=www-staging.marketdata.app
           so DocsLivenessTests resolve. Cleanup is tracked for after the docs
           site promotes.

P0 blockers: None
```

## Release Path

| Stage | Gate |
|-------|------|
| `gate` | Full `{ubuntu, windows, macOS}` matrix, both TFMs, 100% coverage, vulnerability audit, format check |
| `release` | Tag must not exist; CHANGELOG `## [1.0.0-rc.2]` section must be non-empty |
| `publish-nuget` | Live integration suite as a release gate; provenance attestation before push |

A missing `MARKETDATA_TOKEN` fails the release outright rather than letting a silently
skipped integration suite look green.

## Rollback Plan

**NuGet.org versions are immutable — a published version cannot be replaced or deleted.**

1. Stop any promotion messaging.
2. If the package is harmful, unlist `1.0.0-rc.2` on NuGet.org. Unlisting hides it from
   search and from new resolutions; existing lockfiles still resolve it.
3. Ship `1.0.0-rc.2` from `main` with the targeted fix.
4. Record root cause and remediation in the next CHANGELOG entry.

Unlisting is a manual action in the NuGet.org web UI under the `marketdata` account. The
Trusted Publishing policy deliberately does **not** carry the unlist/relist scope, so no
workflow can do it.

## Hotfix Path

- Branch from: the `v1.0.0-rc.2` tag
- Target: `main`
- Next version: `1.0.0-rc.2`

## Candidate-specific Risk Posture

Publishing a release candidate rather than `1.0.0` is itself the primary risk control.
`1.0.0-rc.2` is hidden from default installs — `dotnet add package MarketDataApp`
resolves only stable versions, so consumers must opt in with `--prerelease`. A defect
therefore has a limited blast radius, and the stable `1.0.0` tag can wait for the
candidate to hold up.

## Gate Result

| Check | Status |
|-------|--------|
| No P0 blockers | PASS |
| Rollback plan documented | PASS |
| Hotfix path defined | PASS |
| Publish path gated end to end | PASS |

**GATE STATUS: PASS**
