-- ========================================
-- SEED DATA
-- Order matters: Parent → Child
-- ========================================

-- -------------------------
-- Students
-- -------------------------
INSERT INTO Students
    (Firstname, Lastname, Email)
VALUES
    ('John', 'Smith', 'john.smith@email.com');

-- -------------------------
-- Courses
-- -------------------------
INSERT INTO Courses
    (CourseCode, CourseName, Credits)
VALUES
    ('CSCE101', 'Introduction to Computer Science', 3);

-- -------------------------
-- Instructors
-- -------------------------
INSERT INTO Instructors
    (Firstname, Lastname, Email)
VALUES
    ('Dr.', 'Anderson', 'anderson@email.com');

-- -------------------------
-- Sections
-- -------------------------
INSERT INTO ClassSections
    (CourseId, InstructorId, SectionCode, Capacity)
VALUES
    (1, 1, '001', 30);
-- CSCE101 - Dr. Anderson

-- -------------------------
-- Enrollments
-- -------------------------
INSERT INTO Enrollments
    (StudentId, SectionId)
VALUES
    (1, 1);  -- John Smith enrolled in Section 001