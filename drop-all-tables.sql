-- Script to drop all tables in the dbo schema
-- WARNING: This will permanently delete all data!
-- Use this script to reset the database before applying migrations

-- Drop tables in the correct order to avoid foreign key constraint errors
-- (if there are any foreign keys)

-- Drop IdentityServer tables
DROP TABLE IF EXISTS dbo."PersistedGrants" CASCADE;
DROP TABLE IF EXISTS dbo."DeviceCodes" CASCADE;
DROP TABLE IF EXISTS dbo."Keys" CASCADE;
DROP TABLE IF EXISTS dbo."ServerSideSessions" CASCADE;

-- Drop UserTenants table (junction table)
DROP TABLE IF EXISTS dbo."UserTenants" CASCADE;

-- Drop users table
DROP TABLE IF EXISTS dbo.users CASCADE;

-- Drop tenants table
DROP TABLE IF EXISTS dbo.tenants CASCADE;

-- Drop custom_configurations table
DROP TABLE IF EXISTS dbo.custom_configurations CASCADE;

-- Drop clients table
DROP TABLE IF EXISTS dbo.clients CASCADE;

-- Drop the __EFMigrationsHistory table (Entity Framework migration tracking)
DROP TABLE IF EXISTS dbo."__EFMigrationsHistory" CASCADE;

-- Optional: Drop the schema if needed (uncomment if you want to drop the schema too)
-- DROP SCHEMA IF EXISTS dbo CASCADE;

-- Verify all tables are dropped
SELECT tablename 
FROM pg_tables 
WHERE schemaname = 'dbo';
