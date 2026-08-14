// OptionsChainMonitor — a live options-chain monitor built on the Market Data C#/.NET SDK.
//
// What it shows that Watchlist does not:
//  - The typed chain filters: ExpirationFilter.ForDte targets the expiration closest to a
//    days-to-expiration horizon, strikeLimit bounds the strikes around the money, and
//    OptionSide narrows to calls or puts.
//  - The OptionQuote model: bid/ask/mid, volume, open interest, implied volatility, delta,
//    the in-the-money flag, and the underlying price.
//  - Quota awareness specific to chains: a chain response bills roughly per contract
//    returned, so the defaults keep the filters tight and the refresh slow. Widen them
//    deliberately, not by accident.
//
// Usage:
//   dotnet run                                    monitor AAPL, ~30 DTE, 4 strikes, both sides
//   dotnet run -- SPY --dte 7 --strikes 6         custom underlying and filters
//   dotnet run -- AAPL --side call --interval 30  calls only, faster refresh
//   dotnet run -- AAPL --once                     single snapshot, then exit
//
// Token: set MARKETDATA_TOKEN via an environment variable, a .env file in the working
// directory, or user secrets (dotnet user-secrets set "MARKETDATA_TOKEN" "your-api-token").

using MarketDataApp;
using MarketDataApp.Exceptions;
using MarketDataApp.Options;

var symbol = "AAPL";
var dte = 30;
var strikeLimit = 4;
OptionSide? side = null;
var interval = TimeSpan.FromSeconds(60);
var once = false;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--dte" when i + 1 < args.Length && int.TryParse(args[i + 1], out var days):
            dte = Math.Max(0, days);
            i++;
            break;
        case "--strikes" when i + 1 < args.Length && int.TryParse(args[i + 1], out var strikes):
            strikeLimit = Math.Max(1, strikes);
            i++;
            break;
        case "--side" when i + 1 < args.Length:
            side = args[i + 1].ToLowerInvariant() switch
            {
                "call" => OptionSide.Call,
                "put" => OptionSide.Put,
                _ => null
            };
            i++;
            break;
        case "--interval" when i + 1 < args.Length && int.TryParse(args[i + 1], out var seconds):
            // Chains are the SDK's heaviest responses quota-wise; keep the floor high.
            interval = TimeSpan.FromSeconds(Math.Max(15, seconds));
            i++;
            break;
        case "--once":
            once = true;
            break;
        default:
            symbol = args[i].ToUpperInvariant();
            break;
    }
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
    // One-line setup: the SDK creates and owns the transport, loads MARKETDATA_* from the
    // environment, validates the token with /user/, and seeds the rate-limit snapshot.
    using var client = await MarketDataClient.CreateAsync(cancellationToken: cancellation.Token);

    while (true)
    {
        try
        {
            // One filtered chain request per refresh. ForDte picks the expiration closest to
            // the requested horizon; strikeLimit keeps the response (and the billing) tight.
            var chain = await client.Options.GetChainAsync(
                symbol,
                expiration: ExpirationFilter.ForDte(dte),
                side: side,
                strikeLimit: strikeLimit,
                cancellationToken: cancellation.Token);
            Render(symbol, dte, chain.Values, client.LatestRateLimit);
        }
        catch (RateLimitException exception)
        {
            var wait = exception.RetryAfter ?? interval;
            Console.Error.WriteLine($"Rate limited; next refresh in {wait.TotalSeconds:F0}s.");
            await Task.Delay(wait, cancellation.Token);
            continue;
        }
        catch (NetworkException exception)
        {
            // The SDK already retried with backoff; a persistent failure is a skipped tick.
            Console.Error.WriteLine($"Network problem, skipping refresh: {exception.Message}");
        }
        catch (MarketDataException exception)
        {
            Console.Error.WriteLine($"{exception.ExceptionType} ({exception.StatusCode}): {exception.Message}");
            Console.Error.WriteLine(exception.SupportInfo);
            return 1;
        }

        if (once)
        {
            return 0;
        }

        await Task.Delay(interval, cancellation.Token);
    }
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

static void Render(string symbol, int dte, IReadOnlyList<OptionQuote> contracts, RateLimitSnapshot? rateLimit)
{
    if (!Console.IsOutputRedirected)
    {
        Console.Clear();
    }

    var first = contracts.FirstOrDefault();
    var header = first is null
        ? $"{symbol} — no contracts matched the filters"
        : $"{symbol} @ {Price(first.UnderlyingPrice)} — expiration {first.Expiration:yyyy-MM-dd} " +
          $"({first.Dte?.ToString() ?? "?"} DTE, requested ~{dte})";
    Console.WriteLine($"{header}  —  {DateTimeOffset.Now:HH:mm:ss} local  (Ctrl+C to quit)");
    Console.WriteLine();
    Console.WriteLine(
        $"{"SIDE",-6}{"STRIKE",8}{"BID",9}{"ASK",9}{"MID",9}{"LAST",9}{"VOL",10}{"OI",10}{"IV",9}{"DELTA",8}  ITM");

    // In-the-money rows are highlighted; the chain arrives per side, re-sorted here so calls
    // and puts read as two contiguous ladders.
    foreach (var contract in contracts.OrderBy(c => c.Side).ThenBy(c => c.Strike))
    {
        var original = Console.ForegroundColor;
        if (contract.InTheMoney == true && !Console.IsOutputRedirected)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
        }

        Console.WriteLine(
            $"{contract.Side,-6}" +
            $"{Price(contract.Strike),8}" +
            $"{Price(contract.Bid),9}" +
            $"{Price(contract.Ask),9}" +
            $"{Price(contract.Mid),9}" +
            $"{Price(contract.Last),9}" +
            $"{Count(contract.Volume),10}" +
            $"{Count(contract.OpenInterest),10}" +
            $"{Percent(contract.Iv),9}" +
            $"{Ratio(contract.Delta),8}" +
            $"  {(contract.InTheMoney == true ? "*" : "")}");
        Console.ForegroundColor = original;
    }

    Console.WriteLine();
    Console.WriteLine($"{contracts.Count} contracts (each refresh bills roughly one credit per contract).");
    Console.WriteLine(rateLimit is null
        ? "Rate limit: (no snapshot yet)"
        : $"Rate limit: {rateLimit.Remaining:N0}/{rateLimit.Limit:N0} remaining, resets {rateLimit.Reset.ToLocalTime():HH:mm} local");
}

static string Price(decimal? value) => value?.ToString("F2") ?? "-";

static string Count(long? value) => value?.ToString("N0") ?? "-";

static string Percent(double? fraction) =>
    fraction is { } value ? (value * 100).ToString("F1") + "%" : "-";

static string Ratio(double? value) => value?.ToString("F2") ?? "-";
