# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

Compliance work performed on the C#/.NET SDK after its donation by Omid Rad
(Exceptal) to Market Data. Changes are grouped by type and reference the
MarketData.app SDK requirements sections they satisfy.

### Added

- `AddMarketDataClient` service-collection extensions
  (`Microsoft.Extensions.DependencyInjection`) registering `MarketDataClient` as a
  singleton over an `IHttpClientFactory`-managed HttpClient.
- `MarketDataClient.CreateAsync(httpClient, options?, cancellationToken)` async
  factory: when a token is present and `ValidateTokenOnStartup` is enabled
  (default), it awaits `GET /user/` to fail fast on a bad token
  (`AuthenticationException`) and to seed the rate-limit snapshot. Demo mode and
  opt-out skip the call. (§§5, 8.1)
- `SupportInfo` property that renders the mandated
  `--- MARKET DATA SUPPORT INFO ---` block: snake_case labels in spec order
  (`request_id`, `request_url`, `status_code`, `timestamp`, `message`,
  `exception_type`), column-aligned, with a `(none)` fallback. (§6.3)
- `IsJson` / `IsCsv` / `IsHtml` format-detection flags on every response via the
  `IMarketDataResponse` contract (typed JSON, `CsvResponse`, `HtmlResponse`).
  (§11.6)
- Concise `ToString()` summaries on response wrappers (type, item count, HTTP
  status, no-data marker) and primary data records — no raw payload dumps.
  (§11.6)
- Extension-aware `SaveToFile` / `SaveToFileAsync` that select content by file
  extension (`.json` / `.csv` / `.html`, falling back to the raw body) and
  return the written path. (§11.6)
- Configuration cascade: client-level defaults (`DefaultDateFormat`, `Mode`,
  `Columns`, `AddHeaders`, `Human`, `MinimumLogLevel`, `OutputFormat`) merge into
  per-method parameters (method wins), plus new `MARKETDATA_*` formatting and
  diagnostics environment variables: `MARKETDATA_DATE_FORMAT`, `MARKETDATA_MODE`,
  `MARKETDATA_COLUMNS`, `MARKETDATA_ADD_HEADERS`, `MARKETDATA_USE_HUMAN_READABLE`,
  `MARKETDATA_LOGGING_LEVEL`, and `MARKETDATA_OUTPUT_FORMAT`. (§4)
- Cached `/status/` retry gate: before retrying a server error the SDK consults a
  per-service status snapshot (270s refresh / 300s validity); an `OFFLINE`
  service fails immediately instead of retrying. (§9.5)
- `IDisposable` on `MarketDataClient` and `ApiClient` (disposes the concurrency
  semaphore; never the caller-owned `HttpClient`). (§1)
- Packaging: git-tag-driven versioning via MinVer, deterministic builds,
  symbol package (`.snupkg`), Source Link (CI), package validation, and NuGet
  metadata (README, license, repository/project URLs). (§15)
- Continuous integration: cross-OS unit-test matrix (ubuntu, windows, macOS), a
  line + branch coverage gate, a coverage-report artifact, and live integration
  tests wired to run on pull requests and releases. (§13)
- This `CHANGELOG.md`.

### Changed

- Request timeout is fixed at 99 seconds and is no longer configurable. (§10)
- `MaxConcurrentRequests` is validated and capped to the range 1–50. (§12)
- Automatic retries now apply only to HTTP 501–599 server errors and transient
  network failures; other responses are not retried.
- `request_id` is read from the `cf-ray` response header first, then falls back
  to `x-request-id`. (§6.2)
- Support-info and diagnostic timestamps are rendered in US/Eastern time.
- `SupportInfo` is now a property, replacing the previous `GetSupportInfo()`
  method. (§6.3)
- Package version is no longer a hardcoded `<Version>` literal; it is derived
  from git tags at build time (with a `0.0.0-alpha.0.N` pre-release fallback for
  untagged local/dev builds). (§15)

### Removed

- Configurable `Timeout` option and the `MARKETDATA_TIMEOUT` environment variable
  (timeout is now fixed at 99s). (§10)
- Undocumented `nonstandard` parameter on `options/expirations` (retained on the
  options chain, where it is documented). (§3)
- Undocumented `countback` parameter on `options/quotes`. (§3)
- Removed the deprecated `stocks/bulkquotes` methods
  (`GetBulkQuotesAsync`/`GetBulkQuotesCsvAsync`) and `StockBulkQuotesRequest`; use
  `GetQuotesAsync` with multiple symbols (the `stocks/quotes` endpoint now serves
  bulk retrieval). (§3)

### Fixed

- Constructors no longer perform synchronous-over-async network I/O; startup
  token validation moved to `CreateAsync`. (§§5, 8.1)
- The news JSON endpoint now honors the `columns` projection. (§3)
- Local `dotnet build` no longer emits the Source Link
  "Source control information is not available - the generated source link is
  empty" warning: Source Link and `ContinuousIntegrationBuild` are gated to CI,
  where the repository resolves normally.

[Unreleased]: https://github.com/MarketDataApp/sdk-csharp/commits/main
