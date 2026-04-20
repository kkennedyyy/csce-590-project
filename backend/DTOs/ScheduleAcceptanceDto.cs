namespace ClassFinder.Api.DTOs;

public class ScheduleAcceptanceDto
{
    public int StudentId { get; set; }
    public List<int> ClassIds { get; set; } = [];
}