-- ===============================================
-- Table: Enrollments
-- Purpose: Store Enrollment information
-- ===============================================

CREATE TABLE Enrollments (
    EnrollmentId INT IDENTITY(1,1) PRIMARY KEY, -- Enrollment ID
    StudentId INT NOT NULL, -- Student Id (Students)
    SectionId INT NOT NULL, -- Section Id (Sections)
    EnrollmentDate DATETIME2 DEFAULT GETDATE(),

    -- Foreign Key StudentId references the StudentId in Students, cannot use a StudentId if it is not in Students.
    CONSTRAINT FK_Enrollments_Student FOREIGN KEY (StudentId)
        REFERENCES Students(StudentId),
    
    -- Foreign Key SectionId references the SectionId in ClassSections, cannot use a SectionId if it is not in ClassSections.
    CONSTRAINT FK_Enrollments_Section FOREIGN KEY (SectionId)
        REFERENCES ClassSections(SectionId)
);