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
        Data.Clear();
        var values = Environment.GetEnvironmentVariables();

        foreach (DictionaryEntry entry in values)
        {
            var key = (string)entry.Key;

            if (predicate(key))
            {
                Data[key.Replace("__", ConfigurationPath.KeyDelimiter)] =
                    entry.Value?.ToString();
            }
        }
    }
}

internal static class FilteredEnvironmentVariablesExtensions
{
    public static IConfigurationBuilder AddEnvironmentVariables(
        this IConfigurationBuilder builder,
        Func<string, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(predicate);

        return builder.Add(new FilteredEnvironmentVariablesSource(predicate));
    }

    public static IConfigurationBuilder AddEnvironmentVariables(
        this IConfigurationBuilder builder,
        string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);

        return AddEnvironmentVariables(builder, key =>
            key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
