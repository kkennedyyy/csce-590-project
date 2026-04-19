using System.Text.Json;
using ClassFinder.Api.DTOs;
using ClassFinder.Api.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace csce_590_project.Functions;

public sealed class FeedBlobIngestionFunction(
    IStorageFeedImportService storageFeedImportService,
    ILogger<FeedBlobIngestionFunction> logger)
{
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true
    };

    [Function(nameof(FeedBlobIngestionFunction))]
    public async Task RunAsync(
        [BlobTrigger("%FeedIngestion__ContainerName%/{name}", Connection = "FeedIngestionStorageConnection")]
        Stream blobStream,
        string name,
        CancellationToken cancellationToken)
    {
        try
        {
            var payload = await JsonSerializer.DeserializeAsync<StorageFeedEnvelopeDto>(
                blobStream,
                _serializerOptions,
                cancellationToken
            ) ?? new StorageFeedEnvelopeDto();

            await storageFeedImportService.ImportAsync(payload, cancellationToken);
            logger.LogInformation("Imported feed blob {BlobName} successfully.", name);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Feed blob {BlobName} is invalid JSON.", name);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Feed blob {BlobName} failed to import.", name);
            throw;
        }
    }
}
