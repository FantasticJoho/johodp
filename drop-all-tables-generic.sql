-- Generic script to drop ALL tables in a specified schema
-- WARNING: This will permanently delete all data in the specified schema!
-- This script generates and executes DROP TABLE commands for every table in the schema

-- Temporary table to hold result counts so we can return them as a result set
CREATE TEMP TABLE IF NOT EXISTS temp_schema_drop_counts (
    schema_name text,
    tables_remaining int,
    sequences_remaining int,
    views_remaining int
);

DO $$ 
DECLARE
    r RECORD;
    schema_name TEXT := 'public';  -- CHANGE THIS to run against a different schema
    tables_remaining INT;
    sequences_remaining INT;
    views_remaining INT;
BEGIN
    -- Drop all tables in specified schema
    FOR r IN (
        SELECT tablename, schemaname
        FROM pg_tables
        WHERE schemaname = schema_name
        ORDER BY tablename
    ) LOOP
        EXECUTE 'DROP TABLE IF EXISTS ' || quote_ident(r.schemaname) || '.' || quote_ident(r.tablename) || ' CASCADE';
        RAISE NOTICE 'Dropped table: %.%', r.schemaname, r.tablename;
    END LOOP;
    
    -- Drop all sequences in specified schema
    FOR r IN (
        SELECT sequencename, schemaname
        FROM pg_sequences
        WHERE schemaname = schema_name
    ) LOOP
        EXECUTE 'DROP SEQUENCE IF EXISTS ' || quote_ident(r.schemaname) || '.' || quote_ident(r.sequencename) || ' CASCADE';
        RAISE NOTICE 'Dropped sequence: %.%', r.schemaname, r.sequencename;
    END LOOP;
    
    -- Drop all views in specified schema
    FOR r IN (
        SELECT viewname, schemaname
        FROM pg_views
        WHERE schemaname = schema_name
    ) LOOP
        EXECUTE 'DROP VIEW IF EXISTS ' || quote_ident(r.schemaname) || '.' || quote_ident(r.viewname) || ' CASCADE';
        RAISE NOTICE 'Dropped view: %.%', r.schemaname, r.viewname;
    END LOOP;
    
    -- Drop all functions in specified schema
    FOR r IN (
        SELECT proname, nspname
        FROM pg_proc
        INNER JOIN pg_namespace ON pg_proc.pronamespace = pg_namespace.oid
        WHERE nspname = schema_name
    ) LOOP
        EXECUTE 'DROP FUNCTION IF EXISTS ' || quote_ident(r.nspname) || '.' || quote_ident(r.proname) || ' CASCADE';
        RAISE NOTICE 'Dropped function: %.%', r.nspname, r.proname;
    END LOOP;

    -- Verify everything is dropped in specified schema (counts)
    SELECT count(*) INTO tables_remaining FROM pg_tables WHERE schemaname = schema_name;
    SELECT count(*) INTO sequences_remaining FROM pg_sequences WHERE schemaname = schema_name;
    SELECT count(*) INTO views_remaining FROM pg_views WHERE schemaname = schema_name;

    INSERT INTO temp_schema_drop_counts (schema_name, tables_remaining, sequences_remaining, views_remaining)
    VALUES (schema_name, tables_remaining, sequences_remaining, views_remaining);
END $$;

-- Return counts as a result set so pgAdmin (or any client) can display them
SELECT schema_name AS schema, tables_remaining AS tables, sequences_remaining AS sequences, views_remaining AS views
FROM temp_schema_drop_counts;

-- Clean up temporary table
DROP TABLE IF EXISTS temp_schema_drop_counts;
