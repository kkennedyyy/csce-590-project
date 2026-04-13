using System.Globalization;
using System.Text.RegularExpressions;
using ClassFinder.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ClassFinder.Api.Data;

public static class SeedData
{
    private static readonly IReadOnlyList<CatalogClassSeed> ExtendedCatalog =
    [
        new("CSCE101", "Intro to Computer Science", "Dr. Smith", "Mon,Wed", "09:00", "10:15", "ENGR 205", 3, 30),
        new("MATH200", "Calculus II", "Prof. Adams", "Mon,Wed", "09:45", "11:00", "MATH 121", 4, 40),
        new("CSCE210", "Data Structures", "Dr. Nguyen", "Tue,Thu", "10:30", "11:45", "ZACH 351", 3, 28),
        new("PHYS201", "University Physics", "Dr. Patel", "Tue,Thu", "13:00", "14:15", "PHYS 112", 4, 36),
        new("HIST230", "Modern World History", "Prof. Johnson", "Mon,Wed", "14:00", "15:15", "LAAH 106", 3, 45),
        new("CSCE312", "Computer Organization", "Dr. Lopez", "Mon,Wed", "11:00", "12:15", "HRBB 124", 3, 35),
        new(
            "CSCE313",
            "Intro to Computer Systems",
            "Dr. Coleman",
            "Tue,Thu",
            "15:30",
            "16:45",
            "ZACH 350",
            3,
            30
        ),
        new(
            "ENGL104",
            "Composition and Rhetoric",
            "Prof. Lee",
            "Tue,Thu",
            "08:00",
            "09:15",
            "HECC 116",
            3,
            22
        ),
        new("CHEM107", "General Chemistry", "Dr. Gomez", "Mon,Wed", "16:00", "17:15", "CHEM 102", 4, 40),
        new(
            "STAT211",
            "Statistics for Engineers",
            "Dr. Rivera",
            "Mon,Wed,Fri",
            "10:00",
            "10:50",
            "BLOC 110",
            3,
            50
        ),
        new("CSCE331", "Software Engineering", "Dr. Brown", "Tue,Thu", "12:30", "13:45", "ZACH 200", 3, 32),
        new("BIOL111", "General Biology", "Dr. Kim", "Tue,Thu", "18:00", "19:15", "BSBE 210", 4, 38),
        new("ARTS150", "Foundations of Design", "Prof. Zhang", "Fri", "09:30", "12:20", "ARCC 310", 3, 18),
        new("CSCE451", "Data Mining", "Dr. Silva", "Mon", "19:00", "20:15", "ZACH 349", 3, 24),
        new("NIGHT499", "Late Night Seminar", "Dr. Night", "Thu", "20:30", "22:00", "ONLINE", 2, 20),
        new("PHIL240", "Ethics in Technology", "Prof. Williams", "Wed", "17:30", "18:45", "HECC 101", 3, 30),
        new(
            "CSCE420",
            "Artificial Intelligence",
            "Dr. Howard",
            "Mon,Wed",
            "12:30",
            "13:45",
            "ZACH 355",
            3,
            32
        ),
        new("MUSC221", "Music Theory II", "Prof. Carter", "Tue,Thu", "11:00", "12:15", "MOUS 107", 3, 26),
        new("ECON202", "Macro Economics", "Dr. Evans", "Mon,Wed", "08:30", "09:45", "RUDD 301", 3, 35),
        new(
            "PSYC107",
            "General Psychology",
            "Dr. Allen",
            "Fri",
            "13:00",
            "15:40",
            "HECC 204",
            3,
            60
        ),
    ];

