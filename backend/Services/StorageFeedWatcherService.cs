using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace ClassFinder.Api.Services;

public class StorageFeedWatcherService(
    IServiceScopeFactory serviceScopeFactory,
    IOptions<FeedIngestionOptions> options,
    IHostEnvironment hostEnvironment,
    ILogger<StorageFeedWatcherService> logger
) : BackgroundService
{
    private readonly Channel<string> _queue = Channel.CreateUnbounded<string>();
    private readonly ConcurrentDictionary<string, byte> _queuedPaths = new(StringComparer.OrdinalIgnoreCase);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Storage feed watcher is disabled.");
            return;
        }

        var watchPath = ResolvePath(options.Value.WatchPath);
        Directory.CreateDirectory(watchPath);
        Directory.CreateDirectory(ResolvePath(options.Value.ProcessedPath));
        Directory.CreateDirectory(ResolvePath(options.Value.FailedPath));

        using var watcher = new FileSystemWatcher(watchPath, "*.json")
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime
        };

        watcher.Created += (_, eventArgs) => Enqueue(eventArgs.FullPath);
        watcher.Changed += (_, eventArgs) => Enqueue(eventArgs.FullPath);
        watcher.Renamed += (_, eventArgs) => Enqueue(eventArgs.FullPath);
        watcher.EnableRaisingEvents = true;

        foreach (var existingFile in Directory.GetFiles(watchPath, "*.json"))
        {
            Enqueue(existingFile);
        }

        await foreach (var path in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var importer = scope.ServiceProvider.GetRequiredService<IStorageFeedImportService>();
                await importer.ProcessFileAsync(path, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Storage feed processing failed for {Path}.", path);
            }
            finally
            {
                _queuedPaths.TryRemove(path, out _);
            }
        }
    }

    private void Enqueue(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (_queuedPaths.TryAdd(path, 0))
        {
            _queue.Writer.TryWrite(path);
        }
    }

    private string ResolvePath(string configuredPath)
    {
        return Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(hostEnvironment.ContentRootPath, configuredPath);
    }
}
