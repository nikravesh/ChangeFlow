using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChangeFlow.Infra.DataAccess.Users.Persistence;

internal sealed class UserRecord
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string LoginIdentifier { get; set; } = string.Empty;
    public string NormalizedLoginIdentifier { get; set; } = string.Empty;
}

internal sealed class UserConfiguration : IEntityTypeConfiguration<UserRecord>
{
    public void Configure(EntityTypeBuilder<UserRecord> builder)
    {
        builder.ToTable("users");
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).HasColumnName("id").HasColumnType("TEXT");
        builder.Property(user => user.DisplayName).HasColumnName("display_name").IsRequired();
        builder.Property(user => user.LoginIdentifier).HasColumnName("login_identifier").IsRequired();
        builder.Property(user => user.NormalizedLoginIdentifier)
            .HasColumnName("normalized_login_identifier")
            .IsRequired();
        builder.HasIndex(user => user.NormalizedLoginIdentifier)
            .IsUnique()
            .HasDatabaseName("ux_users_normalized_login_identifier");
    }
}
