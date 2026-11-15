using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChangeFlow.Infra.DataAccess.Users.Persistence;

internal sealed class UserRoleRecord
{
    public Guid UserId { get; set; }
    public string Role { get; set; } = string.Empty;
}

internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRoleRecord>
{
    public void Configure(EntityTypeBuilder<UserRoleRecord> builder)
    {
        builder.ToTable(
            "user_roles",
            table => table.HasCheckConstraint(
                "ck_user_roles_role",
                "\"role\" IN ('Requester', 'Reviewer')"));
        builder.HasKey(role => new { role.UserId, role.Role });
        builder.Property(role => role.UserId).HasColumnName("user_id").HasColumnType("TEXT");
        builder.Property(role => role.Role).HasColumnName("role").IsRequired();
        builder.HasOne<UserRecord>()
            .WithMany()
            .HasForeignKey(role => role.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
