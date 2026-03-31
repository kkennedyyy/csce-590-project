namespace ClassFinder.Api.Models;

public record UserFeedItem(
    string Email,
    string FirstName,
    string LastName
);

public record ClassFeedItem(
    string CourseCode,
    string ClassName,
    string InstructorEmail,
    string InstructorFirstName,
    string InstructorLastName,
    string Location,
    int Credits,
    int Capacity,
    string DaysOfWeek,
    string StartTime, // "09:00"
    string EndTime    // "10:15"
);

public record EnrollmentFeedItem(
    string StudentEmail,
    string CourseCode,
    string Status,            // "Enrolled" or "Waitlisted"
    int? WaitlistPosition
);