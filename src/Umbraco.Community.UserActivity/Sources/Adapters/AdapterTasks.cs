namespace Umbraco.Community.UserActivity.Sources.Adapters;

/// <summary>
/// Awaits an integration's task and maps the result to our own type. Adapters must never <c>await</c> an
/// integration's type directly: the compiler stores the awaiter (a struct over that type) in the async state
/// machine, and a type whose layout needs a missing assembly makes <c>Assembly.GetTypes()</c> throw, which fails
/// Umbraco's start-up type scan on every site without the integration (design 3.2). This method is generic, so its
/// state machine only names the integration's type when the JIT instantiates it, which happens only when installed.
/// </summary>
internal static class AdapterTasks
{
    public static async Task<TOut> Map<TIn, TOut>(Task<TIn> task, Func<TIn, TOut> map) => map(await task.ConfigureAwait(false));
}
