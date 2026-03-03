SET NOCOUNT ON;

DELETE FROM dbo.Enrollments;
DELETE FROM dbo.CourseClasses;
DELETE FROM dbo.Students;
DELETE FROM dbo.Instructors;

INSERT INTO dbo.Instructors (FirstName, LastName, Email)
VALUES
  ('Emily', 'Anderson', 'anderson@email.com'),
  ('Michael', 'Brown', 'brown@email.com');

INSERT INTO dbo.CourseClasses (ClassName, CourseCode, Location, Credits, Capacity, DaysOfWeek, StartTime, EndTime, InstructorId)
VALUES
  ('Introduction to Computer Science', 'CSCE101', 'ENGR 205', 3, 30, 'Mon,Wed', '09:00', '10:15', 1),
  ('Data Structures', 'CSCE210', 'ZACH 351', 3, 25, 'Tue,Thu', '10:30', '11:45', 1),
  ('Calculus II', 'MATH200', 'MATH 121', 4, 2, 'Mon,Wed', '09:45', '11:00', 2),
  ('Software Engineering', 'CSCE331', 'ZACH 200', 3, 35, 'Tue,Thu', '12:30', '13:45', 2),
  ('General Physics', 'PHYS201', 'PHYS 112', 4, 20, 'Fri', '13:00', '15:40', 2);

INSERT INTO dbo.Students (FirstName, LastName, Email)
VALUES
  ('John', 'Smith', 'john.smith@email.com'),
  ('Ava', 'Thomas', 'ava@email.com'),
  ('Liam', 'Young', 'liam@email.com');

INSERT INTO dbo.Enrollments (StudentId, CourseClassId, Status, WaitlistPosition)
VALUES
  (1, 1, 1, NULL),
  (1, 2, 1, NULL),
  (1, 3, 2, 1),
  (2, 3, 1, NULL),
  (3, 3, 1, NULL);
