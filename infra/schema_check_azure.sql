/*
Canonical schema compatibility check for Azure SQL.
This matches backend EF Core model and frontend cloud API expectations.
*/

-- 1) Required tables.
WITH required_tables AS (
    SELECT 'Students' AS table_name UNION ALL
    SELECT 'Instructors' UNION ALL
    SELECT 'CourseClasses' UNION ALL
    SELECT 'Enrollments' UNION ALL
    SELECT 'CoursePrerequisites' UNION ALL
    SELECT 'StudentCourseHistories' UNION ALL
    SELECT 'ExternalSourceSyncRuns' UNION ALL
    SELECT 'StageClassFinderStudents' UNION ALL
    SELECT 'StageClassFinderProfessors' UNION ALL
    SELECT 'StageClassFinderClasses' UNION ALL
    SELECT 'StageClassFinderEnrollments' UNION ALL
    SELECT 'StageClassFinderWaitlist'
)
SELECT
    'table' AS check_type,
    t.table_name,
    CASE WHEN o.object_id IS NULL THEN 0 ELSE 1 END AS exists_flag
FROM required_tables t
LEFT JOIN sys.objects o
    ON o.name = t.table_name
   AND o.type = 'U'
ORDER BY t.table_name;

-- 2) Required columns.
WITH required_columns AS (
    SELECT 'Students' AS table_name, 'Id' AS column_name UNION ALL
    SELECT 'Students', 'FirstName' UNION ALL
    SELECT 'Students', 'LastName' UNION ALL
    SELECT 'Students', 'Email' UNION ALL
    SELECT 'Students', 'ExternalId' UNION ALL
    SELECT 'Students', 'Password' UNION ALL
    SELECT 'Students', 'Major' UNION ALL
    SELECT 'Students', 'Classification' UNION ALL

    SELECT 'Instructors', 'Id' UNION ALL
    SELECT 'Instructors', 'FirstName' UNION ALL
    SELECT 'Instructors', 'LastName' UNION ALL
    SELECT 'Instructors', 'Email' UNION ALL
    SELECT 'Instructors', 'ExternalId' UNION ALL
    SELECT 'Instructors', 'Password' UNION ALL

    SELECT 'CourseClasses', 'Id' UNION ALL
    SELECT 'CourseClasses', 'ClassName' UNION ALL
    SELECT 'CourseClasses', 'CourseCode' UNION ALL
    SELECT 'CourseClasses', 'Location' UNION ALL
    SELECT 'CourseClasses', 'Credits' UNION ALL
    SELECT 'CourseClasses', 'Capacity' UNION ALL
    SELECT 'CourseClasses', 'DaysOfWeek' UNION ALL
    SELECT 'CourseClasses', 'StartTime' UNION ALL
    SELECT 'CourseClasses', 'EndTime' UNION ALL
    SELECT 'CourseClasses', 'InstructorId' UNION ALL
    SELECT 'CourseClasses', 'ExternalId' UNION ALL
    SELECT 'CourseClasses', 'Department' UNION ALL
    SELECT 'CourseClasses', 'DepartmentCode' UNION ALL
    SELECT 'CourseClasses', 'CourseNumber' UNION ALL
    SELECT 'CourseClasses', 'SessionCode' UNION ALL
    SELECT 'CourseClasses', 'Semester' UNION ALL
    SELECT 'CourseClasses', 'DropDeadlineUtc' UNION ALL
    SELECT 'CourseClasses', 'Description' UNION ALL

    SELECT 'Enrollments', 'Id' UNION ALL
    SELECT 'Enrollments', 'StudentId' UNION ALL
    SELECT 'Enrollments', 'CourseClassId' UNION ALL
    SELECT 'Enrollments', 'Status' UNION ALL
    SELECT 'Enrollments', 'WaitlistPosition' UNION ALL
    SELECT 'Enrollments', 'ExternalRecordId' UNION ALL
    SELECT 'Enrollments', 'SourceSystem' UNION ALL
    SELECT 'Enrollments', 'StatusChangedAtUtc' UNION ALL
    SELECT 'Enrollments', 'LastSeenInExternalSyncUtc' UNION ALL

    SELECT 'CoursePrerequisites', 'Id' UNION ALL
    SELECT 'CoursePrerequisites', 'CourseClassId' UNION ALL
    SELECT 'CoursePrerequisites', 'RequiredCourseCode' UNION ALL

    SELECT 'StudentCourseHistories', 'Id' UNION ALL
    SELECT 'StudentCourseHistories', 'StudentId' UNION ALL
    SELECT 'StudentCourseHistories', 'CourseCode' UNION ALL
    SELECT 'StudentCourseHistories', 'CompletedAtUtc' UNION ALL

    SELECT 'ExternalSourceSyncRuns', 'Id' UNION ALL
    SELECT 'ExternalSourceSyncRuns', 'PipelineRunId' UNION ALL
    SELECT 'ExternalSourceSyncRuns', 'StartedAtUtc' UNION ALL
    SELECT 'ExternalSourceSyncRuns', 'CompletedAtUtc' UNION ALL
    SELECT 'ExternalSourceSyncRuns', 'Status' UNION ALL
    SELECT 'ExternalSourceSyncRuns', 'Summary' UNION ALL

    SELECT 'StageClassFinderStudents', 'ExternalStudentId' UNION ALL
    SELECT 'StageClassFinderStudents', 'Email' UNION ALL
    SELECT 'StageClassFinderProfessors', 'ExternalProfessorId' UNION ALL
    SELECT 'StageClassFinderProfessors', 'Email' UNION ALL
    SELECT 'StageClassFinderClasses', 'ExternalClassId' UNION ALL
    SELECT 'StageClassFinderClasses', 'CourseCode' UNION ALL
    SELECT 'StageClassFinderClasses', 'ExternalProfessorId' UNION ALL
    SELECT 'StageClassFinderEnrollments', 'ExternalEnrollmentId' UNION ALL
    SELECT 'StageClassFinderEnrollments', 'ExternalStudentId' UNION ALL
    SELECT 'StageClassFinderWaitlist', 'ExternalWaitlistId'
)
SELECT
    'column' AS check_type,
    c.table_name,
    c.column_name,
    CASE WHEN sc.column_id IS NULL THEN 0 ELSE 1 END AS exists_flag,
    TYPE_NAME(sc.user_type_id) AS sql_type
