using System.Collections.Concurrent;
using ClassFinder.Api.Services;

namespace ClassFinder.Api.Tests.Integration;

public class RecordingEnrollmentNotificationService : IEnrollmentNotificationService
{
    private readonly ConcurrentQueue<EnrollmentNotificationMessage> _messages = new();

    public IReadOnlyCollection<EnrollmentNotificationMessage> Messages => _messages.ToArray();

    public Task SendEnrollmentReceiptAsync(
        EnrollmentNotificationMessage message,
        CancellationToken cancellationToken = default
    )
    {
        _messages.Enqueue(message);
        return Task.CompletedTask;
    }

    public Task SendDirectEmailAsync(
        EnrollmentNotificationMessage message,
        CancellationToken cancellationToken = default
    )
    {
        _messages.Enqueue(message);
        return Task.CompletedTask;
    }

    public void Clear()
    {
        while (_messages.TryDequeue(out _))
        {
        }
    }
}
