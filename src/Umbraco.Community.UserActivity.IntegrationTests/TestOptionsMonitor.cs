using Microsoft.Extensions.Options;

namespace Umbraco.Community.UserActivity.IntegrationTests;

/// <summary>An options monitor over a fixed value.</summary>
internal sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
{
    public T CurrentValue { get; set; } = value;

    public T Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
