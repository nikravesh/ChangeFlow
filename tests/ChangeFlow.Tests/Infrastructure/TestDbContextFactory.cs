using ChangeFlow.Infra.DataAccess.Common;
using Microsoft.EntityFrameworkCore;

namespace ChangeFlow.Tests.Infrastructure;

public sealed class TestDbContextFactory(SqliteDatabaseFixture fixture)
    : IDbContextFactory<ChangeFlowDbContext>
{
    public ChangeFlowDbContext CreateDbContext() => fixture.CreateDbContext();
}
