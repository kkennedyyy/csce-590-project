IF COL_LENGTH('dbo.Students', 'ExternalId') IS NULL
    ALTER TABLE dbo.Students ADD ExternalId NVARCHAR(128) NULL;
IF COL_LENGTH('dbo.Students', 'Password') IS NULL
    ALTER TABLE dbo.Students ADD Password NVARCHAR(200) NOT NULL CONSTRAINT DF_Students_Password DEFAULT '';
IF COL_LENGTH('dbo.Students', 'Major') IS NULL
    ALTER TABLE dbo.Students ADD Major NVARCHAR(120) NOT NULL CONSTRAINT DF_Students_Major DEFAULT '';
IF COL_LENGTH('dbo.Students', 'Classification') IS NULL
    ALTER TABLE dbo.Students ADD Classification NVARCHAR(60) NOT NULL CONSTRAINT DF_Students_Classification DEFAULT '';

IF COL_LENGTH('dbo.Instructors', 'ExternalId') IS NULL
    ALTER TABLE dbo.Instructors ADD ExternalId NVARCHAR(128) NULL;
IF COL_LENGTH('dbo.Instructors', 'Password') IS NULL
    ALTER TABLE dbo.Instructors ADD Password NVARCHAR(200) NOT NULL CONSTRAINT DF_Instructors_Password DEFAULT '';

IF COL_LENGTH('dbo.CourseClasses', 'ExternalId') IS NULL
    ALTER TABLE dbo.CourseClasses ADD ExternalId NVARCHAR(128) NULL;
IF COL_LENGTH('dbo.CourseClasses', 'Department') IS NULL
    ALTER TABLE dbo.CourseClasses ADD Department NVARCHAR(120) NOT NULL CONSTRAINT DF_CourseClasses_Department DEFAULT '';
IF COL_LENGTH('dbo.CourseClasses', 'DepartmentCode') IS NULL
    ALTER TABLE dbo.CourseClasses ADD DepartmentCode NVARCHAR(20) NOT NULL CONSTRAINT DF_CourseClasses_DepartmentCode DEFAULT '';
IF COL_LENGTH('dbo.CourseClasses', 'CourseNumber') IS NULL
    ALTER TABLE dbo.CourseClasses ADD CourseNumber INT NULL;
IF COL_LENGTH('dbo.CourseClasses', 'SessionCode') IS NULL
    ALTER TABLE dbo.CourseClasses ADD SessionCode NVARCHAR(20) NOT NULL CONSTRAINT DF_CourseClasses_SessionCode DEFAULT '';
IF COL_LENGTH('dbo.CourseClasses', 'Semester') IS NULL
    ALTER TABLE dbo.CourseClasses ADD Semester NVARCHAR(40) NOT NULL CONSTRAINT DF_CourseClasses_Semester DEFAULT '';
IF COL_LENGTH('dbo.CourseClasses', 'DropDeadlineUtc') IS NULL
    ALTER TABLE dbo.CourseClasses ADD DropDeadlineUtc DATETIMEOFFSET NULL;
IF COL_LENGTH('dbo.CourseClasses', 'Description') IS NULL
    ALTER TABLE dbo.CourseClasses ADD Description NVARCHAR(1000) NOT NULL CONSTRAINT DF_CourseClasses_Description DEFAULT '';

IF COL_LENGTH('dbo.Enrollments', 'ExternalRecordId') IS NULL
    ALTER TABLE dbo.Enrollments ADD ExternalRecordId NVARCHAR(128) NULL;
IF COL_LENGTH('dbo.Enrollments', 'SourceSystem') IS NULL
    ALTER TABLE dbo.Enrollments ADD SourceSystem NVARCHAR(40) NOT NULL CONSTRAINT DF_Enrollments_SourceSystem DEFAULT 'Application';
IF COL_LENGTH('dbo.Enrollments', 'StatusChangedAtUtc') IS NULL
    ALTER TABLE dbo.Enrollments ADD StatusChangedAtUtc DATETIMEOFFSET NOT NULL CONSTRAINT DF_Enrollments_StatusChangedAtUtc DEFAULT SYSUTCDATETIME();
IF COL_LENGTH('dbo.Enrollments', 'LastSeenInExternalSyncUtc') IS NULL
    ALTER TABLE dbo.Enrollments ADD LastSeenInExternalSyncUtc DATETIMEOFFSET NULL;

IF OBJECT_ID('dbo.CoursePrerequisites', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CoursePrerequisites
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        CourseClassId INT NOT NULL,
        RequiredCourseCode NVARCHAR(40) NOT NULL,
        CONSTRAINT FK_CoursePrerequisites_CourseClasses
            FOREIGN KEY (CourseClassId) REFERENCES dbo.CourseClasses(Id) ON DELETE CASCADE,
        CONSTRAINT UQ_CoursePrerequisites UNIQUE (CourseClassId, RequiredCourseCode)
    );
END;

IF OBJECT_ID('dbo.StudentCourseHistories', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.StudentCourseHistories
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        StudentId INT NOT NULL,
        CourseCode NVARCHAR(40) NOT NULL,
        CompletedAtUtc DATETIMEOFFSET NOT NULL,
        CONSTRAINT FK_StudentCourseHistories_Students
            FOREIGN KEY (StudentId) REFERENCES dbo.Students(Id) ON DELETE CASCADE,
        CONSTRAINT UQ_StudentCourseHistories UNIQUE (StudentId, CourseCode)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Students_ExternalId' AND object_id = OBJECT_ID('dbo.Students'))
    CREATE UNIQUE INDEX IX_Students_ExternalId ON dbo.Students(ExternalId) WHERE ExternalId IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Instructors_ExternalId' AND object_id = OBJECT_ID('dbo.Instructors'))
    CREATE UNIQUE INDEX IX_Instructors_ExternalId ON dbo.Instructors(ExternalId) WHERE ExternalId IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CourseClasses_ExternalId' AND object_id = OBJECT_ID('dbo.CourseClasses'))
    CREATE UNIQUE INDEX IX_CourseClasses_ExternalId ON dbo.CourseClasses(ExternalId) WHERE ExternalId IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CourseClasses_DepartmentCode' AND object_id = OBJECT_ID('dbo.CourseClasses'))
    CREATE INDEX IX_CourseClasses_DepartmentCode ON dbo.CourseClasses(DepartmentCode);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Enrollments_ExternalRecordId' AND object_id = OBJECT_ID('dbo.Enrollments'))
    CREATE INDEX IX_Enrollments_ExternalRecordId ON dbo.Enrollments(ExternalRecordId) WHERE ExternalRecordId IS NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Enrollments_CourseClassId_WaitlistPosition' AND object_id = OBJECT_ID('dbo.Enrollments'))
    CREATE INDEX IX_Enrollments_CourseClassId_WaitlistPosition ON dbo.Enrollments(CourseClassId, WaitlistPosition);
