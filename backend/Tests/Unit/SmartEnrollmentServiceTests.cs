using System.Net;
using System.Text;
using ClassFinder.Api.Data;
using ClassFinder.Api.DTOs;
using ClassFinder.Api.Services;
using ClassFinder.Api.Tests.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace ClassFinder.Api.Tests.Unit;

public class SmartEnrollmentServiceTests
{
    [Fact]
    public async Task GenerateAsync_UsesLlm_WhenOptionalFieldsReturnNull()
    {
        var dbOptions = new DbContextOptionsBuilder<ClassFinderDbContext>()
            .UseInMemoryDatabase($"SmartEnrollmentServiceTests-{Guid.NewGuid():N}")
            .Options;

        await using var dbContext = new ClassFinderDbContext(dbOptions);
        await dbContext.Database.EnsureCreatedAsync();
        TestDataSeeder.Seed(dbContext);

        var llmResponse = """
            {
              "choices": [
                {
                  "message": {
                    "content": "{\n  \"summary\": \"Parsed by LLM\",\n  \"requiredCourseCodes\": [\"CSCE331\"],\n  \"preferredElectiveCourseCodes\": [],\n  \"requiredKeywords\": [],\n  \"preferredKeywords\": [\"software\"],\n  \"electiveSlots\": 1,\n  \"earliestStart\": \"10:00\",\n  \"latestEnd\": null,\n  \"blockedDays\": [],\n  \"preferredNoClassDay\": null,\n  \"minimumBreakMinutes\": null\n}"
                  }
                }
              ]
            }
            """;

        var httpClient = new HttpClient(
            new StubHttpMessageHandler(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(llmResponse, Encoding.UTF8, "application/json")
                }
            )
        );
        var service = new SmartEnrollmentService(
            dbContext,
            new StubHttpClientFactory(httpClient),
            Options.Create(
                new SmartEnrollmentOptions
                {
                    LlmEndpoint = "https://example.openai.azure.com/",
                    LlmApiKey = "test-key",
                    LlmDeployment = "smart-enrollment-llm",
                    LlmApiVersion = "2024-10-21"
                }
            )
        );

        var (response, error) = await service.GenerateAsync(
            "student-123",
            new SmartEnrollmentRequestDto
            {
                Prompt = "I need software engineering after 10am.",
                CandidateLimit = 2
            }
        );

        Assert.Null(error);
        Assert.NotNull(response);
        Assert.True(response!.UsedLlm);
        Assert.Equal("LLM + Rules", response.PlannerMode);
        Assert.Equal("Parsed by LLM", response.Preferences.Summary);
        Assert.Contains("CSCE331", response.Preferences.RequiredCourseCodes);
        Assert.Equal("10:00", response.Preferences.EarliestStart);
        Assert.Equal("18:30", response.Preferences.LatestEnd);
        Assert.Equal(15, response.Preferences.MinimumBreakMinutes);
        Assert.NotEmpty(response.Candidates);
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(response);
    }
}
