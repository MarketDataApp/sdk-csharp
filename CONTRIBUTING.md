# Contributing to the Market Data C#/.NET SDK

Thank you for your interest in contributing!

> **Note:** This SDK is **alpha / in active development**. The public API may change
> without notice until a stable `1.0.0` is tagged.

## Reporting Bugs

Use the [bug report template](https://github.com/MarketDataApp/sdk-csharp/issues/new?template=bug.yml) and provide:

1. **Resource and method** — which SDK method has the bug (e.g. `client.Stocks.GetCandlesAsync`)
2. **Reproduction code** — complete, runnable C# (or F#/VB) that demonstrates the issue
3. **Expected vs actual behavior** — including exact values, exception messages, and stack traces
4. **`SupportInfo`** — if a `MarketDataException` was thrown, paste `ex.SupportInfo` (request id, URL, status, timestamp — the token is never included)
5. **Environment** — SDK (NuGet) version, `dotnet --version`, target framework, OS

### What makes a good bug report

- **Self-contained code** that runs without modification, with unrelated code removed
- **Specific output** — exact error messages, stack traces, or incorrect values
- Confirm the behavior differs from the [API documentation](https://www.marketdata.app/docs/api), since the SDK returns data as the API provides it

## Code Contributions

### Getting started

1. Fork and clone the repository
2. Restore: `dotnet restore MarketDataApp.slnx` (requires the .NET **10** SDK — see `global.json`)
3. Create a branch: `git checkout -b fix/your-bug-description`

### Development guidelines

- **Idiomatic modern C#** — immutable `record` types, nullable reference types, async-only
  (`Task`-returning `…Async` methods with `CancellationToken`), `System.Text.Json`
- **Money is `decimal`** decoded straight from the raw JSON token; non-monetary values
  (Greeks, IV, sizes) stay `double`/`long`/`int`
- **Warnings are errors** — `TreatWarningsAsErrors` is on; the build must be 0 warnings
- **Formatting** — run `dotnet format` before committing; `dotnet format --verify-no-changes` must pass
- **Architecture** — the SDK uses a hand-rolled `HttpClient` wrapper with its own retry
  loop and a cached `/status` gate; keep changes within that design and don't add
  heavyweight dependencies without discussion
- Keep request inputs and response models in sync with the [Market Data API docs](https://www.marketdata.app/docs/api)

### Testing

```bash
# Full unit-test suite (all HTTP mocked — no token required)
dotnet test src/MarketDataApp.Tests/MarketDataApp.Tests.csproj -c Release

# Enforce the coverage gate (100% line AND branch)
dotnet test src/MarketDataApp.Tests/MarketDataApp.Tests.csproj -c Release \
  -p:CollectCoverage=true -p:CoverletOutputFormat=opencover \
  -p:Threshold=100 -p:ThresholdType=line,branch -p:ThresholdStat=total
```

- **Unit tests mock all HTTP** (via `StubHttpMessageHandler`); they never hit the network.
- **100% line AND branch coverage is required** — CI fails below it. Prefer real
  behavior tests; use `[ExcludeFromCodeCoverage]` only for genuinely unreachable code,
  with a justifying comment.
- **Integration tests** (`src/MarketDataApp.IntegrationTests`) hit the live API and are
  gated by `MARKETDATA_RUN_INTEGRATION_TESTS=true` + a `MARKETDATA_TOKEN`. They stay
  skipped without a token, so the default test run and CI stay green.

### Pull requests

1. Ensure `dotnet build -c Release` (0 warnings), the unit tests, the 100% coverage gate,
   and `dotnet format --verify-no-changes` all pass locally
2. Add tests for any new functionality (keep coverage at 100%)
3. Update the README and any relevant `docs/` when behavior or public API changes
4. Add an entry under `## [Unreleased]` in [CHANGELOG.md](./CHANGELOG.md)
5. Keep commits focused; reference any related issues in the PR description

## Questions?

- [GitHub Issues](https://github.com/MarketDataApp/sdk-csharp/issues) for bugs
- [GitHub Discussions](https://github.com/MarketDataApp/sdk-csharp/discussions) for questions and feature ideas
- [Discord](https://discord.com/invite/GmdeAVRtnT) for community chat
