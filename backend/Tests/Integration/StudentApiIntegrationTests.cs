using System.Net;
using System.Net.Http.Json;
using ClassFinder.Api.Data;
using ClassFinder.Api.DTOs;
using ClassFinder.Api.Models;
using Microsoft.Extensions.DependencyInjection;
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

        var removeResponse = await _client.DeleteAsync($"/api/students/student-123/schedule/{classToken}");
        Assert.Equal(HttpStatusCode.OK, removeResponse.StatusCode);
        var afterRemove = await removeResponse.Content.ReadFromJsonAsync<CloudStudentScheduleDto>();
        Assert.NotNull(afterRemove);
        Assert.DoesNotContain(afterRemove!.ScheduledClasses, x => x.ClassId == classToken);
    }

    [Fact]
    public async Task RegisterClass_Returns423_WhenClassIsAtCapacity()
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

        Assert.Equal((HttpStatusCode)423, response.StatusCode);
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
}
