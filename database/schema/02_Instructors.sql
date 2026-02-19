-- ===============================================
-- Table: Instructors
-- Purpose: Store instructors information
-- ===============================================

CREATE TABLE instructors (
    InstructorId INT PRIMARY KEY IDENTITY(1,1), -- Auto-incremental ID
    Firstname NVARCHAR(50) NOT NULL, -- Instructor's First Name
    Lastname NVARCHAR(50) NOT NULL, -- Instructor's Last Name
    Email NVARCHAR(100) NOT NULL UNIQUE, -- Instructor's Email
    CreatedDate DATETIME2 DEFAULT GETDATE(), -- Creation Date
    IsActive BIT DEFAULT 1 -- Active Status
);