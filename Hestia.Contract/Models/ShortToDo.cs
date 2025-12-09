namespace Hestia.Contract.Models;

public class ShortToDo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public uint OrderIndex { get; set; }
    public string Description { get; set; } = string.Empty;
    public ToDoItemType Type { get; set; }
    public bool IsBookmark { get; set; }
    public bool IsFavorite { get; set; }
    public DateOnly DueDate { get; set; }
    public TypeOfPeriodicity TypeOfPeriodicity { get; set; }
    public string AnnuallyDays { get; set; } = string.Empty;
    public string MonthlyDays { get; set; } = string.Empty;
    public string WeeklyDays { get; set; } = string.Empty;
    public ushort DaysOffset { get; set; }
    public ushort MonthsOffset { get; set; }
    public ushort WeeksOffset { get; set; }
    public ushort YearsOffset { get; set; }
    public ToDoItemChildrenType ChildrenType { get; set; }
    public string Link { get; set; } = string.Empty;
    public bool IsRequiredCompleteInDueDate { get; set; }
    public DescriptionType DescriptionType { get; set; }
    public string Icon { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public Guid? ReferenceId { get; set; }
    public Guid? ParentId { get; set; }
    public uint RemindDaysBefore { get; set; }
}