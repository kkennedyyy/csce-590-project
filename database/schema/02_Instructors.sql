-- Canonical schema aligned with backend EF Core model.
-- Creates dbo.Instructors expected by backend/Data/ClassFinderDbContext.cs

IF OBJECT_ID('dbo.Instructors', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Instructors
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        FirstName NVARCHAR(80) NOT NULL,
        LastName NVARCHAR(80) NOT NULL,
        Email NVARCHAR(200) NOT NULL UNIQUE
    );
END;
