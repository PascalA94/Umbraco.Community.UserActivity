using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Management.Controllers;
using Umbraco.Cms.Api.Management.Routing;

namespace Umbraco.Community.UserActivity.IntegrationTests.Api;

public class ControllerConventionTests
{
    private static readonly Type[] Controllers = typeof(Umbraco.Community.UserActivity.Sources.IUserActivitySource).Assembly.GetTypes()
        .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract)
        .ToArray();

    [Test]
    public void AllControllers_AreManagementApi_NoAnonymous()
    {
        Assert.That(Controllers, Is.Not.Empty);
        foreach (Type controller in Controllers)
        {
            Assert.That(typeof(ManagementApiControllerBase).IsAssignableFrom(controller), controller.Name);
            Assert.That(controller.GetCustomAttribute<VersionedApiBackOfficeRouteAttribute>(inherit: true), Is.Not.Null, controller.Name);
            Assert.That(controller.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true), Is.Empty, controller.Name);
            foreach (MethodInfo action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                Assert.That(action.GetCustomAttribute<AllowAnonymousAttribute>(), Is.Null, $"{controller.Name}.{action.Name}");
                Assert.That(action.GetCustomAttribute<HttpGetAttribute>(), Is.Not.Null, $"{controller.Name}.{action.Name}: GET only");
                var policies = controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
                    .Concat(action.GetCustomAttributes<AuthorizeAttribute>())
                    .Select(a => a.Policy)
                    .ToList();
                Assert.That(policies, Has.Some.StartsWith("UserActivity."), $"{controller.Name}.{action.Name}");
            }
        }
    }
}
