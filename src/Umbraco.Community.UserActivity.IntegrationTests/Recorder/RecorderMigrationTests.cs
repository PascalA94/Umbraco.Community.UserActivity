using Umbraco.Cms.Core.Packaging;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Persistence.SqlSyntax;
using Umbraco.Cms.Infrastructure.Scoping;
using Umbraco.Community.UserActivity.Recorder;

namespace Umbraco.Community.UserActivity.IntegrationTests.Recorder;

[TestFixture]
public class RecorderMigrationTests : RecorderTestBase
{
    private (bool Table, string[] Indexes) Schema()
    {
        using IScope scope = Scopes.CreateScope(autoComplete: true);
        ISqlSyntaxProvider syntax = scope.SqlContext.SqlSyntax;
        var indexes = syntax.GetDefinedIndexes(scope.Database)
            .Where(i => string.Equals(i.Item1, RecorderTable.Name, StringComparison.OrdinalIgnoreCase))
            .Select(i => i.Item2)
            .Distinct()
            .Order()
            .ToArray();
        return (syntax.DoesTableExist(scope.Database, RecorderTable.Name), indexes);
    }

    [Test]
    public async Task Migration_CreatesTableAndIndexes_Idempotent()
    {
        await ApplyMigrationPlanAsync();

        (bool table, string[] indexes) = Schema();
        Assert.That(table, Is.True);
        Assert.That(indexes, Is.SupersetOf(new[] { RecorderTable.OccurredIndex, RecorderTable.UserIndex }));

        // Existing data under the old shape: a second run (a re-run plan, a restored database) must not touch rows.
        Writer().Write(Entry());
        await ApplyMigrationPlanAsync();
        await ApplyMigrationPlanAsync();

        Assert.That(Count(), Is.EqualTo(1));
        Assert.That(Schema().Indexes, Is.SupersetOf(new[] { RecorderTable.OccurredIndex, RecorderTable.UserIndex }));
    }

    [Test]
    public async Task Migration_HalfApplied_CreatesOnlyWhatIsMissing_KeepsRows()
    {
        await ApplyMigrationPlanAsync();
        Writer().Write(Entry());
        using (IScope scope = Scopes.CreateScope(autoComplete: true))
        {
            scope.Database.Execute($"DROP INDEX {RecorderTable.UserIndex}" + (scope.SqlContext.DatabaseType.GetType().Name.Contains("SqlServer", StringComparison.OrdinalIgnoreCase) ? $" ON {RecorderTable.Name}" : string.Empty));
        }

        Assert.That(Schema().Indexes, Does.Not.Contain(RecorderTable.UserIndex));

        await ApplyMigrationPlanAsync();

        Assert.That(Schema().Indexes, Is.SupersetOf(new[] { RecorderTable.OccurredIndex, RecorderTable.UserIndex }));
        Assert.That(Count(), Is.EqualTo(1));
    }

    [Test]
    public void Migration_PlanIsFoundByUmbracosTypeScan()
        => Assert.That(GetRequiredService<PackageMigrationPlanCollection>().Select(p => p.GetType()), Does.Contain(typeof(RecorderMigrationPlan)));
}
