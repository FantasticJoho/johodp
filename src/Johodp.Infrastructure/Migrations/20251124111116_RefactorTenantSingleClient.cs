using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Johodp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorTenantSingleClient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add the new ClientId column first
            migrationBuilder.AddColumn<string>(
                name: "ClientId",
                table: "tenants",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            // Migrate data: take the first client from AssociatedClientIds array if it exists
            migrationBuilder.Sql(@"
                UPDATE tenants 
                SET ""ClientId"" = ""AssociatedClientIds""->0->>0
                WHERE jsonb_array_length(""AssociatedClientIds"") > 0
            ");

            // Drop the old column
            migrationBuilder.DropColumn(
                name: "AssociatedClientIds",
                table: "tenants");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "tenants");

            migrationBuilder.AddColumn<List<string>>(
                name: "AssociatedClientIds",
                table: "tenants",
                type: "jsonb",
                nullable: false);
        }
    }
}
