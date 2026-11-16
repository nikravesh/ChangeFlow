using ChangeFlow.Core.Domain.ChangeRequests;
using ChangeFlow.Infra.DataAccess.Users.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChangeFlow.Infra.DataAccess.ChangeRequests;

internal sealed class ChangeRequestConfiguration : IEntityTypeConfiguration<ChangeRequest>
{
    public void Configure(EntityTypeBuilder<ChangeRequest> builder)
    {
        builder.ToTable("change_requests", table =>
        {
            table.HasCheckConstraint("ck_change_requests_id", "id <> '00000000-0000-0000-0000-000000000000'");
            table.HasCheckConstraint("ck_change_requests_status",
                "status IN ('Draft', 'PendingReview', 'ChangesRequested', 'Approved')");
            table.HasCheckConstraint("ck_change_requests_version", "version > 0");
        });
        builder.HasKey(request => request.Id);
        builder.Property(request => request.Id).HasColumnName("id").HasColumnType("TEXT").ValueGeneratedNever();
        builder.Ignore(request => request.RequesterId);
        builder.Property<Guid>("RequesterKey").HasColumnName("requester_id").HasColumnType("TEXT");
        builder.HasOne<UserRecord>().WithMany().HasForeignKey("RequesterKey").OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex("RequesterKey").HasDatabaseName("ix_change_requests_requester_id");
        builder.Property(request => request.Title).HasColumnName("title").IsRequired();
        builder.Property(request => request.Description).HasColumnName("description").IsRequired();
        builder.Property(request => request.Reason).HasColumnName("reason").IsRequired();
        builder.Property(request => request.Status).HasColumnName("status").HasConversion<string>().IsRequired();
        builder.Property(request => request.Version).HasColumnName("version").IsConcurrencyToken();
    }
}
