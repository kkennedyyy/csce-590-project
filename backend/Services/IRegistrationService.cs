using ClassFinder.Api.DTOs;

namespace ClassFinder.Api.Services;

public interface IRegistrationService
{
    Task<CloudAuthEnvelopeDto?> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);

    Task<CloudClassPageDto> GetClassesAsync(
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken = default
    );

    Task<CloudClassDto?> GetClassByTokenAsync(string classToken, CancellationToken cancellationToken = default);

    Task<CloudStudentScheduleDto?> GetStudentScheduleStateAsync(
        string studentToken,
        CancellationToken cancellationToken = default
    );

    Task<(CloudStudentScheduleDto? Schedule, RegistrationError? Error)> RegisterClassAsync(
        string studentToken,
        CloudScheduleMutationRequestDto request,
        CancellationToken cancellationToken = default
    );

    Task<(CloudStudentScheduleDto? Schedule, RegistrationError? Error)> DeregisterClassAsync(
        string studentToken,
        string classOrSectionToken,
        CancellationToken cancellationToken = default
    );

    Task<(CloudStudentScheduleDto? Schedule, RegistrationError? Error)> FinalizeScheduleAsync(
        string studentToken,
        CloudFinalizeScheduleRequestDto request,
        CancellationToken cancellationToken = default
    );

    Task<IReadOnlyList<CloudClassDto>?> GetTeacherClassesAsync(
        string teacherToken,
        CancellationToken cancellationToken = default
    );

    Task<CloudTeacherRosterDto?> GetTeacherRosterAsync(
        string teacherToken,
        string classToken,
        CancellationToken cancellationToken = default
    );

    Task<(CloudClassDto? ClassInfo, RegistrationError? Error)> UpdateTeacherCapacityAsync(
        string teacherToken,
        string classToken,
        int capacity,
        CancellationToken cancellationToken = default
    );

    Task<RegistrationError?> RemoveStudentFromClassAsync(
        string teacherToken,
        string classToken,
        string studentToken,
        CancellationToken cancellationToken = default
    );
}
