# API Contract Gate - v1.0.0

## Gate Owner
Release automation

## Date
2026-08-21

## Scope

This is the **first published version** of the C#/.NET SDK. No prior version of
`MarketDataApp` exists on NuGet.org, so there is no consumer-visible API to break and no
migration path to provide.

## Package Identity

| Field | Value |
|-------|-------|
| Package ID | `MarketDataApp` |
| Assembly / root namespace | `MarketDataApp` |
| Target frameworks | `net8.0`, `net10.0` |

The package ID and the namespace differ deliberately: callers install `MarketDataApp`
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

Both are documented in the CHANGELOG under `[1.0.0]`.

## Stability Commitment

Published as a stable `1.0.0`. From this version the public API is covered by semantic
versioning: a breaking change requires a major bump and a migration note in the CHANGELOG.

Two release candidates preceded it, exercised against the live API on all three operating
systems and both target frameworks. No API change was made between the last candidate and
this release — only messaging, so the surface consumers tested is the surface that ships.

Removing a public member, renaming one, changing a signature, tightening a return type, or
altering documented behaviour all count as breaking, including for members that are public
but undocumented.

## Gate Result

| Check | Status |
|-------|--------|
| Breaking changes documented | PASS (none applicable — first publication) |
| Migration guide provided | PASS (not applicable) |
| CHANGELOG updated | PASS (`## [1.0.0] - 2026-08-21`) |
| Package ID final before publication | PASS |

**GATE STATUS: PASS**
