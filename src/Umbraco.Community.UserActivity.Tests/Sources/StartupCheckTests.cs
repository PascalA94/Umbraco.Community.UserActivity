using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core;
using Umbraco.Community.UserActivity.Configuration;
using Umbraco.Community.UserActivity.Sources;
using Umbraco.Community.UserActivity.Tests.Configuration;

namespace Umbraco.Community.UserActivity.Tests.Sources;

public class StartupCheckTests
{
    private static (UserActivityStartupCheck Check, FakeLogger<SourceAvailability> Logger) Create(UserActivityOptions options, params Type[] sources)
    {
        var builder = new UserActivitySourceCollectionBuilder();
        builder.Append(sources);
        var services = new ServiceCollection();
        builder.RegisterWith(services);
        services.AddSingleton<IOptionsMonitor<UserActivityOptions>>(new StaticOptionsMonitor<UserActivityOptions>(options));
        services.AddSingleton<SourceAvailability>();
        ServiceProvider provider = services.BuildServiceProvider();
        var logger = new FakeLogger<SourceAvailability>();
        return (new UserActivityStartupCheck(provider, logger), logger);
    }

    private static UmbracoApplicationStartingNotification Starting() => new(RuntimeLevel.Run, isRestarting: false);

    [Fact]
    public void Startup_DuplicateAlias_FailsBoot()
    {
        (UserActivityStartupCheck check, _) = Create(OptionsTests.Bind(), typeof(ComposerTests.DupOne), typeof(ComposerTests.DupTwo));

        Assert.Throws<InvalidOperationException>(() => check.Handle(Starting()));
    }

    [Fact]
    public void Startup_UnknownSetting_Warns()
    {
        UserActivityOptions options = OptionsTests.Bind(new Dictionary<string, string?> { ["UserActivity:Sources:Typo:Enabled"] = "false" });
        (UserActivityStartupCheck check, FakeLogger<SourceAvailability> logger) = Create(options, typeof(ComposerTests.DupOne));

        check.Handle(Starting());

        Assert.Single(logger.Collector.GetSnapshot());
    }
}
