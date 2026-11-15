using ChangeFlow.Infra.DataAccess.Common;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ChangeFlow.Infra.DataAccess.Migrations;

[DbContext(typeof(ChangeFlowDbContext))]
[Migration("202611140001_InitialUserProvisioning")]
public sealed class InitialUserProvisioning : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "users",
            columns: table => new
            {
                id = table.Column<Guid>(type: "TEXT", nullable: false),
                display_name = table.Column<string>(type: "TEXT", nullable: false),
                login_identifier = table.Column<string>(type: "TEXT", nullable: false),
                normalized_login_identifier = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_users", user => user.id);
            });

        migrationBuilder.CreateTable(
            name: "user_credentials",
            columns: table => new
            {
                user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                password_hash = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_credentials", credential => credential.user_id);
                table.ForeignKey(
                    name: "FK_user_credentials_users_user_id",
                    column: credential => credential.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "user_roles",
            columns: table => new
            {
                user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                role = table.Column<string>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_roles", role => new { role.user_id, role.role });
                table.CheckConstraint(
                    "ck_user_roles_role",
                    "\"role\" IN ('Requester', 'Reviewer')");
                table.ForeignKey(
                    name: "FK_user_roles_users_user_id",
                    column: role => role.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ux_users_normalized_login_identifier",
            table: "users",
            column: "normalized_login_identifier",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "user_credentials");
        migrationBuilder.DropTable(name: "user_roles");
        migrationBuilder.DropTable(name: "users");
    }
}
