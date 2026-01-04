-- Source SQL Queries Template
-- This file will be copied to a new version file when running the process

-- Create table if not exists
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'VersionHistory')
BEGIN
    CREATE TABLE VersionHistory (
        ID UNIQUEIDENTIFIER PRIMARY KEY,
        VersionNum INT NOT NULL,
        VersionText NVARCHAR(50),
        CreatedDate DATETIME DEFAULT GETDATE()
    );
END

-- Insert version data
INSERT INTO VersionHistory (ID, VersionNum, VersionText, CreatedDate)
VALUES ('a1b2c3d4-e5f6-7890-abcd-ef1234567890', 4, 'version3to4', GETDATE());

-- Update existing records
UPDATE VersionHistory 
SET VersionNum = 4, VersionText = 'version3to4'
WHERE ID = 'a1b2c3d4-e5f6-7890-abcd-ef1234567890';
