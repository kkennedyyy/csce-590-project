using ClassFinder.Api.DTOs;

namespace ClassFinder.Api.Services;

public interface IStorageFeedImportService
{
    Task ImportAsync(StorageFeedEnvelopeDto feed, CancellationToken cancellationToken = default);

    Task ProcessFileAsync(string path, CancellationToken cancellationToken = default);
}
