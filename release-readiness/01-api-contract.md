# API Contract Gate - v1.0.0-rc.1

## Gate Owner
Release automation

## Date
2026-08-20

## Scope

This is the **first published version** of the C#/.NET SDK. No prior version of
`marketdata.sdk` exists on NuGet.org, so there is no consumer-visible API to break and no
migration path to provide.

## Package Identity

| Field | Value |
|-------|-------|
| Package ID | `marketdata.sdk` |
| Assembly / root namespace | `MarketDataApp` |
| Target frameworks | `net8.0`, `net10.0` |

The package ID and the namespace differ deliberately: callers install `marketdata.sdk`
and write `using MarketDataApp;`. The ID matches the sibling SDKs' naming
(`marketdata-sdk-java`, `marketdata-sdk-py`) and avoids colliding with the `MarketDataApp`
organization name on NuGet.org.

**Package IDs are immutable once published.** This is the last point at which the ID can
change without abandoning it.

## Pre-publication Surface Removals

Two deprecated surfaces were removed before first publication. Because nothing has ever
been published, these are **not** breaking changes for any consumer:

| Removed | Replacement |
|---------|-------------|
| `options/strikes` (`GetStrikesAsync`, `OptionsStrikesRequest`, `OptionStrikes`, `OptionsStrikesResponse`) | Use the options chain to discover strikes; the upstream endpoint is deprecated |
| `stocks/bulkquotes` (`GetBulkQuotesAsync`, `StockBulkQuotesRequest`) | `GetQuotesAsync` with multiple symbols |

Both are documented in the CHANGELOG under `[1.0.0-rc.1]`.

## Release-Candidate Rationale

Published as `1.0.0-rc.1` rather than `1.0.0`. The public API is feature-complete and no
breaking changes are planned before the stable release, but a candidate lets real
consumers exercise the surface before the API is frozen by a stable tag.

The dotted `rc.1` form is required: SemVer compares all-digit dot-separated pre-release
identifiers numerically, so `rc.2` correctly precedes `rc.10`. Written `rc1`/`rc2` the
identifier is compared as text and `rc10` would sort before `rc2`. NuGet implements
SemVer 2.0.0 ordering.

## Gate Result

| Check | Status |
|-------|--------|
| Breaking changes documented | PASS (none applicable — first publication) |
| Migration guide provided | PASS (not applicable) |
| CHANGELOG updated | PASS (`## [1.0.0-rc.1] - 2026-08-20`) |
| Package ID final before publication | PASS |

**GATE STATUS: PASS**
