-- Insert Student
DECLARE @StudentId INT;
INSERT INTO Students
    (Firstname, Lastname, Email)
VALUES
    ('John', 'Smith', 'john.smith@email.com');
SET @StudentId = SCOPE_IDENTITY();

-- Insert Course
DECLARE @CourseId INT;
INSERT INTO Courses
    (CourseCode, CourseName, Credits)
VALUES
    ('CSCE101', 'Introduction to Computer Science', 3);
SET @CourseId = SCOPE_IDENTITY();

-- Insert Instructor
DECLARE @InstructorId INT;
INSERT INTO Instructors
    (Firstname, Lastname, Email)
VALUES
    ('Dr.', 'Anderson', 'anderson@email.com');
SET @InstructorId = SCOPE_IDENTITY();

-- Insert Section
DECLARE @SectionId INT;
INSERT INTO ClassSections
    (CourseId, InstructorId, SectionCode, Capacity)
VALUES
    (@CourseId, @InstructorId, '001', 30);
SET @SectionId = SCOPE_IDENTITY();

-- Insert Enrollment
INSERT INTO Enrollments
    (StudentId, SectionId)
VALUES
    (@StudentId, @SectionId);
