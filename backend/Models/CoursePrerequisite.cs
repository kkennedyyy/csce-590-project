namespace ClassFinder.Api.Models;

public class CoursePrerequisite
{
    public int Id { get; set; }
    public int CourseClassId { get; set; }
    public CourseClass? CourseClass { get; set; }
    public string RequiredCourseCode { get; set; } = string.Empty;
}
