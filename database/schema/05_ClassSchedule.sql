-- ===============================================
-- Table: ClassSchedule
-- Purpose: Store Class Schedule information
-- ===============================================

CREATE TABLE ClassSchedule (
    ScheduleId INT PRIMARY KEY IDENTITY(1,1), -- Auto-Incremental ID
    SectionId INT NOT NULL, -- Section Id (Sections)
    DayOfWeek NVARCHAR(10) NOT NULL, -- Example: Monday, Tuesday, Wednesday
    StartTime TIME NOT NULL, -- Start time[hh:mm:ss]
    EndTime TIME NOT NULL, -- End time[hh:mm:ss]
    Location NVARCHAR(100) NOT NULL, -- Location of Class

    -- Foreign Key SectionId references the SectionId in ClassSections, cannot use a SectionId if it is not in ClassSections.
    CONSTRAINT FK_ClassSchedule_Section FOREIGN KEY (SectionId)
        REFERENCES ClassSections(SectionId)
);