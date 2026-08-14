// Watchlist — a live console watchlist built on the Market Data C#/.NET SDK.
//
// What it shows beyond QuickStart's endpoint tour:
//  - The one-line client setup: with no HttpClient supplied, the SDK creates and owns the
//    transport (default handler with the 2-second connect timeout, SDK-managed 99-second
//    request timeout) and validates the token at startup.
//  - Batched quotes on a refresh loop, with the client-wide rate-limit snapshot on screen.
//  - Taxonomy-aware error handling that keeps a long-running tool alive through transient
//    failures and stops it for terminal ones.
//  - CSV export through the paired *CsvAsync endpoint methods.
//
// Usage:
//   dotnet run                                    watch AAPL MSFT GOOG NVDA TSLA every 30s
//   dotnet run -- AAPL MSFT --interval 10         custom symbols and refresh interval
//   dotnet run -- AAPL --once                     single refresh, then exit
//   dotnet run -- --export AAPL aapl-daily.csv    save 30 daily candles as CSV, then exit
//
// Token: set MARKETDATA_TOKEN via an environment variable, a .env file in the working
// directory, or user secrets (dotnet user-secrets set "MARKETDATA_TOKEN" "your-api-token").

using MarketDataApp;
using MarketDataApp.Exceptions;
using MarketDataApp.Stocks;

var symbols = new List<string>();
var interval = TimeSpan.FromSeconds(30);
var once = false;
string? exportSymbol = null;
string? exportPath = null;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--interval" when i + 1 < args.Length && int.TryParse(args[i + 1], out var seconds):
            // Clamp to a quota-friendly floor; each refresh spends one batched request.
            interval = TimeSpan.FromSeconds(Math.Max(5, seconds));
            i++;
            break;
        case "--once":
            once = true;
            break;
        case "--export" when i + 2 < args.Length:
            exportSymbol = args[i + 1].ToUpperInvariant();
            exportPath = args[i + 2];
            i += 2;
            break;
        default:
            symbols.Add(args[i].ToUpperInvariant());
            break;
    }
}

