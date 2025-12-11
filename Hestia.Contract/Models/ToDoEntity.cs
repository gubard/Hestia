using Gaia.Helpers;
using Nestor.Db.Models;

namespace Hestia.Contract.Models;

[SourceEntity(nameof(Id))]
public partial class ToDoEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizeName { get; set; } = string.Empty;
    public uint OrderIndex { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset CreatedDateTime { get; set; } = DateTimeOffset.Now;
    public ToDoType Type { get; set; }
    public bool IsBookmark { get; set; }
    public bool IsFavorite { get; set; }
    public DateOnly DueDate { get; set; } = DateTime.Now.ToDateOnly();
    public bool IsCompleted { get; set; }
    public TypeOfPeriodicity TypeOfPeriodicity { get; set; }
    public string WeeklyDays { get; set; } = "Monday";
    public string MonthlyDays { get; set; } = "1";
    public string AnnuallyDays { get; set; } = "1.1";
    public DateTimeOffset? LastCompleted { get; set; }
    public ushort DaysOffset { get; set; } = 1;
    public ushort MonthsOffset { get; set; }
    public ushort WeeksOffset { get; set; }
    public ushort YearsOffset { get; set; }
    public ChildrenCompletionType ChildrenCompletionType { get; set; }
    public uint CurrentCircleOrderIndex { get; set; }
    public string Link { get; set; } = string.Empty;
    public bool IsRequiredCompleteInDueDate { get; set; } = true;
    public DescriptionType DescriptionType { get; set; }
    public string Icon { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public uint RemindDaysBefore { get; set; }
    public Guid? ReferenceId { get; set; }
    public Guid? ParentId { get; set; }
}