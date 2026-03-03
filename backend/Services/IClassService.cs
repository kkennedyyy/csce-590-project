using ClassFinder.Api.DTOs;

namespace ClassFinder.Api.Services;

public interface IClassService
{
    Task<ClassDetailDto?> GetClassDetailsAsync(int classId, CancellationToken cancellationToken = default);
}
