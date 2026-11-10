using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.UserActivity.Sources;

namespace SampleSource;

/// <summary>Your package's own record of who sent which newsletter (stands in for your table or service).</summary>
public sealed record NewsletterSend(int Id, Guid SentBy, DateTime SentUtc, string Subject);

public interface INewsletterLog
{
    IReadOnlyList<NewsletterSend> Sends(DateTime fromUtc, DateTime toUtc);
}

/// <summary>A third-party activity source: newsletter sends, on each sender's Activity tab and in the global view.</summary>
public sealed class NewsletterSource(INewsletterLog log) : IUserActivitySource
{
    public string Alias => "newsletter";                          // permanent; also UserActivity:Sources:newsletter:Enabled

    public string DisplayNameKey => "sampleNewsletter_sourceName"; // your own localisation key; the alias shows without one

    public bool IsAvailable => true;

    public ActivitySourceInfo Info { get; } = new(["newsletter"], [Categories.Other], CanCount: true, CanSearch: true, RetentionHint: null);

    public Task<ActivitySourcePage> QueryAsync(ActivitySourceQuery query, CancellationToken ct)
    {
        var matches = log.Sends(query.FromUtc.UtcDateTime, query.ToUtc.UtcDateTime)
            .Where(s => query.User is null || s.SentBy == query.User.Key)
            .Where(s => query.Search is null || s.Subject.Contains(query.Search, StringComparison.OrdinalIgnoreCase))
            .Where(s => query.Categories.Count == 0 || query.Categories.Contains(Categories.Other))
            .Where(s => query.EntityTypes.Count == 0 || query.EntityTypes.Contains("newsletter"))
            .Select(ToEvent)
            .Order(query.Ascending ? ActivityEventOrder.Ascending : ActivityEventOrder.Descending) // the order the merge needs
            .ToList();
        return Task.FromResult(new ActivitySourcePage(matches.Take(query.Limit).ToList(), matches.Count, matches.Count > query.Limit));
    }

    public Task<ActivityEvent?> GetAsync(string id, CancellationToken ct)
        => Task.FromResult(log.Sends(DateTime.MinValue, DateTime.MaxValue).Select(ToEvent).FirstOrDefault(e => e.Id == id));

    // Everything here is shown as plain text. Never put an IP address in Details.
    private ActivityEvent ToEvent(NewsletterSend send) => new(
        Alias, $"send:{send.Id}", new DateTimeOffset(send.SentUtc, TimeSpan.Zero),
        new ActivityActor(ActorKind.User, send.SentBy, null, null, null),
        Categories.Other, "NewsletterSent",
        new ActivityEntity("newsletter", null, send.Id.ToString(), send.Subject, true),
        $"Sent \"{send.Subject}\"", null, null);
}

public sealed class NewsletterSourceComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) => builder.UserActivitySources().Append<NewsletterSource>();
}