if (symbols.Count == 0)
{
    symbols.AddRange(["AAPL", "MSFT", "GOOG", "NVDA", "TSLA"]);
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    // Cancel the loop instead of killing the process, so `using` disposals still run.
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

try
{
    // The one-liner: no HttpClient in sight. The SDK creates and owns the transport, loads
    // MARKETDATA_* configuration from the environment, validates the token with /user/, and
    // seeds the rate-limit snapshot before the first data request. Disposing the client also
    // disposes the SDK-owned HttpClient.
    using var client = await MarketDataClient.CreateAsync(cancellationToken: cancellation.Token);

    return exportSymbol is not null && exportPath is not null
        ? await ExportCandlesAsync(client, exportSymbol, exportPath, cancellation.Token)
        : await WatchAsync(client, symbols, interval, once, cancellation.Token);
}
catch (AuthenticationException exception)
{
    Console.Error.WriteLine($"Authentication failed: {exception.Message}");
    Console.Error.WriteLine("Set MARKETDATA_TOKEN (environment variable, .env, or user secrets) and retry.");
    return 1;
}
catch (OperationCanceledException)
{
    Console.WriteLine();
    Console.WriteLine("Stopped.");
    return 0;
}

static async Task<int> WatchAsync(
    MarketDataClient client,
    List<string> symbols,
    TimeSpan interval,
    bool once,
    CancellationToken cancellationToken)
{
    while (true)
    {
        try
        {
            // One batched request per refresh: every symbol in a single round trip.
            var response = await client.Stocks.GetQuotesAsync(
                symbols.ToArray(),
                cancellationToken: cancellationToken);
            Render(response.Values, client.LatestRateLimit);
        }
        catch (RateLimitException exception)
        {
            // The client refuses to send while the latest snapshot is exhausted; wait the
            // server-suggested window out instead of hammering the API.
            var wait = exception.RetryAfter ?? interval;
            Console.Error.WriteLine($"Rate limited; next refresh in {wait.TotalSeconds:F0}s.");
            await Task.Delay(wait, cancellationToken);
            continue;
        }
        catch (NetworkException exception)
        {
            // The SDK already retried with backoff before this surfaced; for a watchlist a
            // persistent network failure is just a skipped tick, not a reason to exit.
            Console.Error.WriteLine($"Network problem, skipping refresh: {exception.Message}");
        }
        catch (MarketDataException exception)
        {
            // Anything else in the taxonomy (bad request, server failure, parse error) is a
            // terminal misconfiguration for this tool; the support block carries the request
            // id and URL for a help ticket.
            Console.Error.WriteLine($"{exception.ExceptionType} ({exception.StatusCode}): {exception.Message}");
            Console.Error.WriteLine(exception.SupportInfo);
            return 1;
        }

        if (once)
        {
            return 0;
        }

        await Task.Delay(interval, cancellationToken);
    }
}

static async Task<int> ExportCandlesAsync(
    MarketDataClient client,
    string symbol,
    string path,
    CancellationToken cancellationToken)
{
    // The paired *CsvAsync methods return the API's CSV output as text; SaveToFileAsync
    // writes it and returns the path written.
    var csv = await client.Stocks.GetCandlesCsvAsync(
        StockResolution.Daily,
        symbol,
        countback: 30,
        options: new MarketDataRequestOptions { Headers = true },
        cancellationToken: cancellationToken);
    var written = await csv.SaveToFileAsync(path, cancellationToken);
    Console.WriteLine($"Saved 30 daily {symbol} candles to {written}");
    return 0;
}

static void Render(IReadOnlyList<StockQuote> quotes, RateLimitSnapshot? rateLimit)
{
    if (!Console.IsOutputRedirected)
    {
        Console.Clear();
    }

    Console.WriteLine($"Market Data watchlist — {DateTimeOffset.Now:HH:mm:ss} local  (Ctrl+C to quit)");
    Console.WriteLine();
    Console.WriteLine($"{"SYMBOL",-8}{"LAST",10}{"CHANGE",10}{"CHANGE%",10}{"VOLUME",16}  UPDATED (ET)");
    foreach (var quote in quotes)
    {
        var original = Console.ForegroundColor;
        if (quote.Change is { } change && !Console.IsOutputRedirected)
        {
            Console.ForegroundColor = change >= 0 ? ConsoleColor.Green : ConsoleColor.Red;
        }

        // Prices/sizes can be null when the columns parameter projects them away or the
        // backend reports NaN for closed/illiquid markets; ChangePct is fractional on the
        // wire (0.0123 == +1.23%). Updated is already America/New_York.
        Console.WriteLine(
            $"{quote.Symbol,-8}" +
            $"{Price(quote.Last),10}" +
            $"{Signed(quote.Change),10}" +
            $"{Percent(quote.ChangePct),10}" +
            $"{Count(quote.Volume),16}  " +
            $"{quote.Updated?.ToString("HH:mm:ss") ?? "-"}");
        Console.ForegroundColor = original;
    }

    Console.WriteLine();
    Console.WriteLine(rateLimit is null
        ? "Rate limit: (no snapshot yet)"
        : $"Rate limit: {rateLimit.Remaining:N0}/{rateLimit.Limit:N0} remaining, resets {rateLimit.Reset.ToLocalTime():HH:mm} local");
}

static string Price(decimal? value) => value?.ToString("F2") ?? "-";

static string Count(long? value) => value?.ToString("N0") ?? "-";

static string Signed(decimal? value) => value?.ToString("+0.00;-0.00;0.00") ?? "-";

static string Percent(double? fraction) =>
    fraction is { } value ? (value * 100).ToString("+0.00;-0.00;0.00") + "%" : "-";
