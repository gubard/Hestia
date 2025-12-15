using Gaia.Models;

namespace Hestia.Contract.Models;

public class EditToDos
{
    public Guid[] Ids { get; set; } = [];
    public Guid? ParentId { get; set; }
    public Guid? ReferenceId { get; set; }
    public DayOfYear[] AnnuallyDays { get; set; } = [];
    public byte[] MonthlyDays { get; set; } = [];
    public ChildrenCompletionType ChildrenType { get; set; }
    public DayOfWeek[] WeeklyDays { get; set; } = [];
    public bool IsBookmark { get; set; }
    public bool IsFavorite { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ToDoType Type { get; set; }
    public DateOnly DueDate { get; set; }
    public TypeOfPeriodicity TypeOfPeriodicity { get; set; }
    public ushort DaysOffset { get; set; } = 1;
    public ushort MonthsOffset { get; set; }
    public ushort WeeksOffset { get; set; }
    public ushort YearsOffset { get; set; }
    public ChildrenCompletionType ChildrenCompletionType { get; set; }
    public string Link { get; set; } = string.Empty;
    public bool IsRequiredCompleteInDueDate { get; set; }
    public DescriptionType DescriptionType { get; set; }
    public string Icon { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public uint RemindDaysBefore { get; set; }
    public bool IsEditIsBookmark { get; set; }
    public bool IsEditIsFavorite { get; set; }
    public bool IsEditName { get; set; }
    public bool IsEditDescription { get; set; }
    public bool IsEditType { get; set; }
    public bool IsEditDueDate { get; set; }
    public bool IsEditTypeOfPeriodicity { get; set; }
    public bool IsEditAnnuallyDays { get; set; }
    public bool IsEditMonthlyDays { get; set; }
    public bool IsEditWeeklyDays { get; set; }
    public bool IsEditDaysOffset { get; set; }
    public bool IsEditMonthsOffset { get; set; }
    public bool IsEditWeeksOffset { get; set; }
    public bool IsEditYearsOffset { get; set; }
    public bool IsEditChildrenCompletionType { get; set; }
    public bool IsEditLink { get; set; }
    public bool IsEditIsRequiredCompleteInDueDate { get; set; }
    public bool IsEditDescriptionType { get; set; }
    public bool IsEditIcon { get; set; }
    public bool IsEditColor { get; set; }
    public bool IsEditReference { get; set; }
    public bool IsEditRemindDaysBefore { get; set; }
    public bool IsEditParentId { get; set; }
    public bool IsEditReferenceId { get; set; }
}