FROM required_columns c
LEFT JOIN sys.objects o
    ON o.name = c.table_name
   AND o.type = 'U'
LEFT JOIN sys.columns sc
    ON sc.object_id = o.object_id
   AND sc.name = c.column_name
ORDER BY c.table_name, c.column_name;

-- 3) Helpful index inventory for manual review (non-blocking).
SELECT
    'index_inventory' AS check_type,
    t.name AS table_name,
    i.name AS index_name,
    i.is_unique
FROM sys.tables t
JOIN sys.indexes i
    ON i.object_id = t.object_id
WHERE t.name IN (
    'Students',
    'Instructors',
    'CourseClasses',
    'Enrollments',
    'CoursePrerequisites',
    'StudentCourseHistories',
    'ExternalSourceSyncRuns',
    'StageClassFinderStudents',
    'StageClassFinderProfessors',
    'StageClassFinderClasses',
    'StageClassFinderEnrollments',
    'StageClassFinderWaitlist'
)
  AND i.name IS NOT NULL
ORDER BY t.name, i.name;

-- 4) Row counts for a quick sanity check after seed.
SELECT
    t.name AS table_name,
    p.rows AS approx_rows
FROM sys.tables t
JOIN sys.partitions p
    ON p.object_id = t.object_id
   AND p.index_id IN (0,1)
WHERE t.name IN (
    'Students',
    'Instructors',
    'CourseClasses',
    'Enrollments',
    'CoursePrerequisites',
    'StudentCourseHistories',
    'ExternalSourceSyncRuns',
    'StageClassFinderStudents',
    'StageClassFinderProfessors',
    'StageClassFinderClasses',
    'StageClassFinderEnrollments',
    'StageClassFinderWaitlist'
)
ORDER BY t.name;
