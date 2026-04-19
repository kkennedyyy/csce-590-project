using ClassFinder.Api.Data;
using ClassFinder.Api.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

var connectionString = builder.Configuration["ConnectionStrings:DefaultConnection"]
    ?? builder.Configuration["DefaultConnection"];

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:DefaultConnection must be configured for feed ingestion.");
}

builder.Services.AddDbContext<ClassFinderDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.Configure<FeedIngestionOptions>(
    builder.Configuration.GetSection(FeedIngestionOptions.SectionName)
);
builder.Services.AddScoped<IStorageFeedImportService, StorageFeedImportService>();

builder.Build().Run();
