-- Canonical schema aligned with backend EF Core model.
-- Creates dbo.Students expected by backend/Data/ClassFinderDbContext.cs

IF OBJECT_ID('dbo.Students', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Students
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        FirstName NVARCHAR(80) NOT NULL,
        LastName NVARCHAR(80) NOT NULL,
        Email NVARCHAR(200) NOT NULL UNIQUE
    );
END;
