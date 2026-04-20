-- Stage tables populated by ADF before each run
-- These tables are used to stage data before it is transformed and loaded into the final tables.
-- These are reduced at the start of each copy and are never read by the app.

CREATE TABLE dbo.StageClassFinderStudents (
    ExternalStudentId NVARCHAR(50) NOT NULL,
    FirstName         NVARCHAR(100) NULL,
    LastName          NVARCHAR(100) NULL,
    Email             NVARCHAR(200) NULL,
    Password          NVARCHAR(200) NULL,
    Major             NVARCHAR(200) NULL,
    Classification    NVARCHAR(100) NULL
);

CREATE TABLE dbo.StageClassFinderProfessors (
    ExternalProfessorId NVARCHAR(50)  NOT NULL,
    FirstName           NVARCHAR(100) NULL,
    LastName            NVARCHAR(100) NULL,
    Email               NVARCHAR(200) NULL,
    Password            NVARCHAR(200) NULL
);

CREATE TABLE dbo.StageClassFinderClasses (
    ExternalClassId     NVARCHAR(50)  NOT NULL,
    CourseCode          NVARCHAR(50)  NULL,   
    ClassName           NVARCHAR(200) NULL,
    Department          NVARCHAR(100) NULL,
    DepartmentCode      NVARCHAR(20)  NULL,   
    CourseNumber        INT           NULL,  
    SessionCode         NVARCHAR(10)  NULL,
    Semester            NVARCHAR(50)  NULL,
    ExternalProfessorId NVARCHAR(50)  NULL,
    DaysOfWeekCompact   NVARCHAR(20)  NULL,   
    StartTime           NVARCHAR(10)  NULL,  
    EndTime             NVARCHAR(10)  NULL,
    Location            NVARCHAR(300) NULL,
    MaxSeats            INT           NULL,
    CurrentEnrolled     INT           NULL,
    Credits             INT           NULL
);

CREATE TABLE dbo.StageClassFinderEnrollments (
    ExternalEnrollmentId NVARCHAR(50)      NOT NULL,
    ExternalStudentId    NVARCHAR(50)      NULL,
    ExternalClassId      NVARCHAR(50)      NULL,
    EnrollmentDateUtc    DATETIMEOFFSET    NULL,
    Status               NVARCHAR(50)      NULL    
);

CREATE TABLE dbo.StageClassFinderWaitlist (
    ExternalWaitlistId NVARCHAR(50)   NOT NULL,
    ExternalStudentId  NVARCHAR(50)   NULL,
    ExternalClassId    NVARCHAR(50)   NULL,
    SignupDateUtc      DATETIMEOFFSET NULL,
    Position           INT            NULL
);

-- Tracks each ADF pipeline run
CREATE TABLE dbo.ExternalSyncLog (
    Id             INT IDENTITY(1,1)  PRIMARY KEY,
    PipelineRunId  NVARCHAR(200)      NOT NULL,
    StartedAtUtc   DATETIMEOFFSET     NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CompletedAtUtc DATETIMEOFFSET     NULL,
    Summary        NVARCHAR(MAX)      NULL
);