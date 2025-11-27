using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Johodp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorUserTenantToTenantIdAndAddTenantUrls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TenantIds",
                schema: "dbo",
                table: "users");

            // Add Urls column as nullable first to avoid constraint violation on existing data
            migrationBuilder.AddColumn<List<string>>(
                name: "Urls",
                schema: "dbo",
                table: "tenants",
                type: "jsonb",
                nullable: true);

            // Update existing tenants with empty array
            migrationBuilder.Sql("UPDATE dbo.tenants SET \"Urls\" = '[]'::jsonb WHERE \"Urls\" IS NULL;");

            // Make column NOT NULL after updating existing data
            migrationBuilder.AlterColumn<List<string>>(
                name: "Urls",
                schema: "dbo",
                table: "tenants",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(List<string>),
                oldType: "jsonb",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "UserTenants",
                schema: "dbo",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTenants", x => new { x.UserId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_UserTenants_tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "dbo",
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserTenants_users_UserId",
                        column: x => x.UserId,
                        principalSchema: "dbo",
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserTenants_TenantId",
                schema: "dbo",
                table: "UserTenants",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserTenants",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "Urls",
                schema: "dbo",
                table: "tenants");

            migrationBuilder.AddColumn<List<string>>(
                name: "TenantIds",
                schema: "dbo",
                table: "users",
                type: "jsonb",
                nullable: false);
        }
    }
}
