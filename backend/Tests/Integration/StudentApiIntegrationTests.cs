using System.Net;
using System.Net.Http.Json;
using ClassFinder.Api.Data;
using ClassFinder.Api.DTOs;
using ClassFinder.Api.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ClassFinder.Api.Tests.Integration;

public class StudentApiIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public StudentApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.ResetState();
    }

    [Fact]
    public async Task GetStudentClasses_ReturnsExpectedFields_AndHttp200()
    {
        var studentId = _factory.GetSampleStudentId();

        var response = await _client.GetAsync($"/api/students/{studentId}/classes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var classes = await response.Content.ReadFromJsonAsync<List<StudentClassDto>>();
        Assert.NotNull(classes);
        Assert.NotEmpty(classes!);
        Assert.All(
            classes!,
            classItem =>
            {
                Assert.True(classItem.ClassId > 0);
                Assert.False(string.IsNullOrWhiteSpace(classItem.ClassName));
                Assert.False(string.IsNullOrWhiteSpace(classItem.CourseCode));
                Assert.False(string.IsNullOrWhiteSpace(classItem.InstructorName));
                Assert.False(string.IsNullOrWhiteSpace(classItem.DaysTimes));
                Assert.False(string.IsNullOrWhiteSpace(classItem.Location));
                Assert.True(classItem.Credits > 0);
            }
        );
        Assert.Contains(classes!, x => x.IsWaitlisted);
    }

    [Fact]
    public async Task GetClassById_ReturnsCapacityInvariant_AndWaitlistWhenFull()
    {
        var fullClassId = _factory.GetFullClassId();

        var response = await _client.GetAsync($"/api/classes/{fullClassId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var classDetail = await response.Content.ReadFromJsonAsync<ClassDetailDto>();
        Assert.NotNull(classDetail);
        Assert.True(classDetail!.Capacity >= classDetail.EnrolledCount);
        Assert.True(classDetail.IsAtCapacity);
        Assert.True(classDetail.WaitlistCount > 0);
        Assert.NotEmpty(classDetail.WaitlistPositions);
    }

    [Fact]
    public async Task GetStudentSchedule_ReturnsCalendarEvents_WithDayAndTime()
    {
        var studentId = _factory.GetSampleStudentId();

        var response = await _client.GetAsync($"/api/students/{studentId}/schedule");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var events = await response.Content.ReadFromJsonAsync<List<ScheduleEventDto>>();
        Assert.NotNull(events);
        Assert.NotEmpty(events!);
        Assert.All(
            events!,
            scheduleEvent =>
            {
                Assert.True(scheduleEvent.ClassId > 0);
                Assert.False(string.IsNullOrWhiteSpace(scheduleEvent.DayOfWeek));
                Assert.Matches("^\\d{2}:\\d{2}$", scheduleEvent.StartTime);
                Assert.Matches("^\\d{2}:\\d{2}$", scheduleEvent.EndTime);
            }
        );
    }

    [Fact]
    public async Task GetCloudClasses_ReturnsPagedClassData_WithExpectedShape()
    {
        var response = await _client.GetAsync("/api/classes?page=1&pageSize=3&search=CSCE");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<CloudClassPageDto>();
        Assert.NotNull(payload);
        Assert.Equal(1, payload!.Page);
        Assert.Equal(3, payload.PageSize);
        Assert.NotEmpty(payload.Classes);
        Assert.All(
            payload.Classes,
            item =>
            {
                Assert.True(item.SectionId > 0);
                Assert.False(string.IsNullOrWhiteSpace(item.Id));
                Assert.False(string.IsNullOrWhiteSpace(item.Title));
                Assert.False(string.IsNullOrWhiteSpace(item.Instructor));
                Assert.NotEmpty(item.Days);
                Assert.Matches("^\\d{2}:\\d{2}$", item.StartTime);
                Assert.Matches("^\\d{2}:\\d{2}$", item.EndTime);
            }
        );
    }

    [Fact]
    public async Task StudentSignup_CreatesAccount_AndReturnsAuthEnvelope()
    {
        var request = new StudentSignupRequestDto
        {
            FirstName = "Demo",
            LastName = "Student",
            Email = $"demo.student.{Guid.NewGuid():N}@email.com",
            Password = "securePass123",
            Major = "Computer Science",
            Classification = "Senior"
        };

        var response = await _client.PostAsJsonAsync("/api/auth/signup/student", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<CloudAuthEnvelopeDto>();
        Assert.NotNull(payload);
        Assert.Equal("student", payload!.User.Role);
        Assert.Equal(request.Email.ToLowerInvariant(), payload.User.Email);
        Assert.StartsWith("student-", payload.User.UserId);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClassFinderDbContext>();
        var student = await dbContext.Students.SingleAsync(item => item.Email == request.Email.ToLowerInvariant());
        Assert.StartsWith("pbkdf2-sha256$", student.Password);
    }

    [Fact]
    public async Task StudentSignup_CanLogin_AndLoadEmptySchedule()
    {
        var email = $"new.student.{Guid.NewGuid():N}@email.com";
        var signupResponse = await _client.PostAsJsonAsync(
            "/api/auth/signup/student",
            new StudentSignupRequestDto
            {
                FirstName = "New",
                LastName = "Student",
                Email = email,
                Password = "securePass123"
            }
        );

        Assert.Equal(HttpStatusCode.Created, signupResponse.StatusCode);
        var signupPayload = await signupResponse.Content.ReadFromJsonAsync<CloudAuthEnvelopeDto>();
        Assert.NotNull(signupPayload);

        var loginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequestDto
            {
                Email = email,
                Password = "securePass123",
                Role = "student"
            }
        );

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<CloudAuthEnvelopeDto>();
        Assert.NotNull(loginPayload);
        Assert.Equal(signupPayload!.User.UserId, loginPayload!.User.UserId);

        var scheduleResponse = await _client.GetAsync($"/api/students/{signupPayload.User.UserId}/schedule/state");
        Assert.Equal(HttpStatusCode.OK, scheduleResponse.StatusCode);
        var schedule = await scheduleResponse.Content.ReadFromJsonAsync<CloudStudentScheduleDto>();
        Assert.NotNull(schedule);
        Assert.Equal(signupPayload.User.UserId, schedule!.StudentId);
        Assert.Empty(schedule.ScheduledClasses);
        Assert.Equal(0, schedule.CurrentCredits);
    }

    [Fact]
    public async Task RegisterAndDeregister_CloudScheduleEndpoints_WorkForExternalTokens()
    {
        var classId = _factory.GetAddableClassId();
        var classToken = _factory.BuildClassToken(classId);

        var registerResponse = await _client.PostAsJsonAsync(
            "/api/students/student-123/schedule",
            new CloudScheduleMutationRequestDto { ClassId = classToken }
        );

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        var schedule = await registerResponse.Content.ReadFromJsonAsync<CloudStudentScheduleDto>();
        Assert.NotNull(schedule);
        Assert.Contains(schedule!.ScheduledClasses, x => x.ClassId == classToken);
        Assert.Contains(_factory.Notifications.Messages, x => x.Action == "enrolled" && x.ClassId == classToken);

        var removeResponse = await _client.DeleteAsync($"/api/students/student-123/schedule/{classToken}");
        Assert.Equal(HttpStatusCode.OK, removeResponse.StatusCode);
        var afterRemove = await removeResponse.Content.ReadFromJsonAsync<CloudStudentScheduleDto>();
        Assert.NotNull(afterRemove);
        Assert.DoesNotContain(afterRemove!.ScheduledClasses, x => x.ClassId == classToken);
        Assert.Contains(_factory.Notifications.Messages, x => x.Action == "dropped" && x.ClassId == classToken);
    }

    [Fact]
    public async Task SmartEnrollment_ReturnsCandidateSchedules_ForPromptDrivenRequest()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/students/student-123/smart-enrollment",
            new SmartEnrollmentRequestDto
            {
                Prompt = "I need CSCE331, prefer Friday off, and want to finish by 3pm.",
                CandidateLimit = 3
            }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<SmartEnrollmentResponseDto>();
        Assert.NotNull(payload);
        Assert.True(payload!.CatalogSize > 0);
        Assert.NotNull(payload.Preferences);
        Assert.Contains("CSCE331", payload.Preferences.RequiredCourseCodes);
        Assert.NotEmpty(payload.Candidates);
        Assert.All(
            payload.Candidates,
            candidate =>
            {
                Assert.False(string.IsNullOrWhiteSpace(candidate.Summary));
                Assert.NotEmpty(candidate.ScheduledClasses);
            }
        );
    }

    [Fact]
    public async Task RegisterClass_ReturnsWaitlistedSchedule_WhenClassIsAtCapacity()
    {
        int studentId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ClassFinderDbContext>();
            var student = new Student
            {
                FirstName = "Capacity",
                LastName = "Tester",
                Email = $"capacity.tester.{Guid.NewGuid():N}@email.com"
            };
            dbContext.Students.Add(student);
            dbContext.SaveChanges();
            studentId = student.Id;
        }

        var fullClassToken = _factory.BuildClassToken(_factory.GetFullClassId());
        var response = await _client.PostAsJsonAsync(
            $"/api/students/{studentId}/schedule",
            new CloudScheduleMutationRequestDto { ClassId = fullClassToken }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var schedule = await response.Content.ReadFromJsonAsync<CloudStudentScheduleDto>();
        Assert.NotNull(schedule);
        Assert.Empty(schedule!.ScheduledClasses);
        Assert.Contains(
            schedule.RegisteredClasses,
            item => item.ClassId == fullClassToken && item.EnrollmentStatus == "Waitlisted" && item.WaitlistPosition >= 1
        );
        Assert.Empty(_factory.Notifications.Messages);
    }

    [Fact]
    public async Task FinalizeSchedule_PersistsToDatabase_AndCanBeReloaded()
    {
        var classToken = _factory.BuildClassToken(_factory.GetAddableClassId());
        var finalizeRequest = new CloudFinalizeScheduleRequestDto
        {
            ScheduledClasses = new List<CloudFinalizeScheduleItemDto>
            {
                new() { ClassId = classToken }
            }
        };

        var finalizeResponse = await _client.PostAsJsonAsync(
            "/api/students/student-123/schedule/finalize",
            finalizeRequest
        );

        Assert.Equal(HttpStatusCode.OK, finalizeResponse.StatusCode);
        var finalized = await finalizeResponse.Content.ReadFromJsonAsync<CloudStudentScheduleDto>();
        Assert.NotNull(finalized);
        Assert.Single(finalized!.ScheduledClasses);
        Assert.Equal(classToken, finalized.ScheduledClasses[0].ClassId);

        var reloadedResponse = await _client.GetAsync("/api/students/student-123/schedule/state");
        Assert.Equal(HttpStatusCode.OK, reloadedResponse.StatusCode);
        var reloaded = await reloadedResponse.Content.ReadFromJsonAsync<CloudStudentScheduleDto>();
        Assert.NotNull(reloaded);
        Assert.Single(reloaded!.ScheduledClasses);
        Assert.Equal(classToken, reloaded.ScheduledClasses[0].ClassId);
    }

    [Fact]
    public async Task RegisterClass_Returns403_WhenPrerequisitesAreMissing()
    {
        int studentId;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ClassFinderDbContext>();
            var student = new Student
            {
                FirstName = "Prereq",
                LastName = "Tester",
                Email = $"prereq.tester.{Guid.NewGuid():N}@email.com",
                Password = "student123"
            };
            dbContext.Students.Add(student);
            dbContext.SaveChanges();
            studentId = student.Id;
        }

        var classToken = _factory.BuildClassToken(_factory.GetAddableClassId());
        var response = await _client.PostAsJsonAsync(
            $"/api/students/{studentId}/schedule",
            new CloudScheduleMutationRequestDto { ClassId = classToken }
        );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(payload);
        Assert.Contains("Missing prerequisites", payload!["message"]);
        Assert.Empty(_factory.Notifications.Messages);
    }

    [Fact]
    public async Task DeregisterClass_Returns403_WhenDropDeadlineHasPassed()
    {
        string classToken;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ClassFinderDbContext>();
            var courseClass = dbContext.CourseClasses.Single(item => item.CourseCode == "CSCE101");
            courseClass.DropDeadlineUtc = DateTimeOffset.UtcNow.AddDays(-1);
            dbContext.SaveChanges();
            classToken = _factory.BuildClassToken(courseClass.Id);
        }

        var response = await _client.DeleteAsync($"/api/students/student-123/schedule/{classToken}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(payload);
        Assert.Contains("drop deadline", payload!["message"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DeregisterClass_PromotesWaitlistedStudent_WhenSeatOpens()
    {
        var fullClassToken = _factory.BuildClassToken(_factory.GetFullClassId());
        int promotedStudentId;
        int enrolledStudentId;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ClassFinderDbContext>();
            promotedStudentId = dbContext.Students.Single(item => item.Email == "john.smith@email.com").Id;
            enrolledStudentId = dbContext.Students.Single(item => item.Email == "ava@email.com").Id;
        }

        var response = await _client.DeleteAsync($"/api/students/{enrolledStudentId}/schedule/{fullClassToken}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ClassFinderDbContext>();
            var promotedEnrollment = await dbContext.Enrollments.SingleAsync(
                item => item.StudentId == promotedStudentId && item.CourseClassId == _factory.GetFullClassId()
            );
            Assert.Equal(EnrollmentStatus.Enrolled, promotedEnrollment.Status);
            Assert.Null(promotedEnrollment.WaitlistPosition);
        }

        Assert.Contains(
            _factory.Notifications.Messages,
            item => item.Action == "enrolled" && item.ClassId == fullClassToken && item.StudentId == "student-123"
        );
    }

    [Fact]
    public async Task GetCloudClasses_FiltersByDepartment_AndMarksStudentEnrollmentState()
    {
        var response = await _client.GetAsync(
            "/api/classes?page=1&pageSize=10&department=Computer%20Science&studentId=student-123"
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<CloudClassPageDto>();
        Assert.NotNull(payload);
        Assert.NotEmpty(payload!.Classes);
        Assert.All(
            payload.Classes,
            item => Assert.Equal("Computer Science", item.Department)
        );
        Assert.Contains(payload.Classes, item => item.Id == "CSCE101-01" && item.IsStudentEnrolled);
        Assert.Contains(payload.Departments, item => item == "Computer Science");
    }

    [Fact]
    public async Task GetTeacherCatalog_ReturnsTeacherClasses_WithStudentEnrollmentState()
    {
        var response = await _client.GetAsync("/api/teachers?department=Computer%20Science&studentId=student-123");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<CloudTeacherCatalogPageDto>();
        Assert.NotNull(payload);
        Assert.NotEmpty(payload!.Teachers);
        Assert.All(
            payload.Teachers,
            teacher =>
            {
                Assert.Equal("Computer Science", teacher.Department);
                Assert.NotEmpty(teacher.Classes);
                Assert.All(teacher.Classes, item => Assert.Equal("Computer Science", item.Department));
            }
        );
        Assert.Contains(
            payload.Teachers.SelectMany(teacher => teacher.Classes),
            item => item.Id == "CSCE101-01" && item.IsStudentEnrolled
        );
    }

    [Fact]
    public async Task GetTeacherRoster_Returns403_WhenTeacherRequestsAnotherInstructorsClass()
    {
        var response = await _client.GetAsync("/api/teachers/teacher-1/classes/MATH200-03/roster");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.NotNull(payload);
        Assert.Contains("assigned to your instructor account", payload!["message"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TeacherLogin_AndClassUpdate_PersistsOwnedClassChanges()
    {
        var loginResponse = await _client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequestDto
            {
                Email = "brown@email.com",
                Password = "teacher123",
                Role = "teacher"
            }
        );

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<CloudAuthEnvelopeDto>();
        Assert.NotNull(loginPayload);
        Assert.Equal("teacher", loginPayload!.User.Role);

        var updateResponse = await _client.PutAsJsonAsync(
            $"/api/teachers/{loginPayload.User.UserId}/classes/MATH200-03",
            new TeacherClassUpdateRequestDto
            {
                Title = "Calculus II Demo Section",
                Location = "MATH 220",
                Capacity = 3,
                Days = ["Mon", "Wed", "Fri"],
                StartTime = "10:00",
                EndTime = "10:50"
            }
        );

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var payload = await updateResponse.Content.ReadFromJsonAsync<CloudClassDto>();
        Assert.NotNull(payload);
        Assert.Equal("Calculus II Demo Section", payload!.Title);
        Assert.Equal("MATH 220", payload.Location);
        Assert.Equal(3, payload.Capacity);
        Assert.Equal(["Mon", "Wed", "Fri"], payload.Days);
        Assert.Equal("10:00", payload.StartTime);
        Assert.Equal("10:50", payload.EndTime);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClassFinderDbContext>();
        var course = await dbContext.CourseClasses.SingleAsync(item => item.CourseCode == "MATH200");
        Assert.Equal("Calculus II Demo Section", course.ClassName);
        Assert.Equal("MATH 220", course.Location);
        Assert.Equal(3, course.Capacity);
        Assert.Equal("Mon,Wed,Fri", course.DaysOfWeek);
        Assert.Equal(new TimeOnly(10, 0), course.StartTime);
        Assert.Equal(new TimeOnly(10, 50), course.EndTime);
    }

    [Fact]
    public async Task StorageFeedArrival_ImportsStudentsClassesAndEnrollmentUpdates()
    {
        var feedDirectory = _factory.GetFeedDirectory();
        var feedPath = Path.Combine(feedDirectory, $"feed-{Guid.NewGuid():N}.json");
        var feed = new StorageFeedEnvelopeDto
        {
            Students =
            [
                new StorageFeedStudentDto
                {
                    FirstName = "Maya",
                    LastName = "Lane",
                    Email = "maya.lane@email.com"
                }
            ],
            Instructors =
            [
                new StorageFeedInstructorDto
                {
                    FirstName = "Priya",
                    LastName = "Shah",
                    Email = "priya.shah@email.com"
                }
            ],
            Classes =
            [
                new StorageFeedClassDto
                {
                    CourseCode = "CSCE590",
                    Title = "Cloud Systems",
                    InstructorEmail = "priya.shah@email.com",
                    InstructorFirstName = "Priya",
                    InstructorLastName = "Shah",
                    Location = "ONLINE",
                    Credits = 3,
                    Capacity = 25,
                    DaysOfWeek = "Tue,Thu",
                    StartTime = "17:30",
                    EndTime = "18:45"
                }
            ]
        };

        await File.WriteAllTextAsync(feedPath, System.Text.Json.JsonSerializer.Serialize(feed));

        var importedClass = await WaitForAsync(
            async () =>
            {
                using var scope = _factory.Services.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ClassFinderDbContext>();
                return await dbContext.CourseClasses
                    .Include(item => item.Instructor)
                    .SingleOrDefaultAsync(item => item.CourseCode == "CSCE590");
            }
        );

        Assert.NotNull(importedClass);
        Assert.Equal("Cloud Systems", importedClass!.ClassName);
        Assert.Equal("priya.shah@email.com", importedClass.Instructor!.Email);

        var enrollmentFeed = new StorageFeedEnvelopeDto
        {
            Enrollments =
            [
                new StorageFeedEnrollmentDto
                {
                    StudentEmail = "maya.lane@email.com",
                    SectionId = importedClass.Id,
                    Status = "Enrolled"
                }
            ]
        };

        await File.WriteAllTextAsync(
            Path.Combine(feedDirectory, $"enrollment-{Guid.NewGuid():N}.json"),
            System.Text.Json.JsonSerializer.Serialize(enrollmentFeed)
        );

        var enrollment = await WaitForAsync(
            async () =>
            {
                using var scope = _factory.Services.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ClassFinderDbContext>();
                var student = await dbContext.Students.SingleOrDefaultAsync(item => item.Email == "maya.lane@email.com");
                if (student is null)
                {
                    return null;
                }

                return await dbContext.Enrollments.SingleOrDefaultAsync(
                    item => item.StudentId == student.Id && item.CourseClassId == importedClass.Id
                );
            }
        );

        Assert.NotNull(enrollment);
        Assert.Equal(EnrollmentStatus.Enrolled, enrollment!.Status);
    }

    private static async Task<T?> WaitForAsync<T>(Func<Task<T?>> action)
        where T : class
    {
        for (var attempt = 0; attempt < 20; attempt += 1)
        {
            var result = await action();
            if (result is not null)
            {
                return result;
            }

            await Task.Delay(200);
        }

        return await action();
    }
}
