using ChangeFlow.Core.Domain.ChangeRequests;
using ChangeFlow.Infra.DataAccess.ChangeRequests;
using ChangeFlow.Infra.DataAccess.Common;
using ChangeFlow.Infra.DataAccess.Users.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ChangeFlow.Infra.DataAccess.Migrations;

[DbContext(typeof(ChangeFlowDbContext))]
[Migration("202611160002_AddReviewDecisions")]
public sealed class AddReviewDecisions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "review_decisions",
            columns: table => new
            {
                change_request_id = table.Column<Guid>(type: "TEXT", nullable: false),
                resulting_version = table.Column<long>(type: "INTEGER", nullable: false),
                reviewer_id = table.Column<Guid>(type: "TEXT", nullable: false),
                decided_at_utc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                action = table.Column<string>(type: "TEXT", nullable: false),
                feedback = table.Column<string>(type: "TEXT", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_review_decisions", decision =>
                    new { decision.change_request_id, decision.resulting_version });
                table.ForeignKey("FK_review_decisions_change_requests_change_request_id",
                    decision => decision.change_request_id, "change_requests", "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_review_decisions_users_reviewer_id",
                    decision => decision.reviewer_id, "users", "id", onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("ck_review_decisions_version", "resulting_version > 0");
                table.CheckConstraint("ck_review_decisions_feedback",
                    "(action = 'Approve' AND feedback IS NULL) OR " +
                    "(action = 'RequestChanges' AND feedback IS NOT NULL AND length(trim(feedback)) > 0)");
            });
        migrationBuilder.CreateIndex("ix_review_decisions_request_reviewer", "review_decisions",
            new[] { "change_request_id", "reviewer_id" });
        migrationBuilder.CreateIndex("ix_review_decisions_reviewer", "review_decisions", "reviewer_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable("review_decisions");

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "11.0.0");
        new UserConfiguration().Configure(modelBuilder.Entity<UserRecord>());
        new UserCredentialConfiguration().Configure(modelBuilder.Entity<UserCredentialRecord>());
        new UserRoleConfiguration().Configure(modelBuilder.Entity<UserRoleRecord>());
        new ChangeRequestConfiguration().Configure(modelBuilder.Entity<ChangeRequest>());
        new ReviewDecisionConfiguration().Configure(modelBuilder.Entity<ReviewDecision>());
    }
}
