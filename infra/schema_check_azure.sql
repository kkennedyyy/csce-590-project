/*
Canonical schema compatibility check for Azure SQL.
This matches backend EF Core model and frontend cloud API expectations.
*/

-- 1) Required tables.
WITH required_tables AS (
    SELECT 'Students' AS table_name UNION ALL
    SELECT 'Instructors' UNION ALL
    SELECT 'CourseClasses' UNION ALL
    SELECT 'Enrollments'
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

    SELECT 'Instructors', 'Id' UNION ALL
    SELECT 'Instructors', 'FirstName' UNION ALL
    SELECT 'Instructors', 'LastName' UNION ALL
    SELECT 'Instructors', 'Email' UNION ALL

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

    SELECT 'Enrollments', 'Id' UNION ALL
    SELECT 'Enrollments', 'StudentId' UNION ALL
    SELECT 'Enrollments', 'CourseClassId' UNION ALL
    SELECT 'Enrollments', 'Status' UNION ALL
    SELECT 'Enrollments', 'WaitlistPosition'
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
WHERE t.name IN ('Students', 'Instructors', 'CourseClasses', 'Enrollments')
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
WHERE t.name IN ('Students', 'Instructors', 'CourseClasses', 'Enrollments')
ORDER BY t.name;
