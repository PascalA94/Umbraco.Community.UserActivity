using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Community.UserActivity.Controllers;
#if UMBRACO_17
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Umbraco.Cms.Api.Management.OpenApi;
#else
using Microsoft.AspNetCore.Routing;
using Umbraco.Cms.Api.Common.OpenApi;
using Umbraco.Cms.Api.Management.OpenApi;
using Umbraco.Cms.Core;
#endif

namespace Umbraco.Community.UserActivity.Web.OpenApi;

/// <summary>
/// The package's own OpenAPI document "user-activity" (design 5), as Archived Users does it per line: 17.x core uses
/// Swashbuckle, 18.x uses Microsoft.AspNetCore.OpenApi. Operation ids come from each action's [EndpointName].
/// </summary>
internal static class UserActivityOpenApi
{
    public static IUmbracoBuilder AddUserActivityOpenApi(this IUmbracoBuilder builder)
    {
#if UMBRACO_17
        builder.Services.ConfigureOptions<UserActivitySwaggerGenOptions>();
        return builder;
#else
        return builder.AddBackOfficeOpenApiDocument(
            UserActivityApi.DocumentName,
            document => document
                .WithTitle(UserActivityApi.GroupName)
                .WithBackOfficeAuthentication()
                .WithJsonOptions(Constants.JsonOptionsNames.BackOffice)
                // Core's operation-id transformer derives names from routes; ours are the permanent [EndpointName]s.
                .ConfigureOpenApiOptions(options => options.AddOperationTransformer((operation, context, _) =>
                {
                    if (context.Description.ActionDescriptor.EndpointMetadata.OfType<IEndpointNameMetadata>().FirstOrDefault() is { } name)
                    {
                        operation.OperationId = name.EndpointName;
                    }

                    return Task.CompletedTask;
                })));
#endif
    }
}

#if UMBRACO_17
/// <summary>Adds the document; core's DocInclusionPredicate assigns actions to it by [MapToApi].</summary>
internal sealed class UserActivitySwaggerGenOptions : IConfigureOptions<SwaggerGenOptions>
{
    public void Configure(SwaggerGenOptions options)
    {
        options.SwaggerDoc(UserActivityApi.DocumentName, new OpenApiInfo { Title = UserActivityApi.GroupName, Version = "Latest" });
        options.OperationFilter<UserActivitySecurityRequirementsOperationFilter>();
        options.OperationFilter<UserActivityOperationIdFilter>();
    }
}

internal sealed class UserActivitySecurityRequirementsOperationFilter : BackOfficeSecurityRequirementsOperationFilterBase
{
    protected override string ApiName => UserActivityApi.DocumentName;
}

/// <summary>Core names operations only in its own namespace; ours take the action's [EndpointName].</summary>
internal sealed class UserActivityOperationIdFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.DocumentName == UserActivityApi.DocumentName
            && context.ApiDescription.ActionDescriptor.EndpointMetadata.OfType<IEndpointNameMetadata>().FirstOrDefault() is { } name)
        {
            operation.OperationId = name.EndpointName;
        }
    }
}
#endif
