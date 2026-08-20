using System.Xml.Linq;

namespace MarketDataApp.IntegrationTests;

/// <summary>
/// Verifies that every canonical docs URL shipped in the XML documentation responds successfully,
/// so a renamed or deleted docs page fails CI instead of silently shipping dead IntelliSense links.
/// </summary>
public sealed class DocsLivenessTests
{
    [IntegrationFact]
    public async Task CanonicalDocUrls_AreLive()
    {
        var xmlPath = Path.ChangeExtension(typeof(MarketDataClient).Assembly.Location, ".xml");
        Assert.True(File.Exists(xmlPath), $"XML documentation file not found at {xmlPath}.");

        var urls = XDocument.Load(xmlPath)
            .Descendants("seealso")
            .Select(element => (string?)element.Attribute("href"))
            .Where(href => href is not null
                && href.StartsWith("https://www.marketdata.app/docs/sdk/csharp/", StringComparison.Ordinal))
            .Select(href => href!)
            .Distinct()
            .Order()
            .ToList();
        Assert.NotEmpty(urls);

        // Until the docs site's staging -> main promotion, the C# pages exist only on the staging
        // host. MARKETDATA_DOCS_HOST rewrites the host under test without touching the URLs the
        // package ships; remove the variable from the workflows once production serves them.
        var hostOverride = Environment.GetEnvironmentVariable("MARKETDATA_DOCS_HOST");
        using var httpClient = new HttpClient();
        var failures = new List<string>();
        foreach (var url in urls)
        {
            var target = hostOverride is null
                ? url
                : url.Replace("www.marketdata.app", hostOverride, StringComparison.Ordinal);
            using var response = await httpClient.GetAsync(target, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                failures.Add($"{target} returned {(int)response.StatusCode}.");
            }
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}
