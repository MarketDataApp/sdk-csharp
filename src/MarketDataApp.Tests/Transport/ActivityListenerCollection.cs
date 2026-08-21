using Xunit;

namespace MarketDataApp.Tests.Transport;

/// <summary>
/// Serializes every test class that registers a process-global <c>ActivityListener</c>.
/// </summary>
/// <remarks>
/// <para><see cref="System.Diagnostics.ActivitySource.AddActivityListener"/> is process-wide, but
/// xUnit runs test classes in parallel. A listener registered by one class is therefore live
/// inside another class's test, which decides whether
/// <c>MarketDataDiagnostics.ActivitySource.StartActivity(...)</c> returns an activity or null.</para>
/// <para>That non-determinism is invisible to assertions — every test still passes — but it moves
/// branch coverage. The <c>activity?.</c> null-conditionals in <c>ApiClient</c>'s catch arms need
/// runs both with and without a listener, so when the windows overlap the wrong way one side goes
/// unvisited and the 100% branch gate fails on an unchanged tree (#74). It surfaced on
/// windows-latest and on ubuntu-latest, on net10.0, and re-running the same commit passed.</para>
/// <para>#47 addressed one instance of this by moving the affected test into
/// <c>TelemetryTests</c>, which only removed the race against that one class's listeners.
/// Membership in this collection removes it against all of them: xUnit runs the classes in a
/// collection sequentially, so no listener from one is ever live during another.</para>
/// <para>Any new test class that calls <c>AddActivityListener</c> must join this collection.</para>
/// </remarks>
[CollectionDefinition(Name)]
public sealed class ActivityListenerCollection
{
    /// <summary>The collection name to put on <c>[Collection(...)]</c>.</summary>
    public const string Name = "ActivityListener";
}
