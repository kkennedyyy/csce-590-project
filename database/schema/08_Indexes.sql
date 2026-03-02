-- ===============================================
-- Indexes
-- Purpose: Shortcut to find all rows with a specific Id
-- ===============================================

-- Index sorts the IDs and points to their location in their respected tables
CREATE INDEX IX_Enrollments_StudentId ON Enrollments(StudentId); -- Index on the column StudentId in the Enrollments table.
CREATE INDEX IX_Waitlists_StudentId ON Waitlists(StudentId); -- Index on the column StudentId in the Waitlists table.
CREATE INDEX IX_ClassSchedule_SectionId ON ClassSchedule(SectionId); -- Index on the column SectionId in the ClassSchedule table.
CREATE INDEX IX_ClassSections_CourseId ON ClassSections(CourseId); -- Index on the column CourseId in the ClassSections table.
CREATE INDEX IX_ClassSections_InstructorId ON ClassSections(InstructorId); -- Index on the column InstructorId in the ClassSections table.
CREATE INDEX IX_Enrollments_SectionId ON Enrollments(SectionId);
CREATE INDEX IX_Waitlists_SectionId ON Waitlists(SectionId);