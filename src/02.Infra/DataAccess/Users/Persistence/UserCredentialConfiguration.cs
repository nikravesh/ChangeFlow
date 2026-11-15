using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChangeFlow.Infra.DataAccess.Users.Persistence;

internal sealed class UserCredentialRecord
{
    public Guid UserId { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
}

internal sealed class UserCredentialConfiguration
    : IEntityTypeConfiguration<UserCredentialRecord>
{
    public void Configure(EntityTypeBuilder<UserCredentialRecord> builder)
    {
        builder.ToTable("user_credentials");
        builder.HasKey(credential => credential.UserId);
        builder.Property(credential => credential.UserId)
            .HasColumnName("user_id")
            .HasColumnType("TEXT");
        builder.Property(credential => credential.PasswordHash)
            .HasColumnName("password_hash")
            .IsRequired();
        builder.HasOne<UserRecord>()
            .WithOne()
            .HasForeignKey<UserCredentialRecord>(credential => credential.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