    public static async Task InitializeAsync(ClassFinderDbContext context)
    {
        if (await context.Students.AnyAsync())
        {
            await EnsureExtendedCatalogAsync(context);
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
            },
        };
        context.Instructors.AddRange(instructors);
        await context.SaveChangesAsync();

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
            },
        };

        context.CourseClasses.AddRange(classes);
        await context.SaveChangesAsync();

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

        context.Students.AddRange(student, studentA, studentB);
        await context.SaveChangesAsync();

        context.Enrollments.AddRange(
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

        await context.SaveChangesAsync();

        context.StudentCourseHistories.AddRange(
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
        await context.SaveChangesAsync();

        await EnsureExtendedCatalogAsync(context);
        await EnsurePrerequisitesAsync(context);
    }

    private static async Task EnsureExtendedCatalogAsync(ClassFinderDbContext context)
    {
        var instructors = await context.Instructors.ToListAsync();
        var instructorByName = instructors.ToDictionary(
            x => NormalizeInstructorName($"{x.FirstName} {x.LastName}"),
            x => x
        );

        var createdInstructor = false;
        foreach (var instructorName in ExtendedCatalog.Select(x => x.InstructorName).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var key = NormalizeInstructorName(instructorName);
            if (instructorByName.ContainsKey(key))
            {
                continue;
            }

            var instructor = BuildInstructor(instructorName);
            context.Instructors.Add(instructor);
            instructorByName[key] = instructor;
            createdInstructor = true;
        }

        if (createdInstructor)
        {
            await context.SaveChangesAsync();
        }

        var existingClasses = await context.CourseClasses
            .Include(x => x.Instructor)
            .ToListAsync();

        var classByKey = existingClasses.ToDictionary(
            x => BuildClassKey(x.CourseCode, x.DaysOfWeek, x.StartTime, x.EndTime, x.Location),
            x => x
        );

        foreach (var item in ExtendedCatalog)
        {
            var startTime = ParseTime(item.StartTime);
            var endTime = ParseTime(item.EndTime);
            var classKey = BuildClassKey(item.CourseCode, item.DaysOfWeek, startTime, endTime, item.Location);
            var instructor = instructorByName[NormalizeInstructorName(item.InstructorName)];

            if (classByKey.TryGetValue(classKey, out var existing))
            {
                // Align existing rows with demo catalog values.
                existing.ClassName = item.ClassName;
                existing.Credits = item.Credits;
                existing.Capacity = item.Capacity;
                existing.InstructorId = instructor.Id;
                existing.DepartmentCode = ExtractDepartmentCode(item.CourseCode);
                existing.Department = ExtractDepartmentName(item.CourseCode);
                existing.CourseNumber = ExtractCourseNumber(item.CourseCode);
                existing.SessionCode = existing.SessionCode.Length > 0 ? existing.SessionCode : $"{existing.Id:00}";
                existing.Semester = existing.Semester.Length > 0 ? existing.Semester : "Fall";
                existing.DropDeadlineUtc ??= DateTimeOffset.UtcNow.AddDays(30);
                continue;
            }

            var created = new CourseClass
            {
                ClassName = item.ClassName,
                CourseCode = item.CourseCode,
                Department = ExtractDepartmentName(item.CourseCode),
                DepartmentCode = ExtractDepartmentCode(item.CourseCode),
                CourseNumber = ExtractCourseNumber(item.CourseCode),
                SessionCode = $"{classByKey.Count + 1:00}",
                Semester = "Fall",
                Location = item.Location,
                Credits = item.Credits,
                Capacity = item.Capacity,
                DaysOfWeek = item.DaysOfWeek,
                StartTime = startTime,
                EndTime = endTime,
                InstructorId = instructor.Id,
                DropDeadlineUtc = DateTimeOffset.UtcNow.AddDays(30)
            };

            context.CourseClasses.Add(created);
            classByKey[classKey] = created;
        }

        await context.SaveChangesAsync();
        await EnsurePrerequisitesAsync(context);
    }

    private static async Task EnsurePrerequisitesAsync(ClassFinderDbContext context)
    {
        var courseClasses = await context.CourseClasses.ToListAsync();
        var prerequisitePairs = new (string CourseCode, string RequiredCourseCode)[]
        {
            ("CSCE210", "CSCE101"),
            ("CSCE312", "CSCE210"),
            ("CSCE331", "CSCE210"),
            ("CSCE420", "CSCE331"),
            ("CSCE451", "CSCE331")
        };

        foreach (var (courseCode, requiredCourseCode) in prerequisitePairs)
        {
            var targetClasses = courseClasses.Where(item => item.CourseCode.Equals(courseCode, StringComparison.OrdinalIgnoreCase));
            foreach (var targetClass in targetClasses)
            {
                var exists = await context.CoursePrerequisites.AnyAsync(
                    item =>
                        item.CourseClassId == targetClass.Id
                        && item.RequiredCourseCode == requiredCourseCode
                );
                if (exists)
                {
                    continue;
                }

                context.CoursePrerequisites.Add(
                    new CoursePrerequisite
                    {
                        CourseClassId = targetClass.Id,
                        RequiredCourseCode = requiredCourseCode
                    }
                );
            }
        }

        await context.SaveChangesAsync();
    }

    private static Instructor BuildInstructor(string fullName)
    {
        var cleaned = Regex.Replace(fullName, @"\s+", " ").Trim();
        var parts = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lastName = parts.Length > 0 ? parts[^1] : "Instructor";
        var firstName = parts.Length > 1 ? string.Join(" ", parts[..^1]) : "Demo";
        var local = Regex.Replace(cleaned.ToLowerInvariant(), @"[^a-z0-9]+", ".").Trim('.');
        var emailLocal = string.IsNullOrWhiteSpace(local) ? "demo.instructor" : local;

        return new Instructor
        {
            FirstName = firstName,
            LastName = lastName,
            Email = $"{emailLocal}@demo.classfinder.local"
        };
    }

    private static string NormalizeInstructorName(string value)
    {
        return Regex.Replace(value.Trim().ToLowerInvariant(), @"\s+", " ");
    }

    private static string ExtractDepartmentCode(string courseCode)
    {
        return new string(courseCode.TakeWhile(char.IsLetter).ToArray());
    }

    private static string ExtractDepartmentName(string courseCode)
    {
        return ExtractDepartmentCode(courseCode) switch
        {
            "CSCE" => "Computer Science",
            "MATH" => "Mathematics",
            "PHYS" => "Physics",
            "CHEM" => "Chemistry",
            "STAT" => "Statistics",
            "BIOL" => "Biology",
            "ARTS" => "Art",
            "ENGL" => "English",
            "HIST" => "History",
            "ECON" => "Economics",
            "MUSC" => "Music",
            "PSYC" => "Psychology",
            "PHIL" => "Philosophy",
            _ => ExtractDepartmentCode(courseCode)
        };
    }

    private static int? ExtractCourseNumber(string courseCode)
    {
        var digits = new string(courseCode.SkipWhile(char.IsLetter).TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
    }

    private static string BuildClassKey(
        string courseCode,
        string daysOfWeek,
        TimeOnly startTime,
        TimeOnly endTime,
        string location
    )
    {
        return string.Join(
            "|",
            courseCode.Trim().ToUpperInvariant(),
            NormalizeDays(daysOfWeek),
            startTime.ToString("HH:mm", CultureInfo.InvariantCulture),
            endTime.ToString("HH:mm", CultureInfo.InvariantCulture),
            location.Trim().ToUpperInvariant()
        );
    }

    private static string NormalizeDays(string value)
    {
        return string.Join(
            ",",
            value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => x.ToUpperInvariant())
        );
    }

    private static TimeOnly ParseTime(string value)
    {
        return TimeOnly.ParseExact(value, "HH:mm", CultureInfo.InvariantCulture);
    }

    private sealed record CatalogClassSeed(
        string CourseCode,
        string ClassName,
        string InstructorName,
        string DaysOfWeek,
        string StartTime,
        string EndTime,
        string Location,
        int Credits,
        int Capacity
    );
}
