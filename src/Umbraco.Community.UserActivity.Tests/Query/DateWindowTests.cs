using Microsoft.Extensions.Time.Testing;
using Umbraco.Community.UserActivity.Query;

namespace Umbraco.Community.UserActivity.Tests.Query;

public class DateWindowTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void DefaultWindow_Last30Days()
    {
        DateWindow window = DateWindow.Resolve(null, null, new FakeTimeProvider(Now), defaultDays: 30, maxDays: 366).Window!;

        Assert.Equal(Now, window.ToUtc);
        Assert.Equal(Now.AddDays(-30), window.FromUtc);
    }

    [Fact]
    public void FromOnly_EndsNow_ToOnly_StartsDefaultBefore()
    {
        var provider = new FakeTimeProvider(Now);
        Assert.Equal(Now, DateWindow.Resolve(Now.AddDays(-3), null, provider, 30, 366).Window!.ToUtc);
        Assert.Equal(Now.AddDays(-40), DateWindow.Resolve(null, Now.AddDays(-10), provider, 30, 366).Window!.FromUtc);
    }

    [Fact]
    public void RangeOverMax_Error()
        => Assert.Equal("from", DateWindow.Resolve(Now.AddDays(-367), Now, new FakeTimeProvider(Now), 30, 366).ErrorParameter);

    [Fact]
    public void FromAfterTo_Error()
        => Assert.Equal("from", DateWindow.Resolve(Now, Now.AddDays(-1), new FakeTimeProvider(Now), 30, 366).ErrorParameter);

    [Fact]
    public void NonUtcOffsets_NormalisedToUtc()
    {
        var local = new DateTimeOffset(2026, 11, 9, 13, 0, 0, TimeSpan.FromHours(1));

        DateWindow window = DateWindow.Resolve(local.AddDays(-1), local, new FakeTimeProvider(Now), 30, 366).Window!;

        Assert.Equal(TimeSpan.Zero, window.ToUtc.Offset);
        Assert.Equal(Now, window.ToUtc);
    }
}
