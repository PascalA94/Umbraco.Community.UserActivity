using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using NSubstitute;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Community.UserActivity.Recorder;
using Umbraco.Community.UserActivity.Recorder.Handlers;
using Umbraco.Community.UserActivity.Sources;

namespace Umbraco.Community.UserActivity.Tests.Recorder.Handlers;

public class RecorderHandlerContextTests
{
    private const string Sentinel = "SentinelPersonalData";
    private static readonly Guid Wren = Guid.Parse("2b0c0000-0000-4000-8000-0000000000bb");

    private readonly List<RecorderEntry> _written = [];
    private readonly IRecorderWriter _writer = Substitute.For<IRecorderWriter>();
    private readonly IBackOfficeSecurityAccessor _security = Substitute.For<IBackOfficeSecurityAccessor>();
    private readonly FakeLogger<RecorderHandlerContext> _logger = new();

    public RecorderHandlerContextTests()
        => _writer.When(w => w.Write(Arg.Any<RecorderEntry>())).Do(c => _written.Add(c.Arg<RecorderEntry>()));

    private RecorderHandlerContext Context() => new(_writer, _security, _logger);

    private void SignedInAs(Guid key, string name)
    {
        IUser user = Substitute.For<IUser>();
        user.Key.Returns(key);
        user.Name.Returns(name);
        IBackOfficeSecurity security = Substitute.For<IBackOfficeSecurity>();
        security.CurrentUser.Returns(user);
        _security.BackOfficeSecurity.Returns(security);
    }

    private static RecorderEntry Entry(string name = "Main policy")
        => new("csp", Categories.Saved, "CspSavedNotification", null, null, "cspPolicy", "k-1", name, null);

    [Fact]
    public void Record_StampsTheBackOfficeUser_FromTheAccessor()
    {
        SignedInAs(Wren, "Wren Writer");

        Context().Record("csp", () => [Entry()]);

        RecorderEntry row = Assert.Single(_written);
        Assert.Equal(Wren, row.UserKey);
        Assert.Equal("Wren Writer", row.UserName);
        Assert.Equal("Main policy", row.EntityName);
    }

    [Fact]
    public void Record_NoBackOfficeSecurity_StoresNullUser()
    {
        _security.BackOfficeSecurity.Returns((IBackOfficeSecurity?)null);

        Context().Record("csp", () => [Entry()]);

        RecorderEntry row = Assert.Single(_written);
        Assert.Null(row.UserKey);
        Assert.Null(row.UserName);
    }

    [Fact]
    public void Record_NoCurrentUser_StoresNullUser()
    {
        IBackOfficeSecurity security = Substitute.For<IBackOfficeSecurity>();
        security.CurrentUser.Returns((IUser?)null);
        _security.BackOfficeSecurity.Returns(security);

        Context().Record("csp", () => [Entry()]);

        Assert.Null(Assert.Single(_written).UserKey);
    }

    [Fact]
    public void Record_ManyEntries_OneRowEach()
    {
        SignedInAs(Wren, "Wren Writer");

        Context().Record("urlTracker", () => [Entry("a"), Entry("b"), Entry("c")]);

        Assert.Equal(["a", "b", "c"], _written.Select(r => r.EntityName));
        Assert.All(_written, r => Assert.Equal(Wren, r.UserKey));
    }

    [Fact]
    public void Record_ThrowingWriter_NeverThrowsIntoTheCaller_AndLogsTypeOnly()
    {
        SignedInAs(Wren, "Wren Writer");
        _writer.When(w => w.Write(Arg.Any<RecorderEntry>())).Do(_ => throw new InvalidOperationException(Sentinel + " in the message"));

        Exception? thrown = Record.Exception(() => Context().Record("csp", () => [Entry(Sentinel)]));

        Assert.Null(thrown);
        AssertOneTypeOnlyWarning("csp", nameof(InvalidOperationException));
    }

    [Fact]
    public void Record_ThrowingBuilder_NeverThrowsIntoTheCaller_AndLogsTypeOnly()
    {
        Exception? thrown = Record.Exception(() => Context().Record("workflow", () => throw new NullReferenceException(Sentinel)));

        Assert.Null(thrown);
        Assert.Empty(_written);
        AssertOneTypeOnlyWarning("workflow", nameof(NullReferenceException));
    }

    [Fact]
    public void Record_ThrowingAccessor_StillStoresTheEvent_WithoutAUser()
    {
        _security.BackOfficeSecurity.Returns(_ => throw new InvalidOperationException(Sentinel));

        Exception? thrown = Record.Exception(() => Context().Record("csp", () => [Entry()]));

        Assert.Null(thrown);
        Assert.Null(Assert.Single(_written).UserKey);
    }

    private void AssertOneTypeOnlyWarning(string source, string exceptionType)
    {
        FakeLogRecord warning = Assert.Single(_logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains(source, warning.Message);
        Assert.Contains(exceptionType, warning.Message);
        Assert.DoesNotContain(Sentinel, warning.Message);
        Assert.Null(warning.Exception);
        Assert.DoesNotContain(Sentinel, string.Concat(warning.StructuredState!.Select(p => p.Value)));
    }
}
