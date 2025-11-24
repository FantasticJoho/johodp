CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

CREATE TABLE clients (
    "Id" uuid NOT NULL,
    "ClientName" character varying(100) NOT NULL,
    "AllowedScopes" text NOT NULL,
    "AllowedRedirectUris" text NOT NULL,
    "AllowedCorsOrigins" text NOT NULL,
    "RequireClientSecret" boolean NOT NULL DEFAULT TRUE,
    "RequireConsent" boolean NOT NULL DEFAULT TRUE,
    "IsActive" boolean NOT NULL DEFAULT TRUE,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_clients" PRIMARY KEY ("Id")
);

CREATE TABLE permissions (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    "Description" character varying(500) NOT NULL,
    "IsActive" boolean NOT NULL DEFAULT TRUE,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_permissions" PRIMARY KEY ("Id")
);

CREATE TABLE roles (
    "Id" uuid NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Description" character varying(500) NOT NULL,
    "RequiresMFA" boolean NOT NULL DEFAULT FALSE,
    "IsActive" boolean NOT NULL DEFAULT TRUE,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_roles" PRIMARY KEY ("Id")
);

CREATE TABLE scopes (
    "Id" uuid NOT NULL,
    "Name" character varying(100) NOT NULL,
    "Code" character varying(50) NOT NULL,
    "Description" character varying(500) NOT NULL,
    "IsActive" boolean NOT NULL DEFAULT TRUE,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_scopes" PRIMARY KEY ("Id")
);

CREATE TABLE users (
    "Id" uuid NOT NULL,
    "Email" text NOT NULL,
    "FirstName" character varying(50) NOT NULL,
    "LastName" character varying(50) NOT NULL,
    "EmailConfirmed" boolean NOT NULL DEFAULT FALSE,
    "IsActive" boolean NOT NULL DEFAULT TRUE,
    "MFAEnabled" boolean NOT NULL DEFAULT FALSE,
    "PasswordHash" text,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "ScopeId" uuid,
    CONSTRAINT "PK_users" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_users_scopes_ScopeId" FOREIGN KEY ("ScopeId") REFERENCES scopes ("Id")
);

CREATE TABLE "UserPermissions" (
    "PermissionsId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    CONSTRAINT "PK_UserPermissions" PRIMARY KEY ("PermissionsId", "UserId"),
    CONSTRAINT "FK_UserPermissions_permissions_PermissionsId" FOREIGN KEY ("PermissionsId") REFERENCES permissions ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_UserPermissions_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE CASCADE
);

CREATE TABLE "UserRoles" (
    "RolesId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    CONSTRAINT "PK_UserRoles" PRIMARY KEY ("RolesId", "UserId"),
    CONSTRAINT "FK_UserRoles_roles_RolesId" FOREIGN KEY ("RolesId") REFERENCES roles ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_UserRoles_users_UserId" FOREIGN KEY ("UserId") REFERENCES users ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX "IX_scopes_Code" ON scopes ("Code");

CREATE INDEX "IX_UserPermissions_UserId" ON "UserPermissions" ("UserId");

CREATE INDEX "IX_UserRoles_UserId" ON "UserRoles" ("UserId");

CREATE INDEX "IX_users_ScopeId" ON users ("ScopeId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20251118094152_Init', '8.0.11');

COMMIT;

START TRANSACTION;

ALTER TABLE users ADD "TenantId" character varying(100);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20251118132702_AddTenantIdToUser', '8.0.11');

COMMIT;

START TRANSACTION;

CREATE TABLE tenants (
    "Id" uuid NOT NULL,
    "Name" character varying(100) NOT NULL,
    "DisplayName" character varying(200) NOT NULL,
    "IsActive" boolean NOT NULL DEFAULT TRUE,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    "PrimaryColor" character varying(50),
    "SecondaryColor" character varying(50),
    "LogoUrl" character varying(500),
    "BackgroundImageUrl" character varying(500),
    "CustomCss" text,
    "DefaultLanguage" character varying(10) NOT NULL DEFAULT 'fr-FR',
    "Timezone" character varying(50) NOT NULL DEFAULT 'Europe/Paris',
    "Currency" character varying(10) NOT NULL DEFAULT 'EUR',
    "AllowedReturnUrls" jsonb NOT NULL,
    "AssociatedClientIds" jsonb NOT NULL,
    "SupportedLanguages" jsonb NOT NULL,
    CONSTRAINT "PK_tenants" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX "IX_tenants_Name" ON tenants ("Name");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20251120083019_AddTenantEntity', '8.0.11');

COMMIT;

START TRANSACTION;

ALTER TABLE users ADD "TenantIds" jsonb;


                UPDATE users 
                SET "TenantIds" = 
                    CASE 
                        WHEN "TenantId" IS NULL OR "TenantId" = '' THEN '[]'::jsonb
                        ELSE jsonb_build_array("TenantId")
                    END
                WHERE "TenantIds" IS NULL;
            


                ALTER TABLE users 
                ALTER COLUMN "TenantIds" SET NOT NULL;
            

ALTER TABLE users DROP COLUMN "TenantId";

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20251120091006_UpdateUserMultiTenant', '8.0.11');

COMMIT;

START TRANSACTION;

ALTER TABLE users ADD "ActivatedAt" timestamp with time zone;

ALTER TABLE users ADD "Status" integer NOT NULL DEFAULT 1;

ALTER TABLE tenants ADD "ApiKey" character varying(100);

ALTER TABLE tenants ADD "NotificationUrl" character varying(500);

ALTER TABLE tenants ADD "NotifyOnAccountRequest" boolean NOT NULL DEFAULT FALSE;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20251120113742_AddOnboardingFlowSupport', '8.0.11');

COMMIT;

START TRANSACTION;

ALTER TABLE users DROP COLUMN "IsActive";

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20251120154023_RemoveIsActiveFromUser', '8.0.11');

COMMIT;

START TRANSACTION;

ALTER TABLE users ALTER COLUMN "Status" DROP DEFAULT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20251120160305_RefactorUserStatusToEnumerationClass', '8.0.11');

COMMIT;

START TRANSACTION;

ALTER TABLE clients RENAME COLUMN "AllowedRedirectUris" TO associated_tenant_ids;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20251124093043_RefactorClientWithTenantAssociations', '8.0.11');

COMMIT;

START TRANSACTION;

ALTER TABLE tenants ADD "ClientId" character varying(200);


                UPDATE tenants 
                SET "ClientId" = "AssociatedClientIds"->0->>0
                WHERE jsonb_array_length("AssociatedClientIds") > 0
            

ALTER TABLE tenants DROP COLUMN "AssociatedClientIds";

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20251124111116_RefactorTenantSingleClient', '8.0.11');

COMMIT;

START TRANSACTION;

ALTER TABLE tenants ADD "AllowedCorsOrigins" jsonb;

UPDATE tenants SET "AllowedCorsOrigins" = '[]'::jsonb WHERE "AllowedCorsOrigins" IS NULL;

ALTER TABLE tenants ALTER COLUMN "AllowedCorsOrigins" SET NOT NULL;

ALTER TABLE clients DROP COLUMN "AllowedCorsOrigins";

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20251124115839_MoveCorsOriginsFromClientToTenant', '8.0.11');

COMMIT;

START TRANSACTION;

ALTER TABLE clients ADD "RequireMfa" boolean NOT NULL DEFAULT FALSE;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20251124131813_AddRequireMfaToClient', '8.0.11');

COMMIT;

