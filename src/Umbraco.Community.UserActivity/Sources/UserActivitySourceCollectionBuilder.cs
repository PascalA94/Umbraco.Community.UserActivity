using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Umbraco.Community.UserActivity.Sources;

/// <summary>Ordered collection of sources. Built-in sources register the same way third-party ones do.</summary>
public sealed class UserActivitySourceCollectionBuilder
    : OrderedCollectionBuilderBase<UserActivitySourceCollectionBuilder, UserActivitySourceCollection, IUserActivitySource>
{
    protected override UserActivitySourceCollectionBuilder This => this;

    /// <summary>Sources are singletons; each opens its own scope per call (pitfall P1).</summary>
    public override UserActivitySourceCollection CreateCollection(IServiceProvider factory)
    {
        UserActivitySourceCollection collection = base.CreateCollection(factory);
        SourceRegistryValidator.Validate(collection);
        return collection;
    }
}

public sealed class UserActivitySourceCollection : BuilderCollectionBase<IUserActivitySource>
{
    public UserActivitySourceCollection(Func<IEnumerable<IUserActivitySource>> items)
        : base(items)
    {
    }
}

public static class UserActivitySourceBuilderExtensions
{
    /// <summary>The source collection: <c>builder.UserActivitySources().Append&lt;MySource&gt;()</c>.</summary>
    public static UserActivitySourceCollectionBuilder UserActivitySources(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<UserActivitySourceCollectionBuilder>();
}
