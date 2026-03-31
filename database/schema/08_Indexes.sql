-- Indexes aligned with canonical schema.

IF OBJECT_ID('dbo.Enrollments', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Enrollments_StudentId_CourseClassId' AND object_id = OBJECT_ID('dbo.Enrollments'))
        CREATE UNIQUE INDEX IX_Enrollments_StudentId_CourseClassId ON dbo.Enrollments(StudentId, CourseClassId);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Enrollments_CourseClassId_Status' AND object_id = OBJECT_ID('dbo.Enrollments'))
        CREATE INDEX IX_Enrollments_CourseClassId_Status ON dbo.Enrollments(CourseClassId, Status);
END;

IF OBJECT_ID('dbo.CourseClasses', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CourseClasses_CourseCode' AND object_id = OBJECT_ID('dbo.CourseClasses'))
        CREATE INDEX IX_CourseClasses_CourseCode ON dbo.CourseClasses(CourseCode);
END;
