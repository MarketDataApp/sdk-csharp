using System.Collections;
using Microsoft.Extensions.Configuration;

namespace MarketDataApp.Extensions;

internal sealed class FilteredEnvironmentVariablesSource(Func<string, bool> predicate)
    : IConfigurationSource
{
    public IConfigurationProvider Build(IConfigurationBuilder builder) =>
        new FilteredEnvironmentVariablesProvider(predicate);
}

internal sealed class FilteredEnvironmentVariablesProvider(Func<string, bool> predicate)
    : ConfigurationProvider
{
    public override void Load()
    {
        // Cleared first so a reload drops keys whose variable is no longer in the environment.
        Data.Clear();

        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            var key = (string)entry.Key;
            if (predicate(key))
            {
                Data[key.Replace("__", ConfigurationPath.KeyDelimiter)] = (string?)entry.Value;
            }
        }
    }
}

internal static class FilteredEnvironmentVariablesExtensions
{
    /// <summary>
    /// Adds only the process environment variables matching <paramref name="predicate"/>, so
    /// the rest of the machine environment never reaches the configuration object.
    /// </summary>
    public static IConfigurationBuilder AddFilteredEnvironmentVariables(
        this IConfigurationBuilder builder,
        Func<string, bool> predicate) =>
        builder.Add(new FilteredEnvironmentVariablesSource(predicate));

    /// <summary>
    /// Adds only the process environment variables whose name starts with
    /// <paramref name="prefix"/>. Deliberately not named AddEnvironmentVariables: the
    /// framework extension with that signature STRIPS the prefix from the resulting key,
    /// while these keys keep it, which is what <see cref="MarketDataClientOptions"/> reads.
    /// </summary>
    public static IConfigurationBuilder AddPrefixedEnvironmentVariables(
        this IConfigurationBuilder builder,
        string prefix) =>
        builder.AddFilteredEnvironmentVariables(
            key => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
}
