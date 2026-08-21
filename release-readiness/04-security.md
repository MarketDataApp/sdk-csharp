# Security Gate - v1.0.0-rc.2

## Gate Owner
Release automation

## Date
2026-08-20

## Dependency Audit

```
dotnet list package --vulnerable --include-transitive --format json
No known vulnerabilities in the restored dependency graph.
```

Run in CI on every build (run 32432713289). Note that `dotnet list package --vulnerable`
exits 0 even when it reports advisories, so the gate parses the JSON output and fails on a
non-zero finding count rather than trusting the exit code. The failure path was verified
against a synthetic vulnerable input before this release.

## Deprecated Packages (advisory, non-blocking)

```
MarketDataApp            — no deprecated packages
MarketDataApp.Tests      — xunit 2.9.3  (Legacy → xunit.v3)
MarketDataApp.IntegrationTests — xunit 2.9.3  (Legacy → xunit.v3)
```

Confined to test projects; nothing deprecated ships in the package. Deprecation is a
maintenance signal, not a vulnerability, so it does not block. Tracked for a future
xunit.v3 migration.

## Third-party Scanning

Socket Security ran on PR #81: Project Report **pass**, Pull Request Alerts **pass**.

## Token Handling Review

| Check | Evidence |
|-------|----------|
| Header-based auth, never a query parameter | `ApiClient.cs:231` — `new AuthenticationHeaderValue("Bearer", _options.ApiToken)`. No `token=` query construction anywhere in `src/`. |
| Token never logged in full | The only log statement touching it is `ApiClient.cs:94`, at **Debug** level, and it passes `RedactToken(...)`. |
| Redaction shows at most 4 trailing characters | `ApiClient.cs:605-606` — `token.Length <= 4 ? "****" : $"****{token[^4..]}"` |
| `ToString()` does not leak the token | `MarketDataClientOptions.cs:113` routes through `RedactToken` |
| Redaction is covered by tests | `MarketDataClientOptionsTests.ToString_RedactsTheApiToken`, `ApiClientCoverageTests.StartupLogging_RedactsConfiguredTokenSuffix`, `StartupLogging_RedactsShortTokenEntirely` |
| TLS verification never disabled | No `ServerCertificateCustomValidationCallback` or equivalent anywhere in `src/` or `examples/` |
| Support diagnostics exclude the token | `SupportInfo` emits request id, URL, status, timestamp, message and exception type only |

## Supply-chain Integrity of the Release Itself

- Publishing uses **OIDC Trusted Publishing**; no long-lived NuGet API key exists as a
  repository secret.
- The Trusted Publishing policy is scoped to owner `MarketDataApp`, repo `sdk-csharp`,
  workflow `release.yml`, environment `nuget`, and glob `MarketDataApp` — it cannot
  publish any other package.
- `actions/attest-build-provenance` signs the exact `.nupkg`/`.snupkg` bytes **before**
  they are pushed.
- Deterministic build with Source Link and an embedded symbol package.

## Gate Result

| Check | Status |
|-------|--------|
| No known vulnerabilities | PASS |
| Token handling secure | PASS |
| TLS verification intact | PASS |
| Publish path keyless and provenance-attested | PASS |

**GATE STATUS: PASS**
