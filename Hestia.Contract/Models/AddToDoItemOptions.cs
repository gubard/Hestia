using Gaia.Models;

namespace Hestia.Contract.Models;

public class AddToDoItemOptions
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ToDoType Type { get; set; }
    public bool IsBookmark { get; set; }
    public bool IsFavorite { get; set; }
    public DateOnly DueDate { get; set; }
    public TypeOfPeriodicity TypeOfPeriodicity { get; set; }
    public DayOfYear[] AnnuallyDays { get; set; } = [];
    public byte[] MonthlyDays { get; set; } = [];
    public DayOfWeek[] WeeklyDays { get; set; } = [];
    public ushort DaysOffset { get; set; }
    public ushort MonthsOffset { get; set; }
    public ushort WeeksOffset { get; set; }
    public ushort YearsOffset { get; set; }
    public ChildrenCompletionType ChildrenCompletionType { get; set; }
    public Uri? Link { get; set; }
    public bool IsRequiredCompleteInDueDate { get; set; }
    public DescriptionType DescriptionType { get; set; }
    public string Icon { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public Guid? ReferenceId { get; set; }
    public Guid? ParentId { get; set; }
    public uint RemindDaysBefore { get; set; }
}