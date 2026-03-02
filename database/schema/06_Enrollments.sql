-- ===============================================
-- Table: Enrollments
-- Purpose: Store Enrollment information
-- ===============================================

CREATE TABLE Enrollments
(
    EnrollmentId INT IDENTITY(1,1) PRIMARY KEY,
    -- Auto-Incremental ID
    StudentId INT NOT NULL,
    -- Student Id (Students)
    SectionId INT NOT NULL,
    -- Section Id (ClassSections)
    EnrollmentDate DATETIME2 DEFAULT GETDATE(),
    -- Date of Enrollment

    -- Foreign Key StudentId references the StudentId in Students, cannot use a StudentId if it is not in Students.
    CONSTRAINT FK_Enrollments_Student FOREIGN KEY (StudentId)
        REFERENCES Students(StudentId),

    -- Foreign Key SectionId references the SectionId in ClassSections, cannot use a SectionId if it is not in ClassSections.
    CONSTRAINT FK_Enrollments_Section FOREIGN KEY (SectionId)
        REFERENCES ClassSections(SectionId)
    -- Prevent duplicate enrollments (same student enrolling in same section twice)
);
ALTER TABLE Enrollment
    ADD CONSTRAINT UQ_Enrollments_Student_Section
    UNIQUE (StudentId, SectionId);