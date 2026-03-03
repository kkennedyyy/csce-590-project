IF OBJECT_ID('dbo.Enrollments', 'U') IS NOT NULL DROP TABLE dbo.Enrollments;
IF OBJECT_ID('dbo.CourseClasses', 'U') IS NOT NULL DROP TABLE dbo.CourseClasses;
IF OBJECT_ID('dbo.Students', 'U') IS NOT NULL DROP TABLE dbo.Students;
IF OBJECT_ID('dbo.Instructors', 'U') IS NOT NULL DROP TABLE dbo.Instructors;

CREATE TABLE dbo.Students (
  Id INT IDENTITY(1,1) PRIMARY KEY,
  FirstName NVARCHAR(80) NOT NULL,
  LastName NVARCHAR(80) NOT NULL,
  Email NVARCHAR(200) NOT NULL UNIQUE
);

CREATE TABLE dbo.Instructors (
  Id INT IDENTITY(1,1) PRIMARY KEY,
  FirstName NVARCHAR(80) NOT NULL,
  LastName NVARCHAR(80) NOT NULL,
  Email NVARCHAR(200) NOT NULL UNIQUE
);

CREATE TABLE dbo.CourseClasses (
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
  CONSTRAINT FK_CourseClasses_Instructors FOREIGN KEY (InstructorId) REFERENCES dbo.Instructors(Id)
);

CREATE TABLE dbo.Enrollments (
  Id INT IDENTITY(1,1) PRIMARY KEY,
  StudentId INT NOT NULL,
  CourseClassId INT NOT NULL,
  Status INT NOT NULL,
  WaitlistPosition INT NULL,
  CONSTRAINT FK_Enrollments_Students FOREIGN KEY (StudentId) REFERENCES dbo.Students(Id) ON DELETE CASCADE,
  CONSTRAINT FK_Enrollments_CourseClasses FOREIGN KEY (CourseClassId) REFERENCES dbo.CourseClasses(Id) ON DELETE CASCADE,
  CONSTRAINT UQ_StudentClass UNIQUE (StudentId, CourseClassId)
);

CREATE INDEX IX_Enrollments_CourseClassId_Status ON dbo.Enrollments(CourseClassId, Status);
CREATE INDEX IX_CourseClasses_CourseCode ON dbo.CourseClasses(CourseCode);
