-- ===============================================
-- Table: Enrollments
-- Purpose: Enrollment Information
-- ===============================================

CREATE TABLE Waitlists (
    WaitlistId INT IDENTITY(1,1) PRIMARY KEY, -- Auto-Incremental ID
    StudentId INT NOT NULL, -- Student ID (Students)
    SectionId INT NOT NULL, -- Section ID (ClassSections)
    Position INT NOT NULL, -- Position in line
    CreatedDate DATETIME2 DEFAULT GETDATE(), -- Date created

    -- Foreign Key StudentId references the StudentId in Students, cannot use a StudentId if it is not in Students.
    CONSTRAINT FK_Waitlists_Student FOREIGN KEY (StudentId)
        REFERENCES Students(StudentId),

    -- Foreign Key SectionId references the SectionId in ClassSections, cannot use a SectionId if it is not in ClassSections.
    CONSTRAINT FK_Waitlists_Section FOREIGN KEY (SectionId)
        REFERENCES ClassSections(SectionId)
);