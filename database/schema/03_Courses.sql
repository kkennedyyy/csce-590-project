-- Canonical schema aligned with backend EF Core model.
-- Creates dbo.CourseClasses (single table for class offerings/sections).

IF OBJECT_ID('dbo.CourseClasses', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CourseClasses
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        ClassName NVARCHAR(160) NOT NULL,
        CourseCode NVARCHAR(40) NOT NULL,
        Location NVARCHAR(160) NOT NULL,
        Credits INT NOT NULL,
        Capacity INT NOT NULL,
        DaysOfWeek NVARCHAR(40) NOT NULL,
        StartTime TIME NOT NULL,
        EndTime TIME NOT NULL,
        InstructorId INT NOT NULL,
        CONSTRAINT FK_CourseClasses_Instructors
            FOREIGN KEY (InstructorId) REFERENCES dbo.Instructors(Id)
    );
END;
