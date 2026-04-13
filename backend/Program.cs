using ClassFinder.Api.Data;
using ClassFinder.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://localhost:8080");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

builder.Services.AddDbContext<ClassFinderDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<NotificationOptions>(
    builder.Configuration.GetSection(NotificationOptions.SectionName)
);
builder.Services.Configure<FeedIngestionOptions>(
    builder.Configuration.GetSection(FeedIngestionOptions.SectionName)
);

builder.Services.AddScoped<IStudentDashboardService, StudentDashboardService>();
builder.Services.AddScoped<IClassService, ClassService>();
builder.Services.AddScoped<IRegistrationService, RegistrationService>();
builder.Services.AddScoped<IEnrollmentNotificationService, EnrollmentNotificationService>();
builder.Services.AddScoped<IStorageFeedImportService, StorageFeedImportService>();
builder.Services.AddHostedService<StorageFeedWatcherService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();
var seedOnStartup = builder.Configuration.GetValue<bool>("SeedDataOnStartup");

if (args.Contains("--seed", StringComparer.OrdinalIgnoreCase))
{
    await InitializeDatabaseAsync(app.Services, true);
    Console.WriteLine("Database initialized and seeded.");
    return;
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

await InitializeDatabaseAsync(app.Services, seedOnStartup);

app.UseCors("FrontendPolicy");
app.UseAuthorization();
app.MapControllers();

app.Run();

static async Task InitializeDatabaseAsync(IServiceProvider services, bool seedData)
{
    const int maxAttempts = 12;
    var delay = TimeSpan.FromSeconds(3);

    for (var attempt = 1; attempt <= maxAttempts; attempt += 1)
    {
        try
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ClassFinderDbContext>();

            if (db.Database.IsRelational())
            {
                await db.Database.MigrateAsync();
            }
            else
            {
                await db.Database.EnsureCreatedAsync();
            }

            if (seedData)
            {
                await SeedData.InitializeAsync(db);
                Console.WriteLine("SeedDataOnStartup enabled; seed completed.");
            }

            return;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            Console.WriteLine(
                $"Database initialization attempt {attempt}/{maxAttempts} failed: {ex.Message}. Retrying in {delay.TotalSeconds:0}s..."
            );
            await Task.Delay(delay);
        }
    }

    using var finalScope = services.CreateScope();
    var finalDb = finalScope.ServiceProvider.GetRequiredService<ClassFinderDbContext>();
    if (finalDb.Database.IsRelational())
    {
        await finalDb.Database.MigrateAsync();
    }
    else
    {
        await finalDb.Database.EnsureCreatedAsync();
    }

    if (seedData)
    {
        await SeedData.InitializeAsync(finalDb);
        Console.WriteLine("SeedDataOnStartup enabled; seed completed.");
    }
}

public partial class Program;
