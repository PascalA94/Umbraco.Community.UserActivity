using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;

namespace Umbraco.Community.UserActivity.DemoSite.Seed;

/// <summary>
/// Seeds three synthetic users and a few content items, including hostile names for the XSS and CSV-injection e2e
/// checks (Review Focus 3). Passwords come from configuration (<c>DemoSeed:Passwords:{Sam|Wren|Ed}</c>: user secrets
/// locally, environment variables in CI) and are never generated, logged or written here.
/// </summary>
public sealed class DemoSeeder(
    IHostEnvironment environment,
    IRuntimeState runtimeState,
    IConfiguration configuration,
    IUserService userService,
    IUserGroupService userGroupService,
    IContentTypeService contentTypeService,
    IContentService contentService,
    IShortStringHelper shortStringHelper,
    ILogger<DemoSeeder> logger) : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private const string DocTypeAlias = "demoPage";

    private static readonly (string Key, string Name, string Email, string Group)[] Users =
    [
        ("Sam", "Sam Admin", "sam@example.test", Constants.Security.AdminGroupAlias),
        ("Wren", "Wren Writer", "wren@example.test", "writer"),
        ("Ed", "Ed Editor", "ed@example.test", "editor"),
    ];

    // Hostile names on purpose: they must render as text and export with a formula prefix.
    private static readonly string[] ContentNames =
    [
        "Home",
        "About us",
        "<img src=x onerror=alert(1)>",
        "=HYPERLINK(\"http://x\")",
    ];

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment() || runtimeState.Level != RuntimeLevel.Run)
        {
            return;
        }

        foreach ((string key, string name, string email, string group) in Users)
        {
            await EnsureUserAsync(key, name, email, group);
        }

        await EnsureContentAsync();
    }

    private async Task EnsureUserAsync(string key, string name, string email, string groupAlias)
    {
        if (userService.GetByEmail(email) is not null)
        {
            return;
        }

        string? password = configuration[$"DemoSeed:Passwords:{key}"];
        if (string.IsNullOrEmpty(password))
        {
            logger.LogInformation("Demo seed: no password configured for {SeedUser}; skipped.", key);
            return;
        }

        IUserGroup? group = await userGroupService.GetAsync(groupAlias);
        if (group is null)
        {
            return;
        }

        Attempt<UserCreationResult, Umbraco.Cms.Core.Services.OperationStatus.UserOperationStatus> created =
            await userService.CreateAsync(
                Constants.Security.SuperUserKey,
                new UserCreateModel { Email = email, UserName = email, Name = name, UserGroupKeys = new HashSet<Guid> { group.Key } },
                approveUser: true);
        if (!created.Success || created.Result.CreatedUser is null)
        {
            logger.LogWarning("Demo seed: could not create {SeedUser} ({Status}).", key, created.Status);
            return;
        }

        var changed = await userService.ChangePasswordAsync(
            Constants.Security.SuperUserKey,
            new ChangeUserPasswordModel { UserKey = created.Result.CreatedUser.Key, NewPassword = password });
        if (!changed.Success)
        {
            logger.LogWarning("Demo seed: could not set the password for {SeedUser} ({Status}).", key, changed.Status);
        }
    }

    private async Task EnsureContentAsync()
    {
        IContentType? docType = contentTypeService.Get(DocTypeAlias);
        if (docType is null)
        {
            docType = new ContentType(shortStringHelper, Constants.System.Root)
            {
                Alias = DocTypeAlias,
                Name = "Demo page",
                Icon = "icon-document",
                AllowedAsRoot = true,
            };
            await contentTypeService.CreateAsync(docType, Constants.Security.SuperUserKey);
        }

        var existing = contentService.GetRootContent().Select(c => c.Name).ToHashSet(StringComparer.Ordinal);
        foreach (string name in ContentNames.Where(n => !existing.Contains(n)))
        {
            IContent content = contentService.Create(name, Constants.System.Root, docType);
            contentService.Save(content);
        }
    }
}
