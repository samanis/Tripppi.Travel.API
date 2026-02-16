-- Add popularity_score column to airports table
-- Run this first if the column doesn't exist

-- Check if column exists, if not add it
DO $$ 
BEGIN
    IF NOT EXISTS (
        SELECT 1 
        FROM information_schema.columns 
        WHERE table_name = 'airports' 
        AND column_name = 'popularity_score'
    ) THEN
        ALTER TABLE airports 
        ADD COLUMN popularity_score INTEGER DEFAULT 10;
        
        RAISE NOTICE 'Column popularity_score added successfully';
    ELSE
        RAISE NOTICE 'Column popularity_score already exists';
    END IF;
END $$;
