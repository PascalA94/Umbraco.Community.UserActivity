using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Umbraco.Community.UserActivity.Configuration;
using Umbraco.Community.UserActivity.Controllers.Models;
using Umbraco.Community.UserActivity.Security;
using Umbraco.Community.UserActivity.Sources;

namespace Umbraco.Community.UserActivity.Controllers;

/// <summary>What the filter bar may offer: active sources only, with retention hints (design 3.2, 4.2).</summary>
[ApiVersion("1.0")]
public sealed class FilterOptionsController : UserActivityControllerBase
{
    private readonly SourceAvailability _availability;
    private readonly IOptionsMonitor<UserActivityOptions> _options;
    private readonly IAuthorizationService _authorization;

    public FilterOptionsController(SourceAvailability availability, IOptionsMonitor<UserActivityOptions> options, IAuthorizationService authorization)
    {
        _availability = availability;
        _options = options;
        _authorization = authorization;
    }

    [HttpGet("filter-options")]
    [MapToApiVersion("1.0")]
    [Authorize(Policy = UserActivityPolicy.SelfOrRead)]
    [EndpointName("GetActivityFilterOptions")]
    [ProducesResponseType(typeof(ActivityFilterOptionsResponseModel), StatusCodes.Status200OK)]
    [EndpointSummary("Gets the sources, categories and entity types the activity views can filter by.")]
    public async Task<IActionResult> GetActivityFilterOptions()
    {
        UserActivityOptions options = _options.CurrentValue;
        var isReader = await IsReaderAsync(_authorization);
        IReadOnlyList<IUserActivitySource> active = _availability.GetActive();

        return Ok(new ActivityFilterOptionsResponseModel
        {
            Sources = active.Select(s => new ActivitySourceOptionResponseModel
            {
                Alias = s.Alias,
                DisplayNameKey = s.DisplayNameKey,
                CanCount = s.Info.CanCount,
                CanSearch = s.Info.CanSearch,
                RetentionHintDays = s.Info.RetentionHint is { } hint ? (int)Math.Ceiling(hint.TotalDays) : null,
            }).ToArray(),
            Categories = [.. Categories.All],
            EntityTypes = active.SelectMany(s => s.Info.EntityTypes).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            DefaultRangeDays = options.DefaultRangeDays,
            MaxRangeDays = options.MaxRangeDays,
            MaxTake = ActivityRequestParser.MaxTake,
            CanReadAll = isReader,
            ExportEnabled = isReader && options.Export.Enabled,
            ShowIpAddresses = isReader && options.ShowIpAddresses,
        });
    }
}
