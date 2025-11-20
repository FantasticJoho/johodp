using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Johodp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateUserMultiTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Ajouter la nouvelle colonne TenantIds comme nullable d'abord
            migrationBuilder.AddColumn<List<string>>(
                name: "TenantIds",
                table: "users",
                type: "jsonb",
                nullable: true);

            // 2. Migrer les données: convertir TenantId (string) en TenantIds (array)
            migrationBuilder.Sql(@"
                UPDATE users 
                SET ""TenantIds"" = 
                    CASE 
                        WHEN ""TenantId"" IS NULL OR ""TenantId"" = '' THEN '[]'::jsonb
                        ELSE jsonb_build_array(""TenantId"")
                    END
                WHERE ""TenantIds"" IS NULL;
            ");

            // 3. Rendre TenantIds NOT NULL maintenant que toutes les lignes ont des valeurs
            migrationBuilder.Sql(@"
                ALTER TABLE users 
                ALTER COLUMN ""TenantIds"" SET NOT NULL;
            ");

            // 4. Supprimer l'ancienne colonne TenantId
            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 1. Ajouter l'ancienne colonne TenantId
            migrationBuilder.AddColumn<string>(
                name: "TenantId",
                table: "users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // 2. Migrer les données: prendre le premier tenant de TenantIds (ou NULL si vide)
            migrationBuilder.Sql(@"
                UPDATE users 
                SET ""TenantId"" = 
                    CASE 
                        WHEN jsonb_array_length(""TenantIds"") > 0 
                        THEN ""TenantIds""->0->>0
                        ELSE NULL
                    END;
            ");

            // 3. Supprimer la nouvelle colonne TenantIds
            migrationBuilder.DropColumn(
                name: "TenantIds",
                table: "users");
        }
    }
}
