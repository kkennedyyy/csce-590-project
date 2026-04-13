using ClassFinder.Api.Data;
using ClassFinder.Api.Models;
using ClassFinder.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ClassFinder.Api.Tests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private static readonly InMemoryDatabaseRoot DbRoot = new();
    private const string DatabaseName = "ClassFinderTests";
    private readonly string _feedDirectory = Path.Combine(
        Path.GetTempPath(),
        "classfinder-feed-tests",
        Guid.NewGuid().ToString("N")
    );

    public RecordingEnrollmentNotificationService Notifications { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration(
            (_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        [$"{FeedIngestionOptions.SectionName}:Enabled"] = "true",
                        [$"{FeedIngestionOptions.SectionName}:WatchPath"] = _feedDirectory,
                        [$"{FeedIngestionOptions.SectionName}:ProcessedPath"] = Path.Combine(
                            _feedDirectory,
                            "processed"
                        ),
                        [$"{FeedIngestionOptions.SectionName}:FailedPath"] = Path.Combine(_feedDirectory, "failed")
                    }
                );
            }
        );
        builder.ConfigureServices(
            services =>
            {
                var dbContextDescriptor = services.SingleOrDefault(
                    service => service.ServiceType == typeof(DbContextOptions<ClassFinderDbContext>)
                );

                if (dbContextDescriptor is not null)
                {
                    services.Remove(dbContextDescriptor);
                }

                services.AddDbContext<ClassFinderDbContext>(options =>
                    options.UseInMemoryDatabase(DatabaseName, DbRoot));
                services.RemoveAll<IEnrollmentNotificationService>();
                services.AddSingleton(Notifications);
                services.AddSingleton<IEnrollmentNotificationService>(sp =>
                    sp.GetRequiredService<RecordingEnrollmentNotificationService>());

                using var scope = services.BuildServiceProvider().CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ClassFinderDbContext>();
                dbContext.Database.EnsureCreated();
                TestDataSeeder.Seed(dbContext);
            }
        );
    }

    public string GetFeedDirectory()
    {
        Directory.CreateDirectory(_feedDirectory);
        return _feedDirectory;
    }

    public void ClearNotifications()
    {
        Notifications.Clear();
    }

    public void ResetState()
    {
        ClearNotifications();

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClassFinderDbContext>();
        dbContext.Database.EnsureDeleted();
        dbContext.Database.EnsureCreated();
        TestDataSeeder.Seed(dbContext);
    }

    public int GetSampleStudentId()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClassFinderDbContext>();
        return dbContext.Students.Single(x => x.Email == "john.smith@email.com").Id;
    }

    public int GetFullClassId()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClassFinderDbContext>();
        return dbContext.CourseClasses.Single(x => x.CourseCode == "MATH200").Id;
    }

    public int GetOpenClassId()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClassFinderDbContext>();
        return dbContext.CourseClasses.Single(x => x.CourseCode == "CSCE101").Id;
    }

    public int GetAddableClassId()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClassFinderDbContext>();
        return dbContext.CourseClasses.Single(x => x.CourseCode == "CSCE331").Id;
    }

    public string BuildClassToken(int classId)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ClassFinderDbContext>();
        var classInfo = dbContext.CourseClasses.Single(x => x.Id == classId);
        return $"{classInfo.CourseCode}-{classInfo.Id:00}";
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        try
        {
            if (Directory.Exists(_feedDirectory))
            {
                Directory.Delete(_feedDirectory, recursive: true);
            }
        }
        catch
        {
        }
    }
}
