CREATE SCHEMA IF NOT EXISTS dbo;

CREATE TABLE IF NOT EXISTS dbo."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM dbo."__EFMigrationsHistory" WHERE "MigrationId" = '20251201095449_InitialCreate') THEN
        IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'dbo') THEN
            CREATE SCHEMA dbo;
        END IF;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM dbo."__EFMigrationsHistory" WHERE "MigrationId" = '20251201095449_InitialCreate') THEN
    CREATE TABLE dbo.clients (
        "Id" uuid NOT NULL,
        "ClientName" character varying(100) NOT NULL,
        "AllowedScopes" text NOT NULL,
        "RequireClientSecret" boolean NOT NULL DEFAULT TRUE,
        "RequireConsent" boolean NOT NULL DEFAULT TRUE,
        "RequireMfa" boolean NOT NULL DEFAULT FALSE,
        "IsActive" boolean NOT NULL DEFAULT TRUE,
        "CreatedAt" timestamp with time zone NOT NULL,
        associated_tenant_ids text NOT NULL,
        CONSTRAINT "PK_clients" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM dbo."__EFMigrationsHistory" WHERE "MigrationId" = '20251201095449_InitialCreate') THEN
    CREATE TABLE dbo.custom_configurations (
        id uuid NOT NULL,
        client_id uuid NOT NULL,
        name character varying(100) NOT NULL,
        description character varying(500),
        is_active boolean NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone,
        primary_color character varying(50),
        secondary_color character varying(50),
        logo_url character varying(500),
        background_image_url character varying(500),
        custom_css text,
        default_language character varying(10) NOT NULL,
        supported_languages text NOT NULL,
        CONSTRAINT "PK_custom_configurations" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM dbo."__EFMigrationsHistory" WHERE "MigrationId" = '20251201095449_InitialCreate') THEN
    CREATE TABLE dbo.tenants (
        "Id" uuid NOT NULL,
        "Name" character varying(100) NOT NULL,
        "DisplayName" character varying(200) NOT NULL,
        "IsActive" boolean NOT NULL DEFAULT TRUE,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        custom_configuration_id uuid NOT NULL,
        "NotificationUrl" character varying(500),
        "ApiKey" character varying(100),
        "NotifyOnAccountRequest" boolean NOT NULL DEFAULT FALSE,
        "ClientId" character varying(200),
        "AllowedCorsOrigins" jsonb NOT NULL,
        "AllowedReturnUrls" jsonb NOT NULL,
        "Urls" jsonb NOT NULL,
        CONSTRAINT "PK_tenants" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM dbo."__EFMigrationsHistory" WHERE "MigrationId" = '20251201095449_InitialCreate') THEN
    CREATE TABLE dbo.users (
        "Id" uuid NOT NULL,
        "Email" text NOT NULL,
        "FirstName" character varying(50) NOT NULL,
        "LastName" character varying(50) NOT NULL,
        "EmailConfirmed" boolean NOT NULL DEFAULT FALSE,
        "MFAEnabled" boolean NOT NULL DEFAULT FALSE,
        "Status" integer NOT NULL,
        "ActivatedAt" timestamp with time zone,
        "PasswordHash" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_users" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM dbo."__EFMigrationsHistory" WHERE "MigrationId" = '20251201095449_InitialCreate') THEN
    CREATE TABLE dbo."UserTenants" (
        "UserId" uuid NOT NULL,
        "TenantId" uuid NOT NULL,
        "Role" character varying(100) NOT NULL,
        "Scope" character varying(200) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_UserTenants" PRIMARY KEY ("UserId", "TenantId"),
        CONSTRAINT "FK_UserTenants_users_UserId" FOREIGN KEY ("UserId") REFERENCES dbo.users ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM dbo."__EFMigrationsHistory" WHERE "MigrationId" = '20251201095449_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_custom_configurations_name" ON dbo.custom_configurations (name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM dbo."__EFMigrationsHistory" WHERE "MigrationId" = '20251201095449_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_tenants_Name" ON dbo.tenants ("Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM dbo."__EFMigrationsHistory" WHERE "MigrationId" = '20251201095449_InitialCreate') THEN
    CREATE INDEX "IX_UserTenants_TenantId" ON dbo."UserTenants" ("TenantId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM dbo."__EFMigrationsHistory" WHERE "MigrationId" = '20251201095449_InitialCreate') THEN
    CREATE INDEX "IX_UserTenants_UserId" ON dbo."UserTenants" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM dbo."__EFMigrationsHistory" WHERE "MigrationId" = '20251201095449_InitialCreate') THEN
    INSERT INTO dbo."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20251201095449_InitialCreate', '8.0.11');
    END IF;
END $EF$;
COMMIT;

