namespace ClassFinder.Api.Services;

public interface IFeedIngestionService
{
    Task PollAndProcessAsync(CancellationToken cancellationToken = default);
}