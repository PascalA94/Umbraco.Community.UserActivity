using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Security;
using Umbraco.Community.UserActivity.Recorder;
using Umbraco.Community.UserActivity.Recorder.Handlers;
using Umbraco.Community.UserActivity.Sources.Adapters;

namespace Umbraco.Community.UserActivity.AdapterTests.Recorder;

/// <summary>
/// A guard registered into a real service collection against the real package, with the recorder writer replaced by a
/// recording substitute and the backoffice user by a stub. Notifications are published straight to whatever handlers the
/// guard registered, so a test proves the whole path: guard, registration, handler, user, row.
/// </summary>
internal sealed class HandlerHarness
{
    public static readonly Guid WrenKey = Guid.Parse("2b0c0000-0000-4000-8000-0000000000bb");

    private readonly ServiceProvider _provider;

    private HandlerHarness(Func<IUmbracoBuilder, RecorderHandlerRegistry, SourceAvailabilityResult> tryRegister, bool withUser, Exception? writerFailure)
    {
        var services = new ServiceCollection();
        IUmbracoBuilder builder = Substitute.For<IUmbracoBuilder>();
        builder.Services.Returns(services);

        IRecorderWriter writer = Substitute.For<IRecorderWriter>();
        writer.When(w => w.Write(Arg.Any<RecorderEntry>())).Do(call =>
        {
            if (writerFailure is not null)
            {
                throw writerFailure;
            }

            Written.Add(call.Arg<RecorderEntry>());
        });

        IBackOfficeSecurityAccessor accessor = Substitute.For<IBackOfficeSecurityAccessor>();
        if (withUser)
        {
            IUser user = Substitute.For<IUser>();
            user.Key.Returns(WrenKey);
            user.Name.Returns("Wren Writer");
            IBackOfficeSecurity security = Substitute.For<IBackOfficeSecurity>();
            security.CurrentUser.Returns(user);
            accessor.BackOfficeSecurity.Returns(security);
        }
        else
        {
            // Background work and API calls have no backoffice security; NSubstitute would otherwise auto-fill one.
            accessor.BackOfficeSecurity.Returns((IBackOfficeSecurity?)null);
        }

        services.AddSingleton(writer);
        services.AddSingleton(accessor);
        services.AddLogging();
        services.AddTransient<RecorderHandlerContext>();

        Registry = new RecorderHandlerRegistry();
        Result = tryRegister(builder, Registry);
        _provider = services.BuildServiceProvider();
    }

    public static HandlerHarness Create(Func<IUmbracoBuilder, RecorderHandlerRegistry, SourceAvailabilityResult> tryRegister, bool withUser = true, Exception? writerFailure = null)
        => new(tryRegister, withUser, writerFailure);

    public List<RecorderEntry> Written { get; } = [];

    public RecorderHandlerRegistry Registry { get; }

    public SourceAvailabilityResult Result { get; }

    public int HandlerCount<TNotification>()
        where TNotification : INotification
        => _provider.GetServices<INotificationHandler<TNotification>>().Count();

    /// <summary>What Umbraco's event aggregator does: every registered handler for the notification runs, from a scope.</summary>
    public void Publish<TNotification>(TNotification notification)
        where TNotification : INotification
    {
        using IServiceScope scope = _provider.CreateScope();
        foreach (INotificationHandler<TNotification> handler in scope.ServiceProvider.GetServices<INotificationHandler<TNotification>>())
        {
            handler.Handle(notification);
        }
    }

    /// <summary>The release under test, from the assembly metadata the project stamps in (a wildcard on the CI latest leg).</summary>
    public static string RuntimeVersion(string name)
        => typeof(HandlerHarness).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == name + "RuntimeVersion").Value!;

    public static void AssertIsRuntimeVersion(Assembly assembly, string name)
    {
        var expected = RuntimeVersion(name);
        if (!expected.Contains('*'))
        {
            Assert.StartsWith(expected.Split('-')[0], assembly.GetName().Version!.ToString());
        }
    }

    /// <summary>The allow-list rule in one place: every key a handler stored is one of these, and nothing else.</summary>
    public static void AssertOnlyKeys(RecorderEntry entry, params string[] allowed)
    {
        Assert.NotNull(entry.Details);
        Assert.All(entry.Details!.Keys, key => Assert.Contains(key, allowed));
    }

    /// <summary>A sentinel planted in a field a handler must never read must not appear anywhere in the stored row.</summary>
    public static void AssertNoTrace(RecorderEntry entry, string sentinel)
        => Assert.DoesNotContain(sentinel, string.Join('|', new[] { entry.Source, entry.Category, entry.RawType, entry.EntityType, entry.EntityKey, entry.EntityName }
            .Concat(entry.Details?.Select(d => d.Key + "=" + d.Value) ?? [])));
}
