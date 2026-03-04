namespace ClassFinder.Api.Services;

public interface IEnrollmentService
{
    Task<(bool Success, string Message)> EnrollAsync(int studentId, int classId, CancellationToken cancellationToken);
    Task<(bool Success, string Message)> DropAsync(int studentId, int classId, CancellationToken cancellationToken);
}