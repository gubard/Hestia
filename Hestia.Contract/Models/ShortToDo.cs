using Gaia.Models;
using Hestia.Contract.Services;

namespace Hestia.Contract.Models;

public class ShortToDo : IToDo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public uint OrderIndex { get; set; }
    public string Description { get; set; } = string.Empty;
    public ToDoType Type { get; set; }
    public bool IsBookmark { get; set; }
    public bool IsFavorite { get; set; }
    public DateOnly DueDate { get; set; }
    public TypeOfPeriodicity TypeOfPeriodicity { get; set; }
    public IEnumerable<DayOfYear> AnnuallyDays { get; set; } = [];
    public IEnumerable<int> MonthlyDays { get; set; } = [];
    public IEnumerable<DayOfWeek> WeeklyDays { get; set; } = [];
    public ushort DaysOffset { get; set; }
    public ushort MonthsOffset { get; set; }
    public ushort WeeksOffset { get; set; }
    public ushort YearsOffset { get; set; }
    public ChildrenCompletionType ChildrenCompletionType { get; set; }
    public string Link { get; set; } = string.Empty;
    public bool IsRequiredCompleteInDueDate { get; set; }
    public DescriptionType DescriptionType { get; set; }
    public string Icon { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public Guid? ReferenceId { get; set; }
    public Guid? ParentId { get; set; }
    public uint RemindDaysBefore { get; set; }
}