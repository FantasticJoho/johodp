using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Johodp.Infrastructure.Migrations.PersistedGrantDb
{
    /// <inheritdoc />
    public partial class MoveToDbOSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.RenameTable(
                name: "ServerSideSessions",
                newName: "ServerSideSessions",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "PushedAuthorizationRequests",
                newName: "PushedAuthorizationRequests",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "PersistedGrants",
                newName: "PersistedGrants",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "Keys",
                newName: "Keys",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "DeviceCodes",
                newName: "DeviceCodes",
                newSchema: "dbo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "ServerSideSessions",
                schema: "dbo",
                newName: "ServerSideSessions");

            migrationBuilder.RenameTable(
                name: "PushedAuthorizationRequests",
                schema: "dbo",
                newName: "PushedAuthorizationRequests");

            migrationBuilder.RenameTable(
                name: "PersistedGrants",
                schema: "dbo",
                newName: "PersistedGrants");

            migrationBuilder.RenameTable(
                name: "Keys",
                schema: "dbo",
                newName: "Keys");

            migrationBuilder.RenameTable(
                name: "DeviceCodes",
                schema: "dbo",
                newName: "DeviceCodes");
        }
    }
}
