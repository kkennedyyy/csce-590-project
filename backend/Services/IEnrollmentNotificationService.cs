namespace ClassFinder.Api.Services;

public sealed record EnrollmentNotificationMessage(
    string RecipientName,
    string RecipientEmail,
    string Action,
    string StudentId,
    string ClassId,
    string ClassTitle,
    string Department,
    string Instructor,
    string Location,
    string ScheduleSummary,
    int Credits,
    int AvailableSeats,
    DateTimeOffset OccurredAtUtc
);

public interface IEnrollmentNotificationService
{
    Task SendEnrollmentReceiptAsync(
        EnrollmentNotificationMessage message,
        CancellationToken cancellationToken = default
    );

    Task SendDirectEmailAsync(
        EnrollmentNotificationMessage message,
        CancellationToken cancellationToken = default
    );
}
