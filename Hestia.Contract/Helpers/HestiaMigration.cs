using System.Collections.Frozen;

namespace Hestia.Contract.Helpers;

public static class HestiaMigration
{
    public static readonly FrozenDictionary<int, string> Migrations;

    static HestiaMigration()
    {
        Migrations = new Dictionary<int, string>
        {
            {
                5,
                @"
CREATE TABLE IF NOT EXISTS ToDos (
    Id TEXT PRIMARY KEY NOT NULL,
    Name TEXT NOT NULL CHECK(length(Name) <= 255),
    NormalizeName TEXT NOT NULL CHECK(length(NormalizeName) <= 255),
    OrderIndex INTEGER NOT NULL,
    Description TEXT NOT NULL CHECK(length(Description) <= 10000),
    CreatedDateTime TEXT NOT NULL,
    Type INTEGER NOT NULL,
    IsBookmark INTEGER NOT NULL CHECK(IsBookmark IN (0, 1)),
    IsFavorite INTEGER NOT NULL CHECK(IsFavorite IN (0, 1)),
    DueDate TEXT NOT NULL,
    IsCompleted INTEGER NOT NULL CHECK(IsCompleted IN (0, 1)),
    TypeOfPeriodicity INTEGER NOT NULL,
    WeeklyDays TEXT NOT NULL,
    MonthlyDays TEXT NOT NULL,
    AnnuallyDays TEXT NOT NULL,
    LastCompleted TEXT,
    DaysOffset INTEGER NOT NULL,
    MonthsOffset INTEGER NOT NULL,
    WeeksOffset INTEGER NOT NULL,
    YearsOffset INTEGER NOT NULL,
    ChildrenCompletionType INTEGER NOT NULL,
    CurrentCircleOrderIndex INTEGER NOT NULL,
    Link TEXT NOT NULL CHECK(length(Link) <= 1000),
    IsRequiredCompleteInDueDate INTEGER NOT NULL CHECK(IsRequiredCompleteInDueDate IN (0, 1)),
    DescriptionType INTEGER NOT NULL,
    Icon TEXT NOT NULL,
    Color TEXT NOT NULL,
    RemindDaysBefore INTEGER NOT NULL,
    ReferenceId TEXT,
    ParentId TEXT,
    -- Optional: Foreign key constraint for self-referencing ParentId
    FOREIGN KEY (ParentId) REFERENCES ToDos (Id)
);
"
            },
        }.ToFrozenDictionary();
    }
}
