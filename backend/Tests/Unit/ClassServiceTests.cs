using ClassFinder.Api.Data;
using ClassFinder.Api.Services;
using ClassFinder.Api.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ClassFinder.Api.Tests.Unit;

public class ClassServiceTests
{
    [Fact]
    public async Task GetClassDetailsAsync_ReturnsClassAsAtCapacity_WhenEnrolledEqualsCapacity()
    {
        await using var dbContext = BuildContext();
        TestDataSeeder.Seed(dbContext);
        var fullClassId = dbContext.CourseClasses.Single(x => x.CourseCode == "MATH200").Id;
        var service = new ClassService(dbContext);

        var classDetails = await service.GetClassDetailsAsync(fullClassId);

        Assert.NotNull(classDetails);
        Assert.True(classDetails!.IsAtCapacity);
        Assert.True(classDetails.Capacity >= classDetails.EnrolledCount);
        Assert.True(classDetails.WaitlistCount >= 1);
    }

    private static ClassFinderDbContext BuildContext()
    {
        var options = new DbContextOptionsBuilder<ClassFinderDbContext>()
            .UseInMemoryDatabase(databaseName: $"UnitTests-{Guid.NewGuid()}")
            .Options;
        return new ClassFinderDbContext(options);
    }
}
