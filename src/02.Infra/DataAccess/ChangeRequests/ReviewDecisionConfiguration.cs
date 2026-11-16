using ChangeFlow.Core.Domain.ChangeRequests;
using ChangeFlow.Infra.DataAccess.Users.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ChangeFlow.Infra.DataAccess.ChangeRequests;

internal sealed class ReviewDecisionConfiguration : IEntityTypeConfiguration<ReviewDecision>
{
    internal const string FeedbackConstraint =
        "(action = 'Approve' AND feedback IS NULL) OR " +
        "(action = 'RequestChanges' AND feedback IS NOT NULL AND length(trim(feedback)) > 0)";

    public void Configure(EntityTypeBuilder<ReviewDecision> builder)
    {
        builder.ToTable("review_decisions", table =>
        {
            table.HasCheckConstraint("ck_review_decisions_version", "resulting_version > 0");
            table.HasCheckConstraint("ck_review_decisions_feedback", FeedbackConstraint);
        });
        builder.HasKey(decision => new { decision.ChangeRequestId, decision.ResultingVersion });
        builder.Property(decision => decision.ChangeRequestId).HasColumnName("change_request_id");
        builder.Property(decision => decision.ResultingVersion).HasColumnName("resulting_version");
        builder.Ignore(decision => decision.ReviewerId);
        builder.Property<Guid>("ReviewerKey").HasColumnName("reviewer_id");
        builder.Property(decision => decision.DecidedAtUtc).HasColumnName("decided_at_utc");
        builder.Property(decision => decision.Action).HasColumnName("action").HasConversion<string>();
        builder.Property(decision => decision.Feedback).HasColumnName("feedback");
        builder.HasOne<ChangeRequest>().WithMany().HasForeignKey(decision => decision.ChangeRequestId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserRecord>().WithMany().HasForeignKey("ReviewerKey").OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(nameof(ReviewDecision.ChangeRequestId), "ReviewerKey")
            .HasDatabaseName("ix_review_decisions_request_reviewer");
        builder.HasIndex("ReviewerKey").HasDatabaseName("ix_review_decisions_reviewer");
    }
}
