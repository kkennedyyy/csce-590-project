-- ========================================
-- SEED DATA
-- Order matters: Parent → Child
-- ========================================

-- -------------------------
-- Students
-- -------------------------
INSERT INTO Students (StudentId, Firstname, Lastname, Email) VALUES
(1, 'John', 'Smith', 'john.smith@email.com'),
(2, 'Sarah', 'Johnson', 'sarah.j@email.com'),
(3, 'Michael', 'Brown', 'michael.b@email.com'),
(4, 'Emily', 'Davis', 'emily.d@email.com'),
(5, 'Daniel', 'Wilson', 'daniel.w@email.com');

-- -------------------------
-- Courses
-- -------------------------
INSERT INTO Courses (CourseId, CourseName) VALUES
(101, 'Database Systems'),
(102, 'Operating Systems'),
(103, 'Computer Networks'),
(104, 'Software Engineering');

-- -------------------------
-- Instructors
-- -------------------------
INSERT INTO Instructors (InstructorId, InstructorName) VALUES
(1, 'Dr. Anderson'),
(2, 'Prof. Thompson'),
(3, 'Dr. Martinez');

-- -------------------------
-- Sections
-- -------------------------
INSERT INTO Sections (SectionId, CourseId, InstructorId) VALUES
(1001, 101, 1),  -- Database Systems - Dr. Anderson
(1002, 102, 2),  -- Operating Systems - Prof. Thompson
(1003, 103, 3),  -- Computer Networks - Dr. Martinez
(1004, 104, 1);  -- Software Engineering - Dr. Anderson

-- -------------------------
-- Enrollments
-- -------------------------
INSERT INTO Enrollments (EnrollmentId, StudentId, SectionId) VALUES
(1, 1, 1001),
(2, 1, 1003),
(3, 2, 1001),
(4, 2, 1002),
(5, 3, 1002),
(6, 3, 1004),
(7, 4, 1003),
(8, 5, 1004);