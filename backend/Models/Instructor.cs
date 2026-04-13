namespace ClassFinder.Api.Models;

public class Instructor
{
    public int Id { get; set; }
    public string? ExternalId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public ICollection<CourseClass> Classes { get; set; } = [];
}
