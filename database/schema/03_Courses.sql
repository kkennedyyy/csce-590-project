-- ===============================================
-- Table: Courses
-- Purpose: Store Course information
-- ===============================================

CREATE TABLE Courses
(
    CourseId INT PRIMARY KEY IDENTITY(1,1),
    -- Auto-incremental ID
    CourseCode NVARCHAR(20) UNIQUE NOT NULL,
    -- Examples: MATH111, ENGL101, CSCE590
    CourseName NVARCHAR(100) NOT NULL,
    -- Course Name
    Credits INT NOT NULL,
    -- Credit Hours
    CreatedDate DATETIME2 DEFAULT GETDATE()
    -- Date Created
);