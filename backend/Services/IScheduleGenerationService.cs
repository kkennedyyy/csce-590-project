using ClassFinder.Api.DTOs;

namespace ClassFinder.Api.Services;

public interface IScheduleGenerationService
{
    Task<List<GeneratedScheduleDto>> GenerateSchedulesAsync(
        ScheduleRequestDto request, 
        CancellationToken cancellationToken);
}