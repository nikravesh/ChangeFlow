using ChangeFlow.Infra.DataAccess.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ChangeFlow.Tests.ChangeRequests.Infrastructure;

public sealed class ChangeRequestApiFixtureTests : IClassFixture<ChangeRequestApiFixture>
{
    private readonly ChangeRequestApiFixture fixture;

    public ChangeRequestApiFixtureTests(ChangeRequestApiFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Fixture_provisions_distinct_accounts_and_uses_the_controlled_clock()
    {
        var services = fixture.Factory.Services;
        var contextFactory = services.GetRequiredService<IDbContextFactory<ChangeFlowDbContext>>();
        await using var context = await contextFactory.CreateDbContextAsync();
        var connection = new SqliteConnectionStringBuilder(context.Database.GetConnectionString());
        Assert.True(Path.IsPathFullyQualified(connection.DataSource));
        Assert.True(File.Exists(connection.DataSource));
        Assert.True(connection.ForeignKeys);

        Guid[] ids = [fixture.Requester.Value, fixture.Reviewer.Value,
            fixture.SecondReviewer.Value, fixture.DualRoleUser.Value];
        Assert.DoesNotContain(Guid.Empty, ids);
        Assert.Equal(4, ids.Distinct().Count());

        var clock = services.GetRequiredService<TimeProvider>();
        var initialTime = clock.GetUtcNow();
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(initialTime.AddMinutes(1), clock.GetUtcNow());

        var token = new JsonWebToken(fixture.CreateAccessToken(fixture.Requester));
        Assert.Equal(fixture.Requester.ToString(), token.Subject);
        Assert.Equal(ChangeRequestApiFixture.Issuer, token.Issuer);
        Assert.Contains(ChangeRequestApiFixture.Audience, token.Audiences);
        Assert.Equal(clock.GetUtcNow().ToUnixTimeSeconds(),
            new DateTimeOffset(token.ValidFrom).ToUnixTimeSeconds());
    }
}
