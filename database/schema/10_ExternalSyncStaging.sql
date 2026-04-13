IF OBJECT_ID('dbo.ExternalSourceSyncRuns', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ExternalSourceSyncRuns
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        PipelineRunId NVARCHAR(128) NOT NULL,
        StartedAtUtc DATETIMEOFFSET NOT NULL,
        CompletedAtUtc DATETIMEOFFSET NULL,
        Status NVARCHAR(40) NOT NULL,
        Summary NVARCHAR(MAX) NULL,
        CONSTRAINT UQ_ExternalSourceSyncRuns UNIQUE (PipelineRunId)
    );
END;

IF OBJECT_ID('dbo.StageClassFinderStudents', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.StageClassFinderStudents
    (
        ExternalStudentId NVARCHAR(128) NULL,
        FirstName NVARCHAR(80) NOT NULL,
        LastName NVARCHAR(80) NOT NULL,
        Email NVARCHAR(200) NOT NULL,
        Password NVARCHAR(200) NULL,
        Major NVARCHAR(120) NULL,
        Classification NVARCHAR(60) NULL,
        ObservedAtUtc DATETIMEOFFSET NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;

IF OBJECT_ID('dbo.StageClassFinderProfessors', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.StageClassFinderProfessors
    (
        ExternalProfessorId NVARCHAR(128) NULL,
        FirstName NVARCHAR(80) NOT NULL,
        LastName NVARCHAR(80) NOT NULL,
        Email NVARCHAR(200) NOT NULL,
        Password NVARCHAR(200) NULL,
        ClassesTaughtJson NVARCHAR(MAX) NULL,
        ObservedAtUtc DATETIMEOFFSET NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;

IF OBJECT_ID('dbo.StageClassFinderClasses', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.StageClassFinderClasses
    (
        ExternalClassId NVARCHAR(128) NOT NULL,
        CourseCode NVARCHAR(40) NOT NULL,
        ClassName NVARCHAR(160) NOT NULL,
        Department NVARCHAR(120) NULL,
        DepartmentCode NVARCHAR(20) NULL,
        CourseNumber INT NULL,
        SessionCode NVARCHAR(20) NULL,
        Semester NVARCHAR(40) NULL,
        ExternalProfessorId NVARCHAR(128) NULL,
        ProfessorEmail NVARCHAR(200) NULL,
        DaysOfWeekCompact NVARCHAR(20) NOT NULL,
        StartTime NVARCHAR(10) NOT NULL,
        EndTime NVARCHAR(10) NOT NULL,
        Location NVARCHAR(160) NOT NULL,
        MaxSeats INT NOT NULL,
        CurrentEnrolled INT NULL,
        Credits INT NOT NULL,
        ObservedAtUtc DATETIMEOFFSET NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;

IF COL_LENGTH('dbo.StageClassFinderClasses', 'CourseCode') IS NULL
    ALTER TABLE dbo.StageClassFinderClasses ADD CourseCode NVARCHAR(40) NOT NULL CONSTRAINT DF_StageClassFinderClasses_CourseCode DEFAULT '';

IF OBJECT_ID('dbo.StageClassFinderEnrollments', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.StageClassFinderEnrollments
    (
        ExternalEnrollmentId NVARCHAR(128) NULL,
        ExternalStudentId NVARCHAR(128) NOT NULL,
        ExternalClassId NVARCHAR(128) NOT NULL,
        EnrollmentDateUtc DATETIMEOFFSET NULL,
        Status NVARCHAR(40) NOT NULL,
        ObservedAtUtc DATETIMEOFFSET NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;

IF OBJECT_ID('dbo.StageClassFinderWaitlist', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.StageClassFinderWaitlist
    (
        ExternalWaitlistId NVARCHAR(128) NULL,
        ExternalStudentId NVARCHAR(128) NOT NULL,
        ExternalClassId NVARCHAR(128) NOT NULL,
        SignupDateUtc DATETIMEOFFSET NULL,
        Position INT NOT NULL,
        ObservedAtUtc DATETIMEOFFSET NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;
