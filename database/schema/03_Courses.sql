

CREATE TABLE Course (
    CourseId INT PRIMARY KEY IDENTITY(1,1), -- Auto-incremental ID
    CourseCode NVARCHAR(20) UNIQUE NOT NULL, -- Unique Course Code
    CourseName NVARCHAR(100) NOT NULL, -- Course Name
    Credits INT NOT NULL, 
    CreatedDate DATETIME2 DEFAULT GETDATE()
);