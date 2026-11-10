using System.Reflection;
using Umbraco.Community.UserActivity.Sources.Adapters;

namespace Umbraco.Community.UserActivity.Tests.Sources.Adapters;

/// <summary>
/// A hostile or odd integration assembly must never stop the site starting (design 3.2, final review SEC-F5): any
/// exception while probing or registering marks the source unavailable instead.
/// </summary>
public class AdapterSafetyTests
{
    private sealed class ThrowingAssembly : Assembly
    {
        public override AssemblyName GetName() => new("Odd.Integration") { Version = new Version(1, 2, 3) };

        public override Type? GetType(string name, bool throwOnError, bool ignoreCase) => throw new NotSupportedException("sentinel-detail");
    }

    [Fact]
    public void Probe_UnexpectedException_IsNotFound()
    {
        ProbeResult result = AdapterProbe.Check(new ThrowingAssembly(), [MemberSpec.Property("Odd.Type", "Name")]);

        Assert.False(result.Ok);
    }

    [Fact]
    public void Register_Throws_ProbeFailedNamingTheExceptionTypeOnly()
    {
        SourceAvailabilityResult result = AdapterRegistrations.Safely("ai", () => throw new NotSupportedException("sentinel-detail"));

        Assert.Equal(AdapterState.ProbeFailed, result.State);
        Assert.Equal("ai", result.Alias);
        Assert.Contains(nameof(NotSupportedException), result.MissingMember);
        Assert.DoesNotContain("sentinel", result.MissingMember);
    }

    [Fact]
    public void Register_Succeeds_ResultPassesThrough()
    {
        var ok = new SourceAvailabilityResult("ai", AdapterState.Registered, "1.0", null);

        Assert.Same(ok, AdapterRegistrations.Safely("ai", () => ok));
    }
}
