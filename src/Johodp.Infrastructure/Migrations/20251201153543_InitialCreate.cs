using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Johodp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "clients",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AllowedScopes = table.Column<string>(type: "text", nullable: false),
                    RequireClientSecret = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    RequireConsent = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    RequireMfa = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    associated_tenant_ids = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_clients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "custom_configurations",
                schema: "dbo",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    primary_color = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    secondary_color = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    logo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    background_image_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    custom_css = table.Column<string>(type: "text", nullable: true),
                    default_language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    supported_languages = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_configurations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    custom_configuration_id = table.Column<Guid>(type: "uuid", nullable: false),
                    NotificationUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ApiKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NotifyOnAccountRequest = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ClientId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AllowedCorsOrigins = table.Column<List<string>>(type: "jsonb", nullable: false),
                    AllowedReturnUrls = table.Column<List<string>>(type: "jsonb", nullable: false),
                    Urls = table.Column<List<string>>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LastName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    MFAEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ActivatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Scope = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_users_tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "dbo",
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_custom_configurations_name",
                schema: "dbo",
                table: "custom_configurations",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenants_Name",
                schema: "dbo",
                table: "tenants",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_Email_TenantId",
                schema: "dbo",
                table: "users",
                columns: new[] { "Email", "TenantId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_TenantId",
                schema: "dbo",
                table: "users",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "clients",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "custom_configurations",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "users",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "tenants",
                schema: "dbo");
        }
    }
}
