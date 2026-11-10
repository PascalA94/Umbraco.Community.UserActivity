namespace Umbraco.Community.UserActivity.Sources;

/// <summary>
/// Start-up checks on the registered sources: valid, unique aliases, and claims that never overlap, so each stored
/// row belongs to exactly one source (design 3.6). These are programming errors, so they fail start-up.
/// </summary>
internal static class SourceRegistryValidator
{
    /// <summary>
    /// umbracoAudit namespaces the core source owns (users, groups, members, sign-ins) and our own view rows: no
    /// source may claim them or anything that contains them, or they would vanish from the core source.
    /// </summary>
    private static readonly string[] CoreOwnedPrefixes = ["umbraco/user/", "umbraco/user-group/", "umbraco/member/", "community/useractivity/"];

    /// <summary>
    /// umbracoLog entity types whose rows are core's (users, groups, members). Only a partial claim (header plus
    /// comment prefix, as Archived Users makes) may take some of them (M3 review QA-4).
    /// </summary>
    private static readonly string[] CoreOwnedLogEntityTypes = ["User", "UserGroup", "Member"];

    public static void Validate(IEnumerable<IUserActivitySource> sources)
    {
        var byAlias = new Dictionary<string, IUserActivitySource>(StringComparer.OrdinalIgnoreCase);
        var prefixes = new List<(string Prefix, string Alias)>();
        var entityTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var rowClaims = new List<(ClaimedLogRows Claim, string Alias)>();

        foreach (IUserActivitySource source in sources)
        {
            if (!SourceAlias.IsValid(source.Alias))
            {
                throw new InvalidOperationException(
                    $"User Activity source {source.GetType().FullName} has an invalid alias; it must match ^[a-z][a-zA-Z0-9]{{1,31}}$.");
            }

            if (byAlias.TryGetValue(source.Alias, out IUserActivitySource? existing))
            {
                throw new InvalidOperationException(
                    $"User Activity sources {existing.GetType().FullName} and {source.GetType().FullName} both use the alias '{source.Alias}'.");
            }

            byAlias[source.Alias] = source;

            if (source is not IClaimsCoreAuditRows claims)
            {
                continue;
            }

            foreach (var prefix in claims.ClaimedAuditEventTypePrefixes)
            {
                if (string.IsNullOrWhiteSpace(prefix) || CoreOwnedPrefixes.Any(core => Overlaps(core, prefix)))
                {
                    throw new InvalidOperationException(
                        $"User Activity source '{source.Alias}' claims umbracoAudit rows that are empty or belong to the core source.");
                }

                // SQL LIKE ignores case on default collations, so overlap is checked ignoring case too.
                (string Prefix, string Alias) clash = prefixes.FirstOrDefault(p => Overlaps(p.Prefix, prefix));
                if (clash.Alias is not null)
                {
                    throw new InvalidOperationException(
                        $"User Activity sources '{clash.Alias}' and '{source.Alias}' both claim umbracoAudit rows: '{clash.Prefix}' and '{prefix}' overlap.");
                }

                prefixes.Add((prefix, source.Alias));
            }

            foreach (var entityType in claims.ClaimedLogEntityTypes)
            {
                if (string.IsNullOrWhiteSpace(entityType))
                {
                    throw new InvalidOperationException($"User Activity source '{source.Alias}' claims an empty umbracoLog entity type.");
                }

                if (CoreOwnedLogEntityTypes.Contains(entityType, StringComparer.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"User Activity source '{source.Alias}' claims every umbracoLog '{entityType}' row; those belong to the core source. Claim a header and comment prefix instead.");
                }

                if (entityTypes.TryGetValue(entityType, out var owner))
                {
                    throw new InvalidOperationException(
                        $"User Activity sources '{owner}' and '{source.Alias}' both claim umbracoLog entity type '{entityType}'.");
                }

                entityTypes[entityType] = source.Alias;
            }

            foreach (ClaimedLogRows claim in claims.ClaimedLogRows)
            {
                if (string.IsNullOrWhiteSpace(claim.EntityType) || string.IsNullOrWhiteSpace(claim.LogHeader) || string.IsNullOrWhiteSpace(claim.CommentPrefix))
                {
                    throw new InvalidOperationException($"User Activity source '{source.Alias}' claims umbracoLog rows with an empty entity type, header or comment prefix.");
                }

                (ClaimedLogRows Claim, string Alias) clash = rowClaims.FirstOrDefault(r =>
                    string.Equals(r.Claim.EntityType, claim.EntityType, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(r.Claim.LogHeader, claim.LogHeader, StringComparison.OrdinalIgnoreCase)
                    && Overlaps(r.Claim.CommentPrefix, claim.CommentPrefix));
                if (clash.Alias is not null)
                {
                    throw new InvalidOperationException(
                        $"User Activity sources '{clash.Alias}' and '{source.Alias}' both claim umbracoLog '{claim.EntityType}' rows with overlapping comment prefixes.");
                }

                rowClaims.Add((claim, source.Alias));
            }
        }

        // A whole-type claim and a partial claim on the same entity type would both own those rows.
        foreach ((ClaimedLogRows claim, string alias) in rowClaims)
        {
            if (entityTypes.TryGetValue(claim.EntityType, out var owner) && owner != alias)
            {
                throw new InvalidOperationException(
                    $"User Activity sources '{owner}' and '{alias}' both claim umbracoLog entity type '{claim.EntityType}' rows.");
            }
        }
    }

    private static bool Overlaps(string a, string b)
        => a.StartsWith(b, StringComparison.OrdinalIgnoreCase) || b.StartsWith(a, StringComparison.OrdinalIgnoreCase);
}
