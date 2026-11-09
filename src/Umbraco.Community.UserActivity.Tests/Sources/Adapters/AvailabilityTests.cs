using System.Reflection;
using Umbraco.Community.UserActivity.Sources.Adapters;

namespace Umbraco.Community.UserActivity.Tests.Sources.Adapters;

public class AvailabilityTests
{
    [Fact]
    public void Guard_ByName_LoadedAssembly_True()
        => Assert.True(OptionalAssemblyGuard.IsLoaded(typeof(AvailabilityTests).Assembly.GetName().Name!));

    [Fact]
    public void Guard_ByName_NeverLoadsTypes()
    {
        // The guard must answer from already-loaded assembly names only: no Assembly.Load, no resolve, no type use.
        var loads = new List<string>();
        var resolves = new List<string>();
        AssemblyLoadEventHandler onLoad = (_, e) => loads.Add(e.LoadedAssembly.GetName().Name!);
        ResolveEventHandler onResolve = (_, e) => { resolves.Add(e.Name); return null; };
        AppDomain.CurrentDomain.AssemblyLoad += onLoad;
        AppDomain.CurrentDomain.AssemblyResolve += onResolve;
        try
        {
            Assert.False(OptionalAssemblyGuard.IsLoaded("Umbraco.AI.Core"));
            Assert.False(OptionalAssemblyGuard.IsLoaded("uSync.History"));
        }
        finally
        {
            AppDomain.CurrentDomain.AssemblyLoad -= onLoad;
            AppDomain.CurrentDomain.AssemblyResolve -= onResolve;
        }

        // The events are process-wide and xUnit runs other classes in parallel, so only the probed names count.
        string[] probed = ["Umbraco.AI.Core", "uSync.History"];
        Assert.DoesNotContain(loads, probed.Contains);
        Assert.DoesNotContain(resolves, r => probed.Any(p => r.StartsWith(p, StringComparison.Ordinal)));
        Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), a => a.GetName().Name == "Umbraco.AI.Core");
    }

    [Fact]
    public void Probe_AllMembersPresent_Ok()
    {
        ProbeResult result = AdapterProbe.Check(typeof(ProbeTarget).Assembly,
        [
            MemberSpec.Type(typeof(ProbeTarget).FullName!),
            MemberSpec.Method(typeof(ProbeTarget).FullName!, nameof(ProbeTarget.GetPagedAsync), "System.Int32", "System.Threading.CancellationToken"),
            MemberSpec.Property(typeof(ProbeTarget).FullName!, nameof(ProbeTarget.Name)),
        ]);

        Assert.True(result.Ok);
        Assert.Null(result.MissingMember);
    }

    [Theory]
    [InlineData("Missing.Type", null, "Missing.Type")]
    [InlineData(null, "NoSuchMethod", "ProbeTarget.NoSuchMethod")]
    public void Probe_MissingMember_ReportsNameOnly(string? typeName, string? method, string expected)
    {
        string type = typeName ?? typeof(ProbeTarget).FullName!;
        MemberSpec spec = method is null ? MemberSpec.Type(type) : MemberSpec.Method(type, method);

        ProbeResult result = AdapterProbe.Check(typeof(ProbeTarget).Assembly, [spec]);

        Assert.False(result.Ok);
        Assert.EndsWith(expected, result.MissingMember);
    }

    [Fact]
    public void Probe_ChangedSignature_IsMissing()
    {
        ProbeResult result = AdapterProbe.Check(typeof(ProbeTarget).Assembly,
            [MemberSpec.Method(typeof(ProbeTarget).FullName!, nameof(ProbeTarget.GetPagedAsync), "System.String")]);

        Assert.False(result.Ok);
        Assert.Contains(nameof(ProbeTarget.GetPagedAsync), result.MissingMember);
    }

    [Fact]
    public void Probe_ChangedReturnType_IsMissing()
    {
        ProbeResult result = AdapterProbe.Check(typeof(ProbeTarget).Assembly,
            [MemberSpec.Property(typeof(ProbeTarget).FullName!, nameof(ProbeTarget.Name), "System.Int32")]);

        Assert.False(result.Ok);
    }

    public sealed class ProbeTarget
    {
        public string Name { get; set; } = string.Empty;

        public Task<int> GetPagedAsync(int take, CancellationToken ct) => Task.FromResult(take);
    }
}
