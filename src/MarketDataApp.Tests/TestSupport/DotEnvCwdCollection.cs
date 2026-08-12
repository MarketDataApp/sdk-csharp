namespace MarketDataApp.Tests.TestSupport;

/// <summary>
/// Serializes every test that reads or writes the process-wide CWD <c>.env</c> file.
/// Tests that construct a client without options go through
/// <c>MarketDataClientOptions.FromEnvironment()</c>, which reads that file; when xUnit runs
/// their classes in parallel with the .env-writing fixtures, a torn read of a half-written
/// file surfaces as a spurious <c>FormatException</c>, and a delete inside the exists/read
/// window as an IO failure. Putting every participant in one collection means they never
/// overlap.
/// </summary>
[CollectionDefinition(Name)]
public sealed class DotEnvCwdCollection
{
    public const string Name = "dotenv-cwd";
}
