# Compatibility Gate - v1.0.0-rc.2

## Gate Owner
Release automation

## Date
2026-08-20

## Target Framework / OS Matrix

Every OS leg runs the unit suite on **both** target frameworks, so the matrix below is
6 combinations, not 3.

| OS | net8.0 | net10.0 | Source |
|----|--------|---------|--------|
| ubuntu-latest | PASS | PASS | run 32432865152 |
| windows-latest | PASS | PASS | run 32432865152 |
| macos-latest | PASS | PASS | run 32432865152 |

## GitHub Actions Status

```
CI on main @ ac93d51 — completed/success

  success  Select OS matrix
  success  Build / Test / Pack (ubuntu-latest)
  success  Build / Test / Pack (windows-latest)
  success  Build / Test / Pack (macos-latest)
  skipped  Integration Tests (live)
```

`Integration Tests (live)` is skipped on push-to-main by design — the job runs on pull
requests, releases and manual dispatch, so the live quota is not spent twice per merge.
Its evidence for this release comes from PR #81 (run 32432713289, 21/21 passed) and it
will run again as the release gate inside `release.yml`.

## Local Parity

`net10.0`: 307/307 locally. `net8.0` could not be executed on the development machine —
only the .NET 10 shared runtime is installed there. Both frameworks are covered by CI,
which installs the 8.0.x and 10.0.x bands via `actions/setup-dotnet`.

## Package Validation

`EnablePackageValidation` is on, so `dotnet pack` fails if the public API surface diverges
between `net8.0` and `net10.0`. Pack succeeded.

## Gate Result

| Check | Status |
|-------|--------|
| net8.0 compatible | PASS |
| net10.0 compatible | PASS |
| Linux compatible | PASS |
| Windows compatible | PASS |
| macOS compatible | PASS |
| Cross-TFM API surface consistent | PASS |
| CI green on main | PASS |

**GATE STATUS: PASS**
