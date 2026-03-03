using System.Net;
using System.Net.Http.Json;
using ClassFinder.Api.DTOs;
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
}
