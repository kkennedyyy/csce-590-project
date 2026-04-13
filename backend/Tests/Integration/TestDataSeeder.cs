using ClassFinder.Api.Data;
using ClassFinder.Api.Models;

namespace ClassFinder.Api.Tests.Integration;

internal static class TestDataSeeder
{
    public static void Seed(ClassFinderDbContext dbContext)
    {
        if (dbContext.Students.Any())
        {
            return;
        }

        var instructors = new List<Instructor>
        {
            new()
            {
                FirstName = "Emily",
                LastName = "Anderson",
                Email = "anderson@email.com",
                Password = "teacher123"
            },
            new()
            {
                FirstName = "Michael",
                LastName = "Brown",
                Email = "brown@email.com",
                Password = "teacher123"
            }
        };
        dbContext.Instructors.AddRange(instructors);
        dbContext.SaveChanges();

        var classes = new List<CourseClass>
        {
            new()
            {
                ClassName = "Introduction to Computer Science",
                CourseCode = "CSCE101",
                Department = "Computer Science",
                DepartmentCode = "CSCE",
                CourseNumber = 101,
                SessionCode = "01",
                Semester = "Fall",
                InstructorId = instructors[0].Id,
                Location = "ENGR 205",
                Credits = 3,
                Capacity = 30,
                DaysOfWeek = "Mon,Wed",
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(10, 15),
                DropDeadlineUtc = DateTimeOffset.UtcNow.AddDays(30)
            },
            new()
            {
                ClassName = "Data Structures",
                CourseCode = "CSCE210",
                Department = "Computer Science",
                DepartmentCode = "CSCE",
                CourseNumber = 210,
                SessionCode = "02",
                Semester = "Fall",
                InstructorId = instructors[0].Id,
                Location = "ZACH 351",
                Credits = 3,
                Capacity = 25,
                DaysOfWeek = "Tue,Thu",
                StartTime = new TimeOnly(10, 30),
                EndTime = new TimeOnly(11, 45),
                DropDeadlineUtc = DateTimeOffset.UtcNow.AddDays(30)
            },
            new()
            {
                ClassName = "Calculus II",
                CourseCode = "MATH200",
                Department = "Mathematics",
                DepartmentCode = "MATH",
                CourseNumber = 200,
                SessionCode = "03",
                Semester = "Fall",
                InstructorId = instructors[1].Id,
                Location = "MATH 121",
                Credits = 4,
                Capacity = 2,
                DaysOfWeek = "Mon,Wed",
                StartTime = new TimeOnly(9, 45),
                EndTime = new TimeOnly(11, 0),
                DropDeadlineUtc = DateTimeOffset.UtcNow.AddDays(30)
            },
            new()
            {
                ClassName = "Software Engineering",
                CourseCode = "CSCE331",
                Department = "Computer Science",
                DepartmentCode = "CSCE",
                CourseNumber = 331,
                SessionCode = "04",
                Semester = "Fall",
                InstructorId = instructors[1].Id,
                Location = "ZACH 200",
                Credits = 3,
                Capacity = 35,
                DaysOfWeek = "Tue,Thu",
                StartTime = new TimeOnly(12, 30),
                EndTime = new TimeOnly(13, 45),
                DropDeadlineUtc = DateTimeOffset.UtcNow.AddDays(30)
            },
            new()
            {
                ClassName = "General Physics",
                CourseCode = "PHYS201",
                Department = "Physics",
                DepartmentCode = "PHYS",
                CourseNumber = 201,
                SessionCode = "05",
                Semester = "Fall",
                InstructorId = instructors[1].Id,
                Location = "PHYS 112",
                Credits = 4,
                Capacity = 20,
                DaysOfWeek = "Fri",
                StartTime = new TimeOnly(13, 0),
                EndTime = new TimeOnly(15, 40),
                DropDeadlineUtc = DateTimeOffset.UtcNow.AddDays(30)
            }
        };
        dbContext.CourseClasses.AddRange(classes);
        dbContext.SaveChanges();

        var student = new Student
        {
            FirstName = "John",
            LastName = "Smith",
            Email = "john.smith@email.com",
            Password = "student123",
            Major = "Computer Science",
            Classification = "Junior"
        };
        var studentA = new Student
        {
            FirstName = "Ava",
            LastName = "Thomas",
            Email = "ava@email.com",
            Password = "student123",
            Major = "Mathematics",
            Classification = "Sophomore"
        };
        var studentB = new Student
        {
            FirstName = "Liam",
            LastName = "Young",
            Email = "liam@email.com",
            Password = "student123",
            Major = "Physics",
            Classification = "Freshman"
        };
        dbContext.Students.AddRange(student, studentA, studentB);
        dbContext.SaveChanges();

        dbContext.Enrollments.AddRange(
            new Enrollment
            {
                StudentId = student.Id,
                CourseClassId = classes[0].Id,
                Status = EnrollmentStatus.Enrolled
            },
            new Enrollment
            {
                StudentId = student.Id,
                CourseClassId = classes[1].Id,
                Status = EnrollmentStatus.Enrolled
            },
            new Enrollment
            {
                StudentId = student.Id,
                CourseClassId = classes[2].Id,
                Status = EnrollmentStatus.Waitlisted,
                WaitlistPosition = 1
            },
            new Enrollment
            {
                StudentId = studentA.Id,
                CourseClassId = classes[2].Id,
                Status = EnrollmentStatus.Enrolled
            },
            new Enrollment
            {
                StudentId = studentB.Id,
                CourseClassId = classes[2].Id,
                Status = EnrollmentStatus.Enrolled
            }
        );
        dbContext.SaveChanges();

        dbContext.StudentCourseHistories.AddRange(
            new StudentCourseHistory
            {
                StudentId = student.Id,
                CourseCode = "CSCE101",
                CompletedAtUtc = DateTimeOffset.UtcNow.AddMonths(-6)
            },
            new StudentCourseHistory
            {
                StudentId = student.Id,
                CourseCode = "CSCE210",
                CompletedAtUtc = DateTimeOffset.UtcNow.AddMonths(-4)
            }
        );
        dbContext.CoursePrerequisites.Add(
            new CoursePrerequisite
            {
                CourseClassId = classes[3].Id,
                RequiredCourseCode = "CSCE210"
            }
        );
        dbContext.SaveChanges();
    }
}
