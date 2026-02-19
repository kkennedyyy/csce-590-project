-- ===============================================
-- Table: Courses
-- Purpose: Store Class Section information
-- ===============================================

CREATE TABLE ClassSections (
    SectionId INT PRIMARY KEY IDENTITY(1,1), -- Auto-incremental ID
    CourseId INT NOT NULL, -- Course ID
    InstructorId INT NOT NULL, -- Instructor ID
    -- NVARCHAR used to prevent loss of leading zeroes
    SectionCode NVARCHAR(10) NOT NULL, -- Examples: 001, 002, 003
    Capacity INT NOT NULL, -- Maximum about of students per section
    CreatedDate DATETIME2 DEFAULT GETDATE(), -- Creation Date

    -- Foreign Key CourseId references the CourseId in Courses, cannot use a CourseId if it is not in Courses.
    CONSTRAINT FK_ClassSections_Course FOREIGN KEY (CourseId)
        REFERENCES Courses(CourseId),

    -- Foreign Key InstructorId references the InstructorId in Instructors, cannot use a InstructorId if it is not in Instructors.
    CONSTRAINT FK_ClassSections_Instructor FOREIGN KEY (InstructorId)
        REFERENCES Instructors(InstructorId)
);