-- Script de migration pour renommer toutes les tables IdentityServer en snake_case
-- À exécuter sur une base de données PostgreSQL existante
--
-- RAISONS DE LA STANDARDISATION EN snake_case:
-- 1. Cohérence: Les tables de l'application (clients, users, tenants) sont déjà en snake_case
-- 2. Pas de quotes: PostgreSQL ne nécessite pas de double-quotes pour les noms en minuscules
--    Exemple: SELECT * FROM persisted_grants au lieu de SELECT * FROM "PersistedGrants"
-- 3. Case-insensitive: PostgreSQL convertit automatiquement en minuscules les identifiants non quotés
--    Évite les erreurs de casse (MyTable vs mytable vs MYTABLE)
-- 4. Standard SQL: Convention largement adoptée dans PostgreSQL et les bases de données relationnelles
-- 5. Lisibilité pgAdmin: Requêtes plus propres et uniformes sans mélange de conventions
-- 6. Compatibilité: Facilite le portage entre différents SGBD (PostgreSQL, MySQL, SQLite)

BEGIN;

-- Renommer les tables IdentityServer
ALTER TABLE IF EXISTS dbo."DeviceCodes" RENAME TO "device_codes";
ALTER TABLE IF EXISTS dbo."Keys" RENAME TO "keys";
ALTER TABLE IF EXISTS dbo."PersistedGrants" RENAME TO "persisted_grants";
ALTER TABLE IF EXISTS dbo."PushedAuthorizationRequests" RENAME TO "pushed_authorization_requests";
ALTER TABLE IF EXISTS dbo."ServerSideSessions" RENAME TO "server_side_sessions";

-- Vérification des tables renommées
SELECT 
    schemaname,
    tablename
FROM pg_tables
WHERE schemaname = 'dbo'
ORDER BY tablename;

COMMIT;

-- Note: Après cette migration, vous devez :
-- 1. Vérifier que toutes les tables sont bien renommées
-- 2. Mettre à jour la table __EFMigrationsHistory pour marquer la migration comme appliquée:
--    INSERT INTO dbo."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
--    VALUES ('20251203021924_RenameIdentityServerTablesToSnakeCase', '9.0.0');
