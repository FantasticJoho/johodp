using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Johodp.Infrastructure.Migrations
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
                name: "users",
                newName: "users",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "UserRoles",
                newName: "UserRoles",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "UserPermissions",
                newName: "UserPermissions",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "tenants",
                newName: "tenants",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "scopes",
                newName: "scopes",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "roles",
                newName: "roles",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "permissions",
                newName: "permissions",
                newSchema: "dbo");

            migrationBuilder.RenameTable(
                name: "clients",
                newName: "clients",
                newSchema: "dbo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "users",
                schema: "dbo",
                newName: "users");

            migrationBuilder.RenameTable(
                name: "UserRoles",
                schema: "dbo",
                newName: "UserRoles");

            migrationBuilder.RenameTable(
                name: "UserPermissions",
                schema: "dbo",
                newName: "UserPermissions");

            migrationBuilder.RenameTable(
                name: "tenants",
                schema: "dbo",
                newName: "tenants");

            migrationBuilder.RenameTable(
                name: "scopes",
                schema: "dbo",
                newName: "scopes");

            migrationBuilder.RenameTable(
                name: "roles",
                schema: "dbo",
                newName: "roles");

            migrationBuilder.RenameTable(
                name: "permissions",
                schema: "dbo",
                newName: "permissions");

            migrationBuilder.RenameTable(
                name: "clients",
                schema: "dbo",
                newName: "clients");
        }
    }
}
