CREATE OR ALTER FUNCTION dbo.ufn_ClassFinder_NormalizeDays (@value NVARCHAR(40))
RETURNS NVARCHAR(40)
AS
BEGIN
    DECLARE @normalized NVARCHAR(40) = UPPER(LTRIM(RTRIM(COALESCE(@value, N''))));
    DECLARE @result NVARCHAR(40);

    IF @normalized = N''
    BEGIN
        RETURN N'';
    END;

    IF CHARINDEX(',', @normalized) > 0
    BEGIN
        SELECT @result = STRING_AGG(
            CASE token
                WHEN N'M' THEN N'Mon'
                WHEN N'MON' THEN N'Mon'
                WHEN N'T' THEN N'Tue'
                WHEN N'TUE' THEN N'Tue'
                WHEN N'W' THEN N'Wed'
                WHEN N'WED' THEN N'Wed'
                WHEN N'TH' THEN N'Thu'
                WHEN N'THU' THEN N'Thu'
                WHEN N'F' THEN N'Fri'
                WHEN N'FRI' THEN N'Fri'
                WHEN N'S' THEN N'Sat'
                WHEN N'SAT' THEN N'Sat'
                WHEN N'SU' THEN N'Sun'
                WHEN N'SUN' THEN N'Sun'
                ELSE CONCAT(UPPER(LEFT(token, 1)), LOWER(SUBSTRING(token, 2, 19)))
            END,
            ','
        )
        FROM (
            SELECT LTRIM(RTRIM(value)) AS token
            FROM STRING_SPLIT(@normalized, ',')
            WHERE LTRIM(RTRIM(value)) <> N''
        ) AS split_tokens;

        RETURN COALESCE(@result, N'');
    END;

    SET @normalized = REPLACE(@normalized, N'TH', N'Thu,');
    SET @normalized = REPLACE(@normalized, N'SU', N'Sun,');
    SET @normalized = REPLACE(@normalized, N'M', N'Mon,');
    SET @normalized = REPLACE(@normalized, N'T', N'Tue,');
    SET @normalized = REPLACE(@normalized, N'W', N'Wed,');
    SET @normalized = REPLACE(@normalized, N'F', N'Fri,');
    SET @normalized = REPLACE(@normalized, N'S', N'Sat,');

    WHILE CHARINDEX(',,', @normalized) > 0
    BEGIN
        SET @normalized = REPLACE(@normalized, ',,', ',');
    END;

    WHILE RIGHT(@normalized, 1) = ','
    BEGIN
        SET @normalized = LEFT(@normalized, LEN(@normalized) - 1);
    END;

    RETURN @normalized;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_ClassFinder_BeginExternalSync
    @PipelineRunId NVARCHAR(128),
    @Summary NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    MERGE dbo.ExternalSourceSyncRuns AS target
    USING (SELECT @PipelineRunId AS PipelineRunId) AS source
        ON target.PipelineRunId = source.PipelineRunId
    WHEN MATCHED THEN
        UPDATE SET
            StartedAtUtc = SYSUTCDATETIME(),
            CompletedAtUtc = NULL,
            Status = N'Running',
            Summary = @Summary
    WHEN NOT MATCHED THEN
        INSERT (PipelineRunId, StartedAtUtc, CompletedAtUtc, Status, Summary)
        VALUES (@PipelineRunId, SYSUTCDATETIME(), NULL, N'Running', @Summary);
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_ClassFinder_FailExternalSync
    @PipelineRunId NVARCHAR(128),
    @Summary NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.ExternalSourceSyncRuns
    SET CompletedAtUtc = SYSUTCDATETIME(),
        Status = N'Failed',
        Summary = @Summary
    WHERE PipelineRunId = @PipelineRunId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.usp_ClassFinder_ApplyExternalSync
    @PipelineRunId NVARCHAR(128),
    @ObservedAtUtc DATETIMEOFFSET = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @syncObservedAt DATETIMEOFFSET = COALESCE(@ObservedAtUtc, SYSUTCDATETIME());
    DECLARE @summary NVARCHAR(MAX);

    BEGIN TRY
        EXEC dbo.usp_ClassFinder_BeginExternalSync
            @PipelineRunId = @PipelineRunId,
            @Summary = N'Applying staged external snapshot to curated catalog tables.';

        BEGIN TRANSACTION;

        ;WITH latest_students AS
        (
            SELECT
                NULLIF(LTRIM(RTRIM(ExternalStudentId)), N'') AS ExternalStudentId,
                LTRIM(RTRIM(FirstName)) AS FirstName,
                LTRIM(RTRIM(LastName)) AS LastName,
                LOWER(LTRIM(RTRIM(Email))) AS Email,
                ISNULL(NULLIF(LTRIM(RTRIM([Password])), N''), N'') AS [Password],
                ISNULL(NULLIF(LTRIM(RTRIM(Major)), N''), N'Undeclared') AS Major,
                ISNULL(NULLIF(LTRIM(RTRIM(Classification)), N''), N'Unspecified') AS Classification,
                ROW_NUMBER() OVER (
                    PARTITION BY COALESCE(NULLIF(LTRIM(RTRIM(ExternalStudentId)), N''), LOWER(LTRIM(RTRIM(Email))))
                    ORDER BY ObservedAtUtc DESC
                ) AS row_number
            FROM dbo.StageClassFinderStudents
        )
        MERGE dbo.Students AS target
        USING
        (
            SELECT ExternalStudentId, FirstName, LastName, Email, [Password], Major, Classification
            FROM latest_students
            WHERE row_number = 1 AND Email <> N''
        ) AS source
            ON (
                source.ExternalStudentId IS NOT NULL AND target.ExternalId = source.ExternalStudentId
            )
            OR target.Email = source.Email
        WHEN MATCHED THEN
            UPDATE SET
                ExternalId = COALESCE(source.ExternalStudentId, target.ExternalId),
                FirstName = source.FirstName,
                LastName = source.LastName,
                Email = source.Email,
                [Password] = source.[Password],
                Major = source.Major,
                Classification = source.Classification
        WHEN NOT MATCHED THEN
            INSERT (ExternalId, FirstName, LastName, Email, [Password], Major, Classification)
            VALUES (source.ExternalStudentId, source.FirstName, source.LastName, source.Email, source.[Password], source.Major, source.Classification);

        ;WITH latest_professors AS
        (
            SELECT
                NULLIF(LTRIM(RTRIM(ExternalProfessorId)), N'') AS ExternalProfessorId,
                LTRIM(RTRIM(FirstName)) AS FirstName,
                LTRIM(RTRIM(LastName)) AS LastName,
                LOWER(LTRIM(RTRIM(Email))) AS Email,
                ISNULL(NULLIF(LTRIM(RTRIM([Password])), N''), N'') AS [Password],
                ROW_NUMBER() OVER (
                    PARTITION BY COALESCE(NULLIF(LTRIM(RTRIM(ExternalProfessorId)), N''), LOWER(LTRIM(RTRIM(Email))))
                    ORDER BY ObservedAtUtc DESC
                ) AS row_number
            FROM dbo.StageClassFinderProfessors
        )
        MERGE dbo.Instructors AS target
        USING
        (
            SELECT ExternalProfessorId, FirstName, LastName, Email, [Password]
            FROM latest_professors
            WHERE row_number = 1 AND Email <> N''
        ) AS source
            ON (
                source.ExternalProfessorId IS NOT NULL AND target.ExternalId = source.ExternalProfessorId
            )
            OR target.Email = source.Email
        WHEN MATCHED THEN
            UPDATE SET
                ExternalId = COALESCE(source.ExternalProfessorId, target.ExternalId),
                FirstName = source.FirstName,
                LastName = source.LastName,
                Email = source.Email,
                [Password] = source.[Password]
        WHEN NOT MATCHED THEN
            INSERT (ExternalId, FirstName, LastName, Email, [Password])
            VALUES (source.ExternalProfessorId, source.FirstName, source.LastName, source.Email, source.[Password]);

        ;WITH unresolved_instructors AS
        (
            SELECT DISTINCT
                NULLIF(LTRIM(RTRIM(ExternalProfessorId)), N'') AS ExternalProfessorId,
                NULLIF(LOWER(LTRIM(RTRIM(ProfessorEmail))), N'') AS ProfessorEmail
            FROM dbo.StageClassFinderClasses
        )
        INSERT INTO dbo.Instructors (ExternalId, FirstName, LastName, Email, [Password])
        SELECT
            ExternalProfessorId,
            N'Imported',
            N'Instructor',
            COALESCE(ProfessorEmail, CONCAT(N'imported+', REPLACE(COALESCE(ExternalProfessorId, CONVERT(NVARCHAR(36), NEWID())), N' ', N'-'), N'@classfinder.local')),
            N''
        FROM unresolved_instructors AS source
        WHERE (source.ExternalProfessorId IS NOT NULL OR source.ProfessorEmail IS NOT NULL)
          AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.Instructors AS target
              WHERE (source.ExternalProfessorId IS NOT NULL AND target.ExternalId = source.ExternalProfessorId)
                 OR (source.ProfessorEmail IS NOT NULL AND target.Email = source.ProfessorEmail)
          );

        ;WITH latest_classes AS
        (
            SELECT
                NULLIF(LTRIM(RTRIM(ExternalClassId)), N'') AS ExternalClassId,
                NULLIF(LTRIM(RTRIM(CourseCode)), N'') AS CourseCode,
                LTRIM(RTRIM(ClassName)) AS ClassName,
                NULLIF(LTRIM(RTRIM(Department)), N'') AS Department,
                NULLIF(LTRIM(RTRIM(DepartmentCode)), N'') AS DepartmentCode,
                CourseNumber,
                NULLIF(LTRIM(RTRIM(SessionCode)), N'') AS SessionCode,
                NULLIF(LTRIM(RTRIM(Semester)), N'') AS Semester,
                NULLIF(LTRIM(RTRIM(ExternalProfessorId)), N'') AS ExternalProfessorId,
                NULLIF(LOWER(LTRIM(RTRIM(ProfessorEmail))), N'') AS ProfessorEmail,
                dbo.ufn_ClassFinder_NormalizeDays(DaysOfWeekCompact) AS DaysOfWeek,
                TRY_CONVERT(TIME(0), StartTime) AS StartTime,
                TRY_CONVERT(TIME(0), EndTime) AS EndTime,
                LTRIM(RTRIM(Location)) AS Location,
                MaxSeats,
                Credits,
                ROW_NUMBER() OVER (
                    PARTITION BY NULLIF(LTRIM(RTRIM(ExternalClassId)), N'')
                    ORDER BY ObservedAtUtc DESC
                ) AS row_number
            FROM dbo.StageClassFinderClasses
        ),
        resolved_classes AS
        (
            SELECT
                source.ExternalClassId,
                COALESCE(
                    CASE
                        WHEN COALESCE(source.DepartmentCode, N'') <> N''
                             AND COALESCE(
                                 source.CourseNumber,
                                 TRY_CONVERT(INT, SUBSTRING(source.CourseCode, NULLIF(PATINDEX('%[0-9]%', source.CourseCode), 0), 10))
                             ) IS NOT NULL
                        THEN CONCAT(
                            source.DepartmentCode,
                            COALESCE(
                                source.CourseNumber,
                                TRY_CONVERT(INT, SUBSTRING(source.CourseCode, NULLIF(PATINDEX('%[0-9]%', source.CourseCode), 0), 10))
                            )
                        )
                    END,
                    source.CourseCode
                ) AS CourseCode,
                source.ClassName,
                COALESCE(
                    source.Department,
                    CASE
                        WHEN PATINDEX('%[0-9]%', source.CourseCode) > 1 THEN LEFT(source.CourseCode, PATINDEX('%[0-9]%', source.CourseCode) - 1)
                        ELSE N'General'
                    END
                ) AS Department,
                COALESCE(
                    source.DepartmentCode,
                    CASE
                        WHEN PATINDEX('%[0-9]%', source.CourseCode) > 1 THEN LEFT(source.CourseCode, PATINDEX('%[0-9]%', source.CourseCode) - 1)
                        ELSE N'GEN'
                    END
                ) AS DepartmentCode,
                COALESCE(
                    source.CourseNumber,
                    TRY_CONVERT(INT, SUBSTRING(source.CourseCode, NULLIF(PATINDEX('%[0-9]%', source.CourseCode), 0), 10))
                ) AS CourseNumber,
                COALESCE(source.SessionCode, RIGHT(CONCAT(N'00', ROW_NUMBER() OVER (ORDER BY source.CourseCode, source.StartTime, source.Location)), 2)) AS SessionCode,
                COALESCE(source.Semester, N'Current') AS Semester,
                source.DaysOfWeek,
                source.StartTime,
                source.EndTime,
                source.Location,
                source.MaxSeats,
                source.Credits,
                instructor.Id AS InstructorId
            FROM latest_classes AS source
            INNER JOIN dbo.Instructors AS instructor
                ON (source.ExternalProfessorId IS NOT NULL AND instructor.ExternalId = source.ExternalProfessorId)
                OR (source.ProfessorEmail IS NOT NULL AND instructor.Email = source.ProfessorEmail)
            WHERE source.row_number = 1
              AND source.ExternalClassId IS NOT NULL
              AND source.CourseCode IS NOT NULL
              AND source.StartTime IS NOT NULL
              AND source.EndTime IS NOT NULL
              AND source.Location <> N''
        )
        MERGE dbo.CourseClasses AS target
        USING resolved_classes AS source
            ON (
                source.ExternalClassId IS NOT NULL AND target.ExternalId = source.ExternalClassId
            )
            OR (
                target.CourseCode = source.CourseCode
                AND target.SessionCode = source.SessionCode
                AND target.StartTime = source.StartTime
                AND target.EndTime = source.EndTime
                AND target.Location = source.Location
            )
        WHEN MATCHED THEN
            UPDATE SET
                ExternalId = COALESCE(source.ExternalClassId, target.ExternalId),
                ClassName = source.ClassName,
                CourseCode = source.CourseCode,
                Department = source.Department,
                DepartmentCode = source.DepartmentCode,
                CourseNumber = source.CourseNumber,
                SessionCode = source.SessionCode,
                Semester = source.Semester,
                Location = source.Location,
                Credits = source.Credits,
                Capacity = source.MaxSeats,
                DaysOfWeek = source.DaysOfWeek,
                StartTime = source.StartTime,
                EndTime = source.EndTime,
                InstructorId = source.InstructorId,
                [Description] = COALESCE(NULLIF(target.[Description], N''), source.ClassName)
        WHEN NOT MATCHED THEN
            INSERT (ExternalId, ClassName, CourseCode, Department, DepartmentCode, CourseNumber, SessionCode, Semester, Location, Credits, Capacity, DaysOfWeek, StartTime, EndTime, InstructorId, DropDeadlineUtc, [Description])
            VALUES (source.ExternalClassId, source.ClassName, source.CourseCode, source.Department, source.DepartmentCode, source.CourseNumber, source.SessionCode, source.Semester, source.Location, source.Credits, source.MaxSeats, source.DaysOfWeek, source.StartTime, source.EndTime, source.InstructorId, NULL, source.ClassName);

        ;WITH external_status_snapshot AS
        (
            SELECT
                NULLIF(LTRIM(RTRIM(ExternalEnrollmentId)), N'') AS ExternalRecordId,
                NULLIF(LTRIM(RTRIM(ExternalStudentId)), N'') AS ExternalStudentId,
                NULLIF(LTRIM(RTRIM(ExternalClassId)), N'') AS ExternalClassId,
                CAST(N'Enrolled' AS NVARCHAR(40)) AS EnrollmentStatus,
                CAST(NULL AS INT) AS WaitlistPosition,
                EnrollmentDateUtc AS RecordedAtUtc,
                ObservedAtUtc
            FROM dbo.StageClassFinderEnrollments
            UNION ALL
            SELECT
                NULLIF(LTRIM(RTRIM(ExternalWaitlistId)), N'') AS ExternalRecordId,
                NULLIF(LTRIM(RTRIM(ExternalStudentId)), N'') AS ExternalStudentId,
                NULLIF(LTRIM(RTRIM(ExternalClassId)), N'') AS ExternalClassId,
                CAST(N'Waitlisted' AS NVARCHAR(40)) AS EnrollmentStatus,
                Position,
                SignupDateUtc AS RecordedAtUtc,
                ObservedAtUtc
            FROM dbo.StageClassFinderWaitlist
        ),
        resolved_external_status AS
        (
            SELECT
                student.Id AS StudentId,
                courseClass.Id AS CourseClassId,
                source.ExternalRecordId,
                CASE source.EnrollmentStatus
                    WHEN N'Waitlisted' THEN 2
                    ELSE 1
                END AS [Status],
                CASE source.EnrollmentStatus
                    WHEN N'Waitlisted' THEN source.WaitlistPosition
                    ELSE NULL
                END AS WaitlistPosition,
                COALESCE(source.RecordedAtUtc, source.ObservedAtUtc, @syncObservedAt) AS StatusChangedAtUtc,
                ROW_NUMBER() OVER (
                    PARTITION BY student.Id, courseClass.Id
                    ORDER BY CASE source.EnrollmentStatus WHEN N'Enrolled' THEN 0 ELSE 1 END, source.ObservedAtUtc DESC
                ) AS row_number
            FROM external_status_snapshot AS source
            INNER JOIN dbo.Students AS student
                ON student.ExternalId = source.ExternalStudentId
            INNER JOIN dbo.CourseClasses AS courseClass
                ON courseClass.ExternalId = source.ExternalClassId
            WHERE source.ExternalStudentId IS NOT NULL
              AND source.ExternalClassId IS NOT NULL
        )
        MERGE dbo.Enrollments AS target
        USING
        (
            SELECT StudentId, CourseClassId, ExternalRecordId, [Status], WaitlistPosition, StatusChangedAtUtc
            FROM resolved_external_status
            WHERE row_number = 1
        ) AS source
            ON target.StudentId = source.StudentId
           AND target.CourseClassId = source.CourseClassId
        WHEN MATCHED AND target.SourceSystem <> N'Application' THEN
            UPDATE SET
                ExternalRecordId = COALESCE(source.ExternalRecordId, target.ExternalRecordId),
                [Status] = source.[Status],
                WaitlistPosition = source.WaitlistPosition,
                SourceSystem = N'ExternalSync',
                StatusChangedAtUtc = source.StatusChangedAtUtc,
                LastSeenInExternalSyncUtc = @syncObservedAt
        WHEN NOT MATCHED THEN
            INSERT (StudentId, CourseClassId, ExternalRecordId, [Status], WaitlistPosition, SourceSystem, StatusChangedAtUtc, LastSeenInExternalSyncUtc)
            VALUES (source.StudentId, source.CourseClassId, source.ExternalRecordId, source.[Status], source.WaitlistPosition, N'ExternalSync', source.StatusChangedAtUtc, @syncObservedAt);

        ;WITH external_status_snapshot AS
        (
            SELECT
                NULLIF(LTRIM(RTRIM(ExternalEnrollmentId)), N'') AS ExternalRecordId,
                NULLIF(LTRIM(RTRIM(ExternalStudentId)), N'') AS ExternalStudentId,
                NULLIF(LTRIM(RTRIM(ExternalClassId)), N'') AS ExternalClassId,
                CAST(N'Enrolled' AS NVARCHAR(40)) AS EnrollmentStatus,
                CAST(NULL AS INT) AS WaitlistPosition,
                EnrollmentDateUtc AS RecordedAtUtc,
                ObservedAtUtc
            FROM dbo.StageClassFinderEnrollments
            UNION ALL
            SELECT
                NULLIF(LTRIM(RTRIM(ExternalWaitlistId)), N'') AS ExternalRecordId,
                NULLIF(LTRIM(RTRIM(ExternalStudentId)), N'') AS ExternalStudentId,
                NULLIF(LTRIM(RTRIM(ExternalClassId)), N'') AS ExternalClassId,
                CAST(N'Waitlisted' AS NVARCHAR(40)) AS EnrollmentStatus,
                Position,
                SignupDateUtc AS RecordedAtUtc,
                ObservedAtUtc
            FROM dbo.StageClassFinderWaitlist
        ),
        latest_external_pairs AS
        (
            SELECT StudentId, CourseClassId
            FROM
            (
                SELECT
                    student.Id AS StudentId,
                    courseClass.Id AS CourseClassId,
                    ROW_NUMBER() OVER (
                        PARTITION BY student.Id, courseClass.Id
                        ORDER BY snapshot.ObservedAtUtc DESC
                    ) AS row_number
                FROM external_status_snapshot AS snapshot
                INNER JOIN dbo.Students AS student
                    ON student.ExternalId = snapshot.ExternalStudentId
                INNER JOIN dbo.CourseClasses AS courseClass
                    ON courseClass.ExternalId = snapshot.ExternalClassId
                WHERE snapshot.ExternalStudentId IS NOT NULL
                  AND snapshot.ExternalClassId IS NOT NULL
            ) AS deduplicated
            WHERE row_number = 1
        )
        UPDATE target
        SET [Status] = 3,
            WaitlistPosition = NULL,
            StatusChangedAtUtc = @syncObservedAt,
            LastSeenInExternalSyncUtc = @syncObservedAt
        FROM dbo.Enrollments AS target
        LEFT JOIN latest_external_pairs AS source
            ON source.StudentId = target.StudentId
           AND source.CourseClassId = target.CourseClassId
        WHERE target.SourceSystem = N'ExternalSync'
          AND source.StudentId IS NULL
          AND target.[Status] IN (1, 2);

        COMMIT TRANSACTION;

        SET @summary = (
            SELECT
                @PipelineRunId AS pipelineRunId,
                @syncObservedAt AS appliedAtUtc,
                (SELECT COUNT(*) FROM dbo.StageClassFinderStudents) AS stagedStudents,
                (SELECT COUNT(*) FROM dbo.StageClassFinderProfessors) AS stagedProfessors,
                (SELECT COUNT(*) FROM dbo.StageClassFinderClasses) AS stagedClasses,
                (SELECT COUNT(*) FROM dbo.StageClassFinderEnrollments) AS stagedEnrollments,
                (SELECT COUNT(*) FROM dbo.StageClassFinderWaitlist) AS stagedWaitlist,
                (SELECT COUNT(*) FROM dbo.Students WHERE ExternalId IS NOT NULL) AS curatedStudents,
                (SELECT COUNT(*) FROM dbo.Instructors WHERE ExternalId IS NOT NULL) AS curatedProfessors,
                (SELECT COUNT(*) FROM dbo.CourseClasses WHERE ExternalId IS NOT NULL) AS curatedClasses,
                (SELECT COUNT(*) FROM dbo.Enrollments WHERE SourceSystem = N'ExternalSync' AND [Status] = 1) AS curatedExternalEnrollments,
                (SELECT COUNT(*) FROM dbo.Enrollments WHERE SourceSystem = N'ExternalSync' AND [Status] = 2) AS curatedExternalWaitlist
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
        );

        UPDATE dbo.ExternalSourceSyncRuns
        SET CompletedAtUtc = SYSUTCDATETIME(),
            Status = N'Succeeded',
            Summary = @summary
        WHERE PipelineRunId = @PipelineRunId;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0
        BEGIN
            ROLLBACK TRANSACTION;
        END;

        DECLARE @errorSummary NVARCHAR(MAX) = CONCAT(
            N'External sync failed: ',
            ERROR_MESSAGE(),
            N' (line ',
            ERROR_LINE(),
            N', number ',
            ERROR_NUMBER(),
            N')'
        );

        EXEC dbo.usp_ClassFinder_FailExternalSync
            @PipelineRunId = @PipelineRunId,
            @Summary = @errorSummary;

        THROW;
    END CATCH;
END;
GO
