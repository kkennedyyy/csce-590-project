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
            new() { FirstName = "Emily", LastName = "Anderson", Email = "anderson@email.com" },
            new() { FirstName = "Michael", LastName = "Brown", Email = "brown@email.com" }
        };
        dbContext.Instructors.AddRange(instructors);
        dbContext.SaveChanges();

        var classes = new List<CourseClass>
        {
            new()
            {
                ClassName = "Introduction to Computer Science",
                CourseCode = "CSCE101",
                InstructorId = instructors[0].Id,
                Location = "ENGR 205",
                Credits = 3,
                Capacity = 30,
                DaysOfWeek = "Mon,Wed",
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(10, 15)
            },
            new()
            {
                ClassName = "Data Structures",
                CourseCode = "CSCE210",
                InstructorId = instructors[0].Id,
                Location = "ZACH 351",
                Credits = 3,
                Capacity = 25,
                DaysOfWeek = "Tue,Thu",
                StartTime = new TimeOnly(10, 30),
                EndTime = new TimeOnly(11, 45)
            },
            new()
            {
                ClassName = "Calculus II",
                CourseCode = "MATH200",
                InstructorId = instructors[1].Id,
                Location = "MATH 121",
                Credits = 4,
                Capacity = 2,
                DaysOfWeek = "Mon,Wed",
                StartTime = new TimeOnly(9, 45),
                EndTime = new TimeOnly(11, 0)
            },
            new()
            {
                ClassName = "Software Engineering",
                CourseCode = "CSCE331",
                InstructorId = instructors[1].Id,
                Location = "ZACH 200",
                Credits = 3,
                Capacity = 35,
                DaysOfWeek = "Tue,Thu",
                StartTime = new TimeOnly(12, 30),
                EndTime = new TimeOnly(13, 45)
            },
            new()
            {
                ClassName = "General Physics",
                CourseCode = "PHYS201",
                InstructorId = instructors[1].Id,
                Location = "PHYS 112",
                Credits = 4,
                Capacity = 20,
                DaysOfWeek = "Fri",
                StartTime = new TimeOnly(13, 0),
                EndTime = new TimeOnly(15, 40)
            }
        };
        dbContext.CourseClasses.AddRange(classes);
        dbContext.SaveChanges();

        var student = new Student
        {
            FirstName = "John",
            LastName = "Smith",
            Email = "john.smith@email.com"
        };
        var studentA = new Student
        {
            FirstName = "Ava",
            LastName = "Thomas",
            Email = "ava@email.com"
        };
        var studentB = new Student
        {
            FirstName = "Liam",
            LastName = "Young",
            Email = "liam@email.com"
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
    }
}
