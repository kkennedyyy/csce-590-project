using ClassFinder.Api.Data;
using ClassFinder.Api.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace ClassFinder.Api.Tests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private static readonly InMemoryDatabaseRoot DbRoot = new();
    private const string DatabaseName = "ClassFinderTests";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
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

                using var scope = services.BuildServiceProvider().CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ClassFinderDbContext>();
                dbContext.Database.EnsureCreated();
                TestDataSeeder.Seed(dbContext);
            }
        );
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
}
