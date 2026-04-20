CREATE OR ALTER PROCEDURE dbo.usp_ClassFinder_ApplyExternalSync
    @PipelineRunId NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    -- ──────────────────────────────────────────────────────────────────────
    -- 1. Merge Students
    --    Match key: ExternalId (the UUID from the students.csv student_id column)
    -- ──────────────────────────────────────────────────────────────────────
    MERGE dbo.Students AS target
    USING dbo.StageClassFinderStudents AS source
        ON target.ExternalId = source.ExternalStudentId
    WHEN MATCHED THEN
        UPDATE SET
            target.FirstName       = source.FirstName,
            target.LastName        = source.LastName,
            target.Email           = source.Email,
            target.Password        = source.Password,
            target.Major           = source.Major,
            target.Classification  = source.Classification,
            target.UpdatedAtUtc    = SYSDATETIMEOFFSET()
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (ExternalId, FirstName, LastName, Email, Password, Major, Classification, CreatedAtUtc, UpdatedAtUtc)
        VALUES (
            source.ExternalStudentId,
            source.FirstName,
            source.LastName,
            source.Email,
            source.Password,
            source.Major,
            source.Classification,
            SYSDATETIMEOFFSET(),
            SYSDATETIMEOFFSET()
        );

    -- ──────────────────────────────────────────────────────────────────────
    -- 2. Merge Instructors
    --    Match key: ExternalId (the UUID from professors.json professor_id)
    -- ──────────────────────────────────────────────────────────────────────
    MERGE dbo.Instructors AS target
    USING dbo.StageClassFinderProfessors AS source
        ON target.ExternalId = source.ExternalProfessorId
    WHEN MATCHED THEN
        UPDATE SET
            target.FirstName  = source.FirstName,
            target.LastName   = source.LastName,
            target.Email      = source.Email,
            target.Password   = source.Password
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (ExternalId, FirstName, LastName, Email, Password)
        VALUES (
            source.ExternalProfessorId,
            source.FirstName,
            source.LastName,
            source.Email,
            source.Password
        );

    -- ──────────────────────────────────────────────────────────────────────
    -- 3. Merge CourseClasses
    --    Match key: ExternalId (the UUID 'id' field from /api/classes)
    --    CourseCode is built as DepartmentCode + CourseNumber (e.g. "ARTS101")
    --    InstructorId resolved by joining Instructors on ExternalId
    -- ──────────────────────────────────────────────────────────────────────
    MERGE dbo.CourseClasses AS target
    USING (
        SELECT
            sc.ExternalClassId,
            COALESCE(
                NULLIF(LTRIM(RTRIM(sc.DepartmentCode + CAST(sc.CourseNumber AS NVARCHAR(10)))), ''),
                NULLIF(LTRIM(RTRIM(sc.DepartmentCode + sc.CourseCode)), ''),
                NULLIF(LTRIM(RTRIM(sc.CourseCode)), ''),
                NULLIF(LTRIM(RTRIM(LEFT(sc.ExternalClassId, 40))), '')
            ) AS CourseCode,
            sc.ClassName,
            sc.Semester,
            sc.Location,
            sc.Credits,
            sc.MaxSeats                                                AS Capacity,
            sc.DaysOfWeekCompact                                       AS DaysOfWeek,
            sc.StartTime,
            sc.EndTime,
            i.Id                                                       AS InstructorId
        FROM dbo.StageClassFinderClasses sc
        LEFT JOIN dbo.Instructors i ON i.ExternalId = sc.ExternalProfessorId
    ) AS source
        ON target.ExternalId = source.ExternalClassId
    WHEN MATCHED THEN
        UPDATE SET
            target.CourseCode     = COALESCE(source.CourseCode, target.CourseCode),
            target.ClassName      = source.ClassName,
            target.Semester       = ISNULL(source.Semester, target.Semester),
            target.Location       = source.Location,
            target.Credits        = source.Credits,
            target.Capacity       = source.Capacity,
            target.DaysOfWeek     = source.DaysOfWeek,
            target.StartTime      = CAST(source.StartTime AS TIME),
            target.EndTime        = CAST(source.EndTime   AS TIME),
            target.InstructorId   = ISNULL(source.InstructorId, target.InstructorId),
            target.UpdatedAtUtc   = SYSDATETIMEOFFSET()
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (ExternalId, CourseCode, ClassName, Semester, Location, Credits, Capacity, DaysOfWeek, StartTime, EndTime, InstructorId, CreatedAtUtc, UpdatedAtUtc)
        VALUES (
            source.ExternalClassId,
            COALESCE(source.CourseCode, LEFT(source.ExternalClassId, 40)),
            source.ClassName,
            COALESCE(source.Semester, 'Current'),
            source.Location,
            source.Credits,
            source.Capacity,
            source.DaysOfWeek,
            CAST(source.StartTime AS TIME),
            CAST(source.EndTime   AS TIME),
            ISNULL(source.InstructorId, 1),   -- fallback to ID 1 if professor not matched
            SYSDATETIMEOFFSET(),
            SYSDATETIMEOFFSET()
        );

    -- ──────────────────────────────────────────────────────────────────────
    -- 4. Merge Enrollments (from /api/enrollments)
    --    Status values from the API: "enrolled" / "waitlisted" (lowercase)
    --    Mapped to enum: 1=Enrolled, 2=Waitlisted
    -- ──────────────────────────────────────────────────────────────────────
    MERGE dbo.Enrollments AS target
    USING (
        SELECT
            x.ExternalEnrollmentId,
            x.StudentId,
            x.CourseClassId,
            x.Status
        FROM (
            SELECT
                se.ExternalEnrollmentId,
                s.Id AS StudentId,
                c.Id AS CourseClassId,
                CASE LOWER(se.Status)
                    WHEN 'enrolled'   THEN 1
                    WHEN 'waitlisted' THEN 2
                    ELSE 1
                END AS Status,
                ROW_NUMBER() OVER (
                    PARTITION BY s.Id, c.Id
                    ORDER BY se.EnrollmentDateUtc DESC, se.ExternalEnrollmentId
                ) AS rn
            FROM dbo.StageClassFinderEnrollments se
            JOIN dbo.Students s ON s.ExternalId = se.ExternalStudentId
            JOIN dbo.CourseClasses c ON c.ExternalId = se.ExternalClassId
        ) x
        WHERE x.rn = 1
    ) AS source
        ON target.StudentId = source.StudentId
       AND target.CourseClassId = source.CourseClassId
    WHEN MATCHED THEN
        UPDATE SET
            target.ExternalId = COALESCE(source.ExternalEnrollmentId, target.ExternalId),
            target.Status = source.Status,
            target.WaitlistPosition = CASE WHEN source.Status = 1 THEN NULL ELSE target.WaitlistPosition END
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (ExternalId, StudentId, CourseClassId, Status)
        VALUES (source.ExternalEnrollmentId, source.StudentId, source.CourseClassId, source.Status);

    -- ──────────────────────────────────────────────────────────────────────
    -- 5. Merge Waitlist entries (from /api/waitlist) into Enrollments
    --    These always have Status = 2 (Waitlisted) and a Position
    -- ──────────────────────────────────────────────────────────────────────
    MERGE dbo.Enrollments AS target
    USING (
        SELECT
            x.ExternalWaitlistId,
            x.StudentId,
            x.CourseClassId,
            x.Position
        FROM (
            SELECT
                w.ExternalWaitlistId,
                s.Id AS StudentId,
                c.Id AS CourseClassId,
                w.Position,
                ROW_NUMBER() OVER (
                    PARTITION BY s.Id, c.Id
                    ORDER BY w.SignupDateUtc DESC, w.ExternalWaitlistId
                ) AS rn
            FROM dbo.StageClassFinderWaitlist w
            JOIN dbo.Students s ON s.ExternalId = w.ExternalStudentId
            JOIN dbo.CourseClasses c ON c.ExternalId = w.ExternalClassId
        ) x
        WHERE x.rn = 1
    ) AS source
        ON target.StudentId = source.StudentId
       AND target.CourseClassId = source.CourseClassId
    WHEN MATCHED THEN
        UPDATE SET
            target.ExternalId = COALESCE(source.ExternalWaitlistId, target.ExternalId),
            target.Status = 2,
            target.WaitlistPosition = source.Position
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (ExternalId, StudentId, CourseClassId, Status, WaitlistPosition)
        VALUES (source.ExternalWaitlistId, source.StudentId, source.CourseClassId, 2, source.Position);

    -- ──────────────────────────────────────────────────────────────────────
    -- 6. Mark sync complete in the log
    -- ──────────────────────────────────────────────────────────────────────
    UPDATE dbo.ExternalSyncLog
    SET
        CompletedAtUtc = SYSDATETIMEOFFSET(),
        Summary        = CONCAT(Summary, ' | Completed at ', CAST(SYSDATETIMEOFFSET() AS NVARCHAR(50)))
    WHERE PipelineRunId = @PipelineRunId
      AND CompletedAtUtc IS NULL;

    COMMIT TRANSACTION;
END;