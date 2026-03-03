using ClassFinder.Api.Data;
using ClassFinder.Api.Services;
using ClassFinder.Api.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ClassFinder.Api.Tests.Unit;

public class StudentDashboardServiceTests
{
    [Fact]
    public async Task GetStudentClassesAsync_ReturnsWaitlistAndInstructorData()
    {
        await using var dbContext = BuildContext();
        TestDataSeeder.Seed(dbContext);
        var studentId = dbContext.Students.Single(x => x.Email == "john.smith@email.com").Id;

        var service = new StudentDashboardService(dbContext);

        var classes = await service.GetStudentClassesAsync(studentId);

        Assert.NotEmpty(classes);
        Assert.Contains(classes, x => x.IsWaitlisted && x.WaitlistPosition == 1);
        Assert.All(classes, x => Assert.False(string.IsNullOrWhiteSpace(x.InstructorName)));
    }

    [Fact]
    public async Task GetStudentScheduleAsync_ExpandsMultiDayClassesIntoSeparateEvents()
    {
        await using var dbContext = BuildContext();
        TestDataSeeder.Seed(dbContext);
        var studentId = dbContext.Students.Single(x => x.Email == "john.smith@email.com").Id;

        var service = new StudentDashboardService(dbContext);

        var events = await service.GetStudentScheduleAsync(studentId);

        Assert.Contains(events, x => x.CourseCode == "CSCE101" && x.DayOfWeek == "Mon");
        Assert.Contains(events, x => x.CourseCode == "CSCE101" && x.DayOfWeek == "Wed");
    }

    private static ClassFinderDbContext BuildContext()
    {
        var options = new DbContextOptionsBuilder<ClassFinderDbContext>()
            .UseInMemoryDatabase(databaseName: $"UnitTests-{Guid.NewGuid()}")
            .Options;
        return new ClassFinderDbContext(options);
    }
}
