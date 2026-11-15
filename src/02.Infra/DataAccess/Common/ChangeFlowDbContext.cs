using Microsoft.EntityFrameworkCore;

namespace ChangeFlow.Infra.DataAccess.Common;

public sealed class ChangeFlowDbContext(DbContextOptions<ChangeFlowDbContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChangeFlowDbContext).Assembly);
    }
}
