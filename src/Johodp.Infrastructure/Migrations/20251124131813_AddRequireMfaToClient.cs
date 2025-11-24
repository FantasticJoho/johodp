using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Johodp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRequireMfaToClient : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequireMfa",
                table: "clients",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequireMfa",
                table: "clients");
        }
    }
}
