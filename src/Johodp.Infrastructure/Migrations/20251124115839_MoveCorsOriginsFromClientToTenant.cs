using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Johodp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MoveCorsOriginsFromClientToTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Add AllowedCorsOrigins to tenants as nullable first
            migrationBuilder.AddColumn<List<string>>(
                name: "AllowedCorsOrigins",
                table: "tenants",
                type: "jsonb",
                nullable: true);

            // Step 2: Set default value (empty array) for existing tenants
            migrationBuilder.Sql(
                "UPDATE tenants SET \"AllowedCorsOrigins\" = '[]'::jsonb WHERE \"AllowedCorsOrigins\" IS NULL;");

            // Step 3: Make the column NOT NULL
            migrationBuilder.AlterColumn<List<string>>(
                name: "AllowedCorsOrigins",
                table: "tenants",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(List<string>),
                oldType: "jsonb",
                oldNullable: true);

            // Step 4: Drop AllowedCorsOrigins from clients
            migrationBuilder.DropColumn(
                name: "AllowedCorsOrigins",
                table: "clients");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowedCorsOrigins",
                table: "tenants");

            migrationBuilder.AddColumn<string>(
                name: "AllowedCorsOrigins",
                table: "clients",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
