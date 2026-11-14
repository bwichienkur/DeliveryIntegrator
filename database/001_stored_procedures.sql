IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'ld')
    EXEC('CREATE SCHEMA ld');
GO

CREATE OR ALTER PROCEDURE ld.ld_GetProgramDetails
    @Program NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        @Program AS ProgramCode,
        CONCAT(@Program, '-DETAIL') AS ProgramDisplayName,
        'Active' AS Status;
END
GO