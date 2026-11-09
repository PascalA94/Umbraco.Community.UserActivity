using System.Globalization;
using Microsoft.Extensions.Options;
using Umbraco.Community.UserActivity.Configuration;
using Umbraco.Community.UserActivity.Query;
using Umbraco.Community.UserActivity.Sources;

namespace Umbraco.Community.UserActivity.Controllers;

/// <summary>
/// Validates list and export query strings (design 5, 7.3). Everything is bound as strings so a bad value never
/// reaches ASP.NET's model-state errors, which echo the value; our errors name the parameter only.
/// </summary>
public sealed class ActivityRequestParser
{
    public const int MaxTake = 100;
    public const int DefaultTake = 50;
    public const int MinSearch = 3;
    public const int MaxSearch = 100;

    private readonly SourceAvailability _availability;
    private readonly IOptionsMonitor<UserActivityOptions> _options;
    private readonly TimeProvider _time;

    public ActivityRequestParser(SourceAvailability availability, IOptionsMonitor<UserActivityOptions> options, TimeProvider time)
    {
        _availability = availability;
        _options = options;
        _time = time;
    }

    public ParseResult Parse(ActivityRequest request, Guid? userKey)
    {
        UserActivityOptions options = _options.CurrentValue;
        IReadOnlyList<IUserActivitySource> active = _availability.GetActive();

        if (!TryValues(request.Source, active.Select(s => s.Alias), out HashSet<string>? sources))
        {
            return ParseResult.Invalid("source");
        }

        if (!TryValues(request.Category, Categories.All, out HashSet<string>? categories))
        {
            return ParseResult.Invalid("category");
        }

        if (!TryValues(request.EntityType, active.SelectMany(s => s.Info.EntityTypes), out HashSet<string>? entityTypes))
        {
            return ParseResult.Invalid("entityType");
        }

        if (!TryInt(request.Skip, 0, out var skip) || skip < 0)
        {
            return ParseResult.Invalid("skip");
        }

        if (!TryInt(request.Take, DefaultTake, out var take) || take < 1 || take > MaxTake)
        {
            return ParseResult.Invalid("take");
        }

        bool ascending;
        switch (request.Order)
        {
            case null or "desc":
                ascending = false;
                break;
            case "asc":
                ascending = true;
                break;
            default:
                return ParseResult.Invalid("order");
        }

        if (!TryDate(request.From, out DateTimeOffset? from) || !InRange(from))
        {
            return ParseResult.Invalid("from");
        }

        if (!TryDate(request.To, out DateTimeOffset? to) || !InRange(to))
        {
            return ParseResult.Invalid("to");
        }

        DateWindowResult window = DateWindow.Resolve(from, to, _time, options.DefaultRangeDays, options.MaxRangeDays);
        if (window.Window is null)
        {
            return ParseResult.Invalid(window.ErrorParameter!);
        }

        string? search = request.Search;
        if (search is not null && (search.Length < MinSearch || search.Length > MaxSearch || search.Any(char.IsControl)))
        {
            return ParseResult.Invalid("search");
        }

        return new ParseResult(new ActivityQuery(userKey, sources!, categories!, entityTypes!, window.Window, search, skip, take, ascending), null);
    }

    /// <summary>Repeated parameter: every value must be one of the allowed ones (ordinal; aliases and categories are exact API values).</summary>
    private static bool TryValues(string[]? values, IEnumerable<string> allowed, out HashSet<string>? result)
    {
        var known = new HashSet<string>(allowed, StringComparer.Ordinal);
        result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values ?? [])
        {
            if (!known.Contains(value))
            {
                result = null;
                return false;
            }

            result.Add(value);
        }

        return true;
    }

    private static bool TryInt(string? value, int fallback, out int result)
    {
        result = fallback;
        return value is null || int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out result);
    }

    /// <summary>
    /// Dates from 2000 to a year ahead: anything else is a mistake, and extremes overflow date arithmetic (a 500) or
    /// fall below SQL Server's datetime range (M1 review SEC-3).
    /// </summary>
    private bool InRange(DateTimeOffset? value)
        => value is null || (value.Value >= EarliestDate && value.Value <= _time.GetUtcNow().AddYears(1));

    private static readonly DateTimeOffset EarliestDate = new(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static bool TryDate(string? value, out DateTimeOffset? result)
    {
        result = null;
        if (value is null)
        {
            return true;
        }

        if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTimeOffset parsed))
        {
            return false;
        }

        result = parsed;
        return true;
    }
}

/// <summary>Raw list query values, as strings (see <see cref="ActivityRequestParser"/>).</summary>
public sealed class ActivityRequest
{
    public string[]? Source { get; init; }

    public string[]? Category { get; init; }

    public string[]? EntityType { get; init; }

    public string? From { get; init; }

    public string? To { get; init; }

    public string? Search { get; init; }

    public string? Skip { get; init; }

    public string? Take { get; init; }

    public string? Order { get; init; }
}

public sealed record ParseResult(ActivityQuery? Query, string? InvalidParameter)
{
    public static ParseResult Invalid(string parameter) => new(null, parameter);
}
