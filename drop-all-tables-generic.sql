-- Generic script to drop ALL tables in the 'public' schema only
-- WARNING: This will permanently delete all data in the public schema!
-- This script generates and executes DROP TABLE commands for every table in the public schema

DO $$ 
DECLARE
    r RECORD;
BEGIN
    -- Drop all tables in public schema
    FOR r IN (
        SELECT tablename, schemaname
        FROM pg_tables
        WHERE schemaname = 'public'
        ORDER BY tablename
    ) LOOP
        EXECUTE 'DROP TABLE IF EXISTS ' || quote_ident(r.schemaname) || '.' || quote_ident(r.tablename) || ' CASCADE';
        RAISE NOTICE 'Dropped table: %.%', r.schemaname, r.tablename;
    END LOOP;
    
    -- Drop all sequences in public schema
    FOR r IN (
        SELECT sequencename, schemaname
        FROM pg_sequences
        WHERE schemaname = 'public'
    ) LOOP
        EXECUTE 'DROP SEQUENCE IF EXISTS ' || quote_ident(r.schemaname) || '.' || quote_ident(r.sequencename) || ' CASCADE';
        RAISE NOTICE 'Dropped sequence: %.%', r.schemaname, r.sequencename;
    END LOOP;
    
    -- Drop all views in public schema
    FOR r IN (
        SELECT viewname, schemaname
        FROM pg_views
        WHERE schemaname = 'public'
    ) LOOP
        EXECUTE 'DROP VIEW IF EXISTS ' || quote_ident(r.schemaname) || '.' || quote_ident(r.viewname) || ' CASCADE';
        RAISE NOTICE 'Dropped view: %.%', r.schemaname, r.viewname;
    END LOOP;
    
    -- Drop all functions in public schema
    FOR r IN (
        SELECT proname, nspname
        FROM pg_proc
        INNER JOIN pg_namespace ON pg_proc.pronamespace = pg_namespace.oid
        WHERE nspname = 'public'
    ) LOOP
        EXECUTE 'DROP FUNCTION IF EXISTS ' || quote_ident(r.nspname) || '.' || quote_ident(r.proname) || ' CASCADE';
        RAISE NOTICE 'Dropped function: %.%', r.nspname, r.proname;
    END LOOP;
END $$;

-- Verify everything is dropped in public schema
SELECT 'Tables remaining:' AS check_type, count(*) 
FROM pg_tables 
WHERE schemaname = 'public'
UNION ALL
SELECT 'Sequences remaining:', count(*) 
FROM pg_sequences 
WHERE schemaname = 'public'
UNION ALL
SELECT 'Views remaining:', count(*) 
FROM pg_views 
WHERE schemaname = 'public';
