-- Canonical schema aligned with backend EF Core model.
-- Creates dbo.Enrollments with status + waitlist position.

IF OBJECT_ID('dbo.Enrollments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Enrollments
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        StudentId INT NOT NULL,
        CourseClassId INT NOT NULL,
        Status INT NOT NULL,
        WaitlistPosition INT NULL,
        CONSTRAINT FK_Enrollments_Students
            FOREIGN KEY (StudentId) REFERENCES dbo.Students(Id) ON DELETE CASCADE,
        CONSTRAINT FK_Enrollments_CourseClasses
            FOREIGN KEY (CourseClassId) REFERENCES dbo.CourseClasses(Id) ON DELETE CASCADE,
        CONSTRAINT UQ_Enrollments_Student_CourseClass UNIQUE (StudentId, CourseClassId)
    );
END;
