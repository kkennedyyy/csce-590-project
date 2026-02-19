-- ===============================================
-- Table: ClassSchedule
-- Purpose: Store Class Schedule information
-- ===============================================

CREATE TABLE ClassSchedule (
    ScheduleId INT PRIMARY KEY IDENTITY(1,1), -- Sc
    SectionId INT NOT NULL,
    DayOfWeek NVARCHAR(10) NOT NULL, -- Example: Monday, Tuesday, Wednesday
    StartTime TIME NOT NULL,
    EndTime TIME NOT NULL,
    Location NVARCHAR(100) NOT NULL,

    CONSTRAINT FK_ClassSchedule_Section FOREIGN KEY (SectionId)
        REFERENCES ClassSections(SectionId)
);