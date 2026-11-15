using ChangeFlow.Infra.DataAccess.Common;
using ChangeFlow.Infra.DataAccess.Users.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace ChangeFlow.Infra.DataAccess.Migrations;

[DbContext(typeof(ChangeFlowDbContext))]
public sealed class ChangeFlowDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "11.0.0");
        new UserConfiguration().Configure(modelBuilder.Entity<UserRecord>());
        new UserCredentialConfiguration().Configure(modelBuilder.Entity<UserCredentialRecord>());
        new UserRoleConfiguration().Configure(modelBuilder.Entity<UserRoleRecord>());
    }
}
