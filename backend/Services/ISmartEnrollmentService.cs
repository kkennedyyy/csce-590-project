using ClassFinder.Api.DTOs;

namespace ClassFinder.Api.Services;

public interface ISmartEnrollmentService
{
    Task<(SmartEnrollmentResponseDto? Response, RegistrationError? Error)> GenerateAsync(
        string studentToken,
        SmartEnrollmentRequestDto request,
        CancellationToken cancellationToken = default
    );
}
