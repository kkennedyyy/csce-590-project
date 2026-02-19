-- ===============================================
-- Table: Students
-- Purpose: Store student information
-- ===============================================

CREATE TABLE Students (
    -- StudentId must be unique and non-null to each row, auto-incremental starting at 1 and incrementing by 1 for each new id
    StudentId INT PRIMARY KEY IDENTITY(1,1),

    -- NVARCHAR used for non-english letters with a maximum length of 50 characters. Cannot be null
    Firstname NVARCHAR(50) NOT NULL,
    Middlename NVARCHAR(50),
    Lastname NVARCHAR(50) NOT NULL,

    -- Email must be unique, but can be null
    Email NVARCHAR(100) UNIQUE NOT NULL,

    -- Get account creation date and set the account to active as default
    CreatedDate DATETIME2 DEFAULT GETDATE(),
    IsActive BIT DEFAULT 1
);