// QuickStart - Market Data C#/.NET SDK
//
// Set MARKETDATA_TOKEN in the environment or in examples/QuickStart/.env before running.
// The example follows the resource and common workflows demonstrated by the Java SDK examples.

using MarketDataApp;
using MarketDataApp.Exceptions;
using MarketDataApp.Funds;
using MarketDataApp.Markets;
using MarketDataApp.Options;
using MarketDataApp.Stocks;

var options = MarketDataClientOptions.FromEnvironment();
if (string.IsNullOrWhiteSpace(options.ApiToken))
{
    Console.WriteLine("Set MARKETDATA_TOKEN in the environment, .env file or user secrets before running this example.");
    return;
}

using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var cancellationToken = cancellation.Token;

try
{
    // No HttpClient in sight: the SDK creates and owns one configured with its requirements
    // (default handler with the 2-second connect timeout; the SDK's fixed 99-second request
    // timeout governs each attempt). CreateAsync also validates the token with /user/ and seeds
    // the rate-limit snapshot before the first data request, without blocking; the plain
    // constructor runs the same validation as a blocking call, and ValidateTokenOnStartup =
    // false skips it. Disposing the client also disposes the SDK-owned HttpClient.
    using var client = await MarketDataClient.CreateAsync(options, cancellationToken);

    // Stocks: a single quote, a candle window, and one batched request for several symbols.
    var quoteResponse = await client.Stocks.GetQuoteAsync("AAPL", cancellationToken: cancellationToken);
    var quote = quoteResponse.Values.FirstOrDefault();
    Console.WriteLine($"AAPL quote: last={quote?.Last} bid/ask={quote?.Bid}/{quote?.Ask}");

    var candles = await client.Stocks.GetCandlesAsync(
        StockResolution.Daily,
        "MSFT",
        countback: 5,
        cancellationToken: cancellationToken);
    Console.WriteLine($"MSFT candles: {candles.Values.Count}");

    var bulkQuotes = await client.Stocks.GetQuotesAsync(
        ["AAPL", "MSFT", "GOOG"],
        cancellationToken: cancellationToken);
    foreach (var row in bulkQuotes.Values)
    {
        Console.WriteLine($"  {row.Symbol}: mid={row.Mid}");
    }

    // Funds and markets: typed historical NAV and market-calendar responses.
    var fundCandles = await client.Funds.GetCandlesAsync(
        FundResolution.Daily,
        "VFINX",
        countback: 5,
        cancellationToken: cancellationToken);
    Console.WriteLine($"VFINX NAV candles: {fundCandles.Values.Count}");

    var marketStatus = await client.Markets.GetStatusAsync(
        country: "US",
        countback: 5,
        cancellationToken: cancellationToken);
    Console.WriteLine($"US market days returned: {marketStatus.Values.Count}");

    // Options: list expirations, filter a chain, then quote a contract from that chain.
    var expirations = await client.Options.GetExpirationsAsync(
        "AAPL",
        cancellationToken: cancellationToken);
    Console.WriteLine($"AAPL expirations: {expirations.Values.Count}");

    var chain = await client.Options.GetChainAsync(
        "AAPL",
        expiration: ExpirationFilter.ForDte(45),
        strike: StrikeFilter.ForRange(150, 250),
        side: OptionSide.Call,
        strikeLimit: 5,
        cancellationToken: cancellationToken);
    Console.WriteLine($"Filtered option chain: {chain.Values.Count} contracts");

    var optionSymbol = chain.Values.FirstOrDefault()?.OptionSymbol;
    if (!string.IsNullOrWhiteSpace(optionSymbol))
    {
        var optionQuote = await client.Options.GetQuoteAsync(
            optionSymbol,
            cancellationToken: cancellationToken);
        Console.WriteLine($"Option quote: {optionQuote.Values.FirstOrDefault()?.OptionSymbol}");
    }

    // Utilities: public service status, account information, and echoed request headers.
    var services = await client.Utilities.GetStatusAsync(cancellationToken);
    Console.WriteLine($"API services online: {services.Values.Count(service => service.Online)}");

    var user = await client.Utilities.GetUserAsync(cancellationToken);
    Console.WriteLine($"Quota: {user.Values.RequestsRemaining}/{user.Values.RequestsLimit} remaining");

    var headers = await client.Utilities.GetHeadersAsync(cancellationToken);
    Console.WriteLine($"Headers echoed by the API: {headers.Values.Count}");

    // Response metadata and CSV output.
    Console.WriteLine($"Request {quoteResponse.RequestId ?? "(no request id)"} returned HTTP {quoteResponse.StatusCode}");
    var csv = await client.Stocks.GetPricesCsvAsync(
        ["AAPL", "MSFT"],
        new MarketDataRequestOptions { Headers = true, Human = true },
        cancellationToken);
    var csvPath = Path.Combine(Path.GetTempPath(), "market-data-quickstart.csv");
    await csv.SaveToFileAsync(csvPath, cancellationToken);
    Console.WriteLine($"Saved CSV response to {csvPath}");
    File.Delete(csvPath);

    // Async fan-out: all requests share the client's retry, rate-limit, and concurrency behavior.
    var parallelStatuses = await Task.WhenAll(
        client.Utilities.GetStatusAsync(cancellationToken),
        client.Utilities.GetStatusAsync(cancellationToken),
        client.Utilities.GetStatusAsync(cancellationToken));
    Console.WriteLine($"Completed {parallelStatuses.Length} status requests concurrently");
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("The request was cancelled.");
}
catch (RateLimitException ex)
{
    var retryAfter = ex.RetryAfter is { } delay ? $" Retry after {delay.TotalSeconds:F0}s." : string.Empty;
    Console.Error.WriteLine($"Rate limited: {ex.Message}.{retryAfter}");
}
catch (AuthenticationException ex)
{
    Console.Error.WriteLine($"Authentication failed ({ex.StatusCode}): {ex.Message}");
}
catch (MarketDataException ex)
{
    Console.Error.WriteLine($"{ex.ExceptionType} ({ex.StatusCode}): {ex.Message}");
    Console.Error.WriteLine(ex.SupportInfo);
}
