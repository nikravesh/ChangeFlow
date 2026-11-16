using ChangeFlow.Core.Domain.ChangeRequests;
using ChangeFlow.Infra.DataAccess.ChangeRequests;
using ChangeFlow.Infra.DataAccess.Common;
using ChangeFlow.Infra.DataAccess.Users.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ChangeFlow.Infra.DataAccess.Migrations;

[DbContext(typeof(ChangeFlowDbContext))]
[Migration("202611160001_AddChangeRequests")]
public sealed class AddChangeRequests : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "change_requests",
            columns: table => new
            {
                id = table.Column<Guid>(type: "TEXT", nullable: false),
                requester_id = table.Column<Guid>(type: "TEXT", nullable: false),
                title = table.Column<string>(type: "TEXT", nullable: false),
                description = table.Column<string>(type: "TEXT", nullable: false),
                reason = table.Column<string>(type: "TEXT", nullable: false),
                status = table.Column<string>(type: "TEXT", nullable: false),
                version = table.Column<long>(type: "INTEGER", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_change_requests", request => request.id);
                table.ForeignKey("FK_change_requests_users_requester_id", request => request.requester_id,
                    "users", "id", onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("ck_change_requests_id", "id <> '00000000-0000-0000-0000-000000000000'");
                table.CheckConstraint("ck_change_requests_status",
                    "status IN ('Draft', 'PendingReview', 'ChangesRequested', 'Approved')");
                table.CheckConstraint("ck_change_requests_version", "version > 0");
            });
        migrationBuilder.CreateIndex("ix_change_requests_requester_id", "change_requests", "requester_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable("change_requests");

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "11.0.0");
        new UserConfiguration().Configure(modelBuilder.Entity<UserRecord>());
        new UserCredentialConfiguration().Configure(modelBuilder.Entity<UserCredentialRecord>());
        new UserRoleConfiguration().Configure(modelBuilder.Entity<UserRoleRecord>());
        new ChangeRequestConfiguration().Configure(modelBuilder.Entity<ChangeRequest>());
    }
}
