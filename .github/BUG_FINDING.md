# Bug Finding Workflow

This document defines a systematic process for discovering bugs in
`MarketDataApp/sdk-csharp` through exploration and testing, before users hit them.

> **IMPORTANT: Every bug found MUST be submitted as a GitHub issue.**
>
> Do not just record bugs in markdown files, notes, or comments. Each bug must become a
> real GitHub issue:
>
> - **CLI**: `gh issue create --label "bug" --title "[Bug]: ..." --body "..."`
> - **Web**: [Create Bug Report](https://github.com/MarketDataApp/sdk-csharp/issues/new?template=bug.yml)
>
> A bug hunt is not complete until every discovered bug exists as a GitHub issue.

## Overview

**Purpose**: proactive bug discovery, as opposed to reactive bug processing.

- **BUG_FINDING.md** (this document): find bugs before users encounter them.
- **[ISSUE_WORKFLOW.md](./ISSUE_WORKFLOW.md)**: process bug reports that users submit.

**Workflow**: Find Bug → **Create GitHub Issue (REQUIRED)** → [ISSUE_WORKFLOW.md] → Fix

**When to use this document**:

- QA passes before releases
- Pre-release validation (see [RELEASE_PROCESS.md](./RELEASE_PROCESS.md))
- Exploratory testing sessions
- After a significant refactor
- When onboarding, to understand the edge cases

---

## Prerequisites

### Environment setup

```bash
dotnet build MarketDataApp.slnx -c Release
dotnet --version          # 10.0.x band, see global.json

export MARKETDATA_TOKEN="your_token_here"
export MARKETDATA_RUN_INTEGRATION_TESTS=true
```

### Baseline verification

Confirm the suite passes before hunting. Bug finding assumes a working baseline.

```bash
dotnet test src/MarketDataApp.Tests/MarketDataApp.Tests.csproj -c Release
```

If tests fail, fix that first.

### Architecture

Familiarize yourself with the main components:

- `MarketDataClient` — entry point. Exposes `Stocks`, `Options`, `Funds`, `Markets`,
  `Utilities`. Constructed three ways: `CreateAsync` (async factory, validates the token
  via `GET /user/`), the plain constructor (same validation, blocking), and
  `AddMarketDataClient` (DI).
- `ApiClient` — HTTP, retries, the cached `/status/` retry gate, rate-limit tracking.
- `MarketDataClientOptions` / `MarketDataRequestOptions` — the configuration cascade.
  Client-level defaults merge into per-method parameters; **the method wins**.
- `MarketDataResponse<T>` — the response base: `Values`, `IsNoData`, `StatusCode`,
  `RequestUrl`, `RequestId`, `RateLimit`, `Parts`, `IsComposite`, `RawBody`,
  `IsJson`/`IsCsv`/`IsHtml`, `SaveToFile`/`SaveToFileAsync`.
- `MarketDataException` and subclasses — `AuthenticationException`,
  `BadRequestException`, `NotFoundException`, `RateLimitException`, `ServerException`,
  `NetworkException`, `ParseException`. All carry `SupportInfo`.

### Two runtimes, always

The library multi-targets `net8.0` and `net10.0` and CI runs the suite on **both**. A bug
that appears on only one target framework is still a bug, and is often a more interesting
one. When a finding looks runtime-dependent, record which TFM you observed it on.

```bash
dotnet test src/MarketDataApp.Tests/MarketDataApp.Tests.csproj -c Release -f net8.0
dotnet test src/MarketDataApp.Tests/MarketDataApp.Tests.csproj -c Release -f net10.0
```

---

## Area 1: Error Handling and Exception Surface

### What can go wrong

- The wrong exception type for a given HTTP status
- `SupportInfo` missing the request id or URL, making triage impossible
- An API token leaking into a message, log line, or `SupportInfo`
- An inner exception swallowed, losing the root cause
- `ParseException` thrown for a payload the SDK should handle

### Test scenarios

#### 1.1 Bad token

```csharp
using var client = await MarketDataClient.CreateAsync(
    new MarketDataClientOptions { ApiToken = "obviously-invalid-token" });

// Verify: AuthenticationException, and the token does NOT appear in the message.
// Bug indicator: a generic Exception, or the token echoed back in any output.
```

#### 1.2 Unknown symbol

```csharp
var response = await client.Stocks.GetQuoteAsync("ZZZZ_NOT_A_SYMBOL");

// Verify: is this a NotFoundException, or a successful response with IsNoData == true?
// Both are defensible; the SDK must pick one and apply it consistently.
// Bug indicator: NullReferenceException, or Values being null rather than empty.
```

#### 1.3 SupportInfo completeness

```csharp
try
{
    await client.Stocks.GetCandlesAsync(StockResolution.Daily, "AAPL", countback: -5);
}
catch (MarketDataException ex)
{
    Console.WriteLine(ex.SupportInfo);
    // Verify: request_id, request_url, status_code, timestamp, message and
    // exception_type are all present, in that order, column-aligned, with
    // "(none)" for anything genuinely absent.
    // Bug indicator: blank values, missing lines, or a leaked token in request_url.
}
```

### Red flags

- A bare `Exception` or `HttpRequestException` reaching the caller
- An API token visible anywhere in output
- `SupportInfo` lines missing or unaligned
- `InnerException` null where an underlying failure clearly existed

### Pass/fail criteria

| Scenario | Pass | Fail |
|---|---|---|
| Bad token | `AuthenticationException`, token redacted | Generic exception, or token leaked |
| Unknown symbol | Consistent: typed exception or `IsNoData` | `NullReferenceException`, null `Values` |
| SupportInfo | All six fields present and aligned | Blank or missing fields |

---

## Area 2: Empty and Sparse Responses

### What can go wrong

- `Values` returning `null` instead of an empty collection
- `IsNoData` disagreeing with `Values.Count == 0`
- Optional fields throwing when absent
- Behavior differing between 0, 1, and 2+ results

### Test scenarios

#### 2.1 Empty result window

```csharp
// A weekend has no trading days.
var candles = await client.Stocks.GetCandlesAsync(
    new StockCandlesRequest
    {
        Resolution = StockResolution.Daily,
        Symbol = "AAPL",
        From = new DateOnly(2024, 1, 6),   // Saturday
        To = new DateOnly(2024, 1, 7),     // Sunday
    });

// Verify every access pattern:
Console.WriteLine(candles.IsNoData);        // expect true
Console.WriteLine(candles.Values.Count);    // expect 0, never a throw
foreach (var c in candles.Values) { }       // expect a clean no-op
// Bug indicator: null Values, an ArgumentOutOfRangeException, or IsNoData
// disagreeing with Values.Count.
```

#### 2.2 Single item

```csharp
var quote = await client.Stocks.GetQuoteAsync("AAPL");

// Verify: Values is still a list of one, not a bare item.
// Bug indicator: the shape of the response changing with the row count.
```

#### 2.3 Missing optional fields

```csharp
// Forward earnings quarters often carry null EPS values.
var earnings = await client.Stocks.GetEarningsAsync(
    new StockEarningsRequest { Symbol = "AAPL", From = new DateOnly(2024, 1, 1) });

// Verify: nullable fields decode to null without throwing.
// Bug indicator: NullReferenceException, or a default like 0 masking a null.
```

### Pass/fail criteria

| Scenario | Pass | Fail |
|---|---|---|
| Empty window | Empty collection, `IsNoData` true, no throw | Null `Values` or an exception |
| Single item | Consistent collection shape | Shape varies with count |
| Missing optional | Null field, no throw | `NullReferenceException`, or 0 substituted for null |

---

## Area 3: Concurrency, Rate Limits, and Composite Requests

### What can go wrong

- Partial failures hidden inside a fan-out
- The rate-limit snapshot going stale or reporting the wrong request
- Composite responses (`Parts`, `IsComposite`) losing or duplicating rows
- The concurrency semaphore deadlocking or being disposed twice

### Test scenarios

#### 3.1 Multi-symbol batching

```csharp
var quotes = await client.Stocks.GetQuotesAsync(["AAPL", "MSFT", "ZZZZ_BOGUS"]);

// Verify: how is the bogus symbol reported? A row with nulls, an omitted row,
// or an exception for the whole batch?
// Bug indicator: silent data loss — three symbols in, two rows out, no signal.
foreach (var row in quotes.Values)
{
    Console.WriteLine($"{row.Symbol}: mid={row.Mid}");
}
```

#### 3.2 Rate-limit snapshot coherence

```csharp
var response = await client.Stocks.GetQuoteAsync("AAPL");

Console.WriteLine(response.RateLimit);          // request-scoped
Console.WriteLine(client.LatestRateLimit);      // client-level

// Verify: both are populated and mutually consistent after a real request.
// Bug indicator: a null request-scoped snapshot, or a client-level value that
// never advances across successive calls.
```

#### 3.3 Composite / auto-chunked requests

```csharp
// A long intraday window splits into concurrent sub-requests that are merged.
var candles = await client.Stocks.GetCandlesAsync(
    new StockCandlesRequest
    {
        Resolution = StockResolution.Minute(5),
        Symbol = "AAPL",
        From = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-2)),
        To = DateOnly.FromDateTime(DateTime.UtcNow),
    });

Console.WriteLine($"{candles.IsComposite} / {candles.Parts.Count} parts");

// Verify: the merged series is ordered, gap-free at the chunk boundaries, and
// free of duplicates.
// Bug indicator: duplicate or missing timestamps where two chunks meet.
```

#### 3.4 Disposal under concurrency

```csharp
var client = await MarketDataClient.CreateAsync(options);
var tasks = Enumerable.Range(0, 20)
    .Select(_ => client.Stocks.GetQuoteAsync("AAPL"))
    .ToArray();
client.Dispose();                 // dispose while requests are in flight
await Task.WhenAll(tasks);

// Verify: a clear ObjectDisposedException, or the in-flight work completes.
// Bug indicator: a hang, an unobserved task exception, or a
// SemaphoreFullException from double release.
```

### Pass/fail criteria

| Scenario | Pass | Fail |
|---|---|---|
| Multi-symbol | Failures visible per symbol | Silent data loss |
| Rate limits | Coherent request and client snapshots | Null or stale values |
| Composite | Complete, ordered, deduplicated merge | Gaps or duplicates at boundaries |
| Disposal | Deterministic outcome | Hang or semaphore corruption |

---

## Area 4: Date, Time, and Number Handling

### What can go wrong

- `DateOnly` boundaries resolving to the wrong trading day
- `DateFormat` variants decoding to different instants
- Timezone assumptions leaking from the host machine
- Decimal precision lost on prices

### Test scenarios

#### 4.1 Date format round-trip

```csharp
foreach (var df in new[] { DateFormat.Timestamp, DateFormat.Unix, DateFormat.Spreadsheet })
{
    var candles = await client.Stocks.GetCandlesAsync(
        new StockCandlesRequest
        {
            Resolution = StockResolution.Daily,
            Symbol = "AAPL",
            Countback = 5,
        },
        new MarketDataRequestOptions { DateFormat = df });

    Console.WriteLine($"{df}: {candles.Values.Count} rows");
}

// Verify: every encoding decodes to the SAME instants.
// Bug indicator: a format that throws, or rows that shift by hours between formats.
```

#### 4.2 Year boundary

```csharp
var candles = await client.Stocks.GetCandlesAsync(
    new StockCandlesRequest
    {
        Resolution = StockResolution.Daily,
        Symbol = "AAPL",
        From = new DateOnly(2023, 12, 29),
        To = new DateOnly(2024, 1, 2),
    });

// Verify: the series crosses the year boundary without a gap.
```

#### 4.3 Host timezone independence

```bash
TZ=Pacific/Kiritimati dotnet test src/MarketDataApp.Tests/MarketDataApp.Tests.csproj -c Release
TZ=Pacific/Niue      dotnet test src/MarketDataApp.Tests/MarketDataApp.Tests.csproj -c Release
```

Two runs, ~25 hours of UTC offset apart. Any test that passes under one and fails under
the other has an unstated dependency on the host clock. This is worth running on every QA
pass — CI runners are all UTC, so these bugs stay invisible there.

### Pass/fail criteria

| Scenario | Pass | Fail |
|---|---|---|
| Date formats | Identical decoded instants | Format-dependent values |
| Year boundary | Continuous series | Gap at the boundary |
| Host timezone | Identical results under any `TZ` | Results vary with `TZ` |

---

## Area 5: Configuration Cascade

### What can go wrong

- A per-method parameter losing to a client-level default (the precedence is inverted)
- An environment variable overriding an explicit in-code setting
- An unset option resolving to a wrong default rather than being omitted

The documented precedence is: **per-method parameter > client option > environment
variable > SDK default.**

### Test scenarios

#### 5.1 Method beats client

```csharp
using var client = await MarketDataClient.CreateAsync(
    new MarketDataClientOptions { DefaultDateFormat = DateFormat.Unix });

var response = await client.Stocks.GetQuoteAsync(
    "AAPL",
    options: new MarketDataRequestOptions { DateFormat = DateFormat.Timestamp });

Console.WriteLine(response.RequestUrl);
// Verify: the URL carries dateformat=timestamp, NOT unix.
// Bug indicator: the client default winning over the explicit per-call value.
```

#### 5.2 Client beats environment

```bash
MARKETDATA_DATE_FORMAT=unix dotnet run --project examples/QuickStart
```

```csharp
// with DefaultDateFormat = DateFormat.Spreadsheet set in code
// Verify: the in-code option wins over the environment variable.
```

#### 5.3 Unset stays unset

```csharp
var response = await client.Stocks.GetQuoteAsync("AAPL");
Console.WriteLine(response.RequestUrl);

// Verify: parameters nobody set are ABSENT from the query string, not sent with
// a guessed default.
// Bug indicator: columns=, human=false, or similar noise appearing unrequested.
```

### Pass/fail criteria

| Scenario | Pass | Fail |
|---|---|---|
| Method vs client | Method wins | Client default wins |
| Client vs environment | Client option wins | Environment wins |
| Unset parameter | Omitted from the URL | Sent with an invented default |

---

## Area 6: Output Formats and File Export

### What can go wrong

- `IsJson`/`IsCsv`/`IsHtml` disagreeing with the actual payload
- `SaveToFile` choosing content by the wrong rule
- A `ToString()` override dumping a raw payload rather than a summary

### Test scenarios

#### 6.1 Format flags

```csharp
var json = await client.Stocks.GetQuoteAsync("AAPL");
var csv  = await client.Stocks.GetQuoteCsvAsync("AAPL");

Console.WriteLine($"{json.IsJson}/{json.IsCsv}/{json.IsHtml}");   // expect True/False/False
Console.WriteLine($"{csv.IsJson}/{csv.IsCsv}/{csv.IsHtml}");      // expect False/True/False

// Bug indicator: exactly one flag should be true; two true or none true is a bug.
```

#### 6.2 Extension-driven export

```csharp
var response = await client.Stocks.GetCandlesAsync(
    StockResolution.Daily, "AAPL", countback: 5);

response.SaveToFile("out.json");   // JSON content
response.SaveToFile("out.csv");    // CSV content
response.SaveToFile("out.txt");    // raw body fallback

// Verify: each file's content matches its extension, and the returned path is
// the file actually written.
// Bug indicator: identical bytes in all three, or a path that does not exist.
```

#### 6.3 ToString() discipline

```csharp
Console.WriteLine(response);
// Verify: a short summary — type, item count, HTTP status, no-data marker.
// Bug indicator: the whole payload dumped, which would flood a user's logs.
```

---

## Area 7: Dependency Injection and Lifetime

### What can go wrong

- The client registered with the wrong lifetime
- The SDK disposing an `HttpClient` it does not own
- Startup token validation blocking application start

### Test scenarios

#### 7.1 Caller-owned HttpClient survives

```csharp
var http = new HttpClient();
using (var client = await MarketDataClient.CreateAsync(http)) { }

// The client is disposed; the caller's HttpClient must NOT be.
var stillWorks = await http.GetAsync("https://api.marketdata.app/v1/status/");
// Bug indicator: ObjectDisposedException — the SDK disposed something it borrowed.
```

#### 7.2 DI registration

```csharp
var services = new ServiceCollection();
services.AddMarketDataClient(o => o.ApiToken = "token");
var provider = services.BuildServiceProvider();

var a = provider.GetRequiredService<MarketDataClient>();
var b = provider.GetRequiredService<MarketDataClient>();
// Verify: singleton — ReferenceEquals(a, b) is true.
```

---

## Reporting What You Find

For every bug, open an issue immediately. Include:

1. The area and scenario number from this document
2. Minimal reproduction code, complete with `using` directives and client construction
3. Expected versus actual behavior
4. The `SupportInfo` block when an exception was involved
5. SDK version, `dotnet --version`, and the **target framework** you observed it on
6. Whether it reproduces on `net8.0`, `net10.0`, or both

```bash
gh issue create --label "bug" \
  --title "[Bug]: GetCandlesAsync returns null Values for an empty window" \
  --body "$(cat <<'EOF'
**Area**: 2.1 Empty result window
**Reproduces on**: net8.0 and net10.0
...
EOF
)"
```

Then hand off to [ISSUE_WORKFLOW.md](./ISSUE_WORKFLOW.md).

---

## Coverage Note

This repo enforces **100% line and branch coverage**, so every code path already has *a*
test. That is exactly why this document targets behavior rather than reachability: full
coverage proves each line ran, not that it did the right thing. The bugs left to find here
are wrong answers on covered lines, disagreements between the two target frameworks, and
assumptions about the host environment — none of which a coverage number can see.
