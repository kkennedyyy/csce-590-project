using ClassFinder.Api.DTOs;

namespace ClassFinder.Api.Services;

public interface IStudentDashboardService
{
    Task<bool> StudentExistsAsync(int studentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StudentClassDto>> GetStudentClassesAsync(
        int studentId,
        CancellationToken cancellationToken = default
    );
    Task<IReadOnlyList<ScheduleEventDto>> GetStudentScheduleAsync(
        int studentId,
        CancellationToken cancellationToken = default
    );
}
