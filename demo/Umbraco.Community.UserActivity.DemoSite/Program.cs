
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// One SQLite database per Umbraco line, so switching -p:UmbracoMajor never upgrades or downgrades the other line's data.
if (builder.Configuration.GetConnectionString("umbracoDbDSN") is { } dsn && dsn.Contains("Umbraco.sqlite.db", StringComparison.Ordinal))
{
#if UMBRACO_17
    const string line = "17";
#else
    const string line = "18";
#endif
    builder.Configuration["ConnectionStrings:umbracoDbDSN"] = dsn.Replace("Umbraco.sqlite.db", $"Umbraco{line}.sqlite.db", StringComparison.Ordinal);
}


builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .Build();

WebApplication app = builder.Build();

// Core writes invitation emails to the pickup folder but does not create it; the e2e invited-user spec needs invites.
if (builder.Configuration["Umbraco:CMS:Global:Smtp:PickupDirectoryLocation"] is { Length: > 0 } pickup)
{
    Directory.CreateDirectory(Path.Combine(builder.Environment.ContentRootPath, pickup));
}


await app.BootUmbracoAsync();


app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync();
