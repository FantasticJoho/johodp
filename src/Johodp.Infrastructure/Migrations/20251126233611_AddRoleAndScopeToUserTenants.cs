using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Johodp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleAndScopeToUserTenants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "dbo",
                table: "UserTenants",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: DateTime.UtcNow);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                schema: "dbo",
                table: "UserTenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "user");

            migrationBuilder.AddColumn<string>(
                name: "Scope",
                schema: "dbo",
                table: "UserTenants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "default");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                schema: "dbo",
                table: "UserTenants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserTenants_UserId",
                schema: "dbo",
                table: "UserTenants",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserTenants_UserId",
                schema: "dbo",
                table: "UserTenants");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "dbo",
                table: "UserTenants");

            migrationBuilder.DropColumn(
                name: "Role",
                schema: "dbo",
                table: "UserTenants");

            migrationBuilder.DropColumn(
                name: "Scope",
                schema: "dbo",
                table: "UserTenants");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "dbo",
                table: "UserTenants");

            migrationBuilder.AddForeignKey(
                name: "FK_UserTenants_tenants_TenantId",
                schema: "dbo",
                table: "UserTenants",
                column: "TenantId",
                principalSchema: "dbo",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
