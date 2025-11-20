using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Johodp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOnboardingFlowSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ActivatedAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "ApiKey",
                table: "tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NotificationUrl",
                table: "tenants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyOnAccountRequest",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActivatedAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "users");

            migrationBuilder.DropColumn(
                name: "ApiKey",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "NotificationUrl",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "NotifyOnAccountRequest",
                table: "tenants");
        }
    }
}
