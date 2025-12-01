-- Script to move __EFMigrationsHistory tables from public to dbo schema
-- This ensures all EF Core migration tracking is in the dbo schema

-- Move __EFMigrationsHistory from public to dbo (if exists)
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM pg_tables 
        WHERE schemaname = 'public' 
        AND tablename = '__EFMigrationsHistory'
    ) THEN
        ALTER TABLE public."__EFMigrationsHistory" SET SCHEMA dbo;
        RAISE NOTICE 'Moved __EFMigrationsHistory from public to dbo';
    ELSE
        RAISE NOTICE '__EFMigrationsHistory already in dbo or does not exist in public';
    END IF;
END $$;

-- Verify final state
SELECT schemaname, tablename 
FROM pg_tables 
WHERE tablename = '__EFMigrationsHistory'
ORDER BY schemaname;
