using Gaia.Models;

namespace Hestia.Contract.Models;

public class EditToDoItems
{
    public Guid[] Ids { get; set; } = [];
    public EditPropertyValue<string> Name { get; set; } = new();
    public EditPropertyValue<bool> IsFavorite { get; set; } = new();
    public EditPropertyValue<ToDoType> Type { get; set; } = new();
    public EditPropertyValue<string> Description { get; set; } = new();
    public EditPropertyValue<Uri?> Link { get; set; } = new();
    public EditPropertyValue<Guid?> ParentId { get; set; } = new();
    public EditPropertyValue<DescriptionType> DescriptionType { get; set; } = new();
    public EditPropertyValue<Guid?> ReferenceId { get; set; } = new();
    public EditPropertyValue<DayOfYear[]> AnnuallyDays { get; set; } = new();
    public EditPropertyValue<byte[]> MonthlyDays { get; set; } = new();
    public EditPropertyValue<ChildrenCompletionType> ChildrenType { get; set; } = new();
    public EditPropertyValue<DateOnly> DueDate { get; set; } = new();
    public EditPropertyValue<ushort> DaysOffset { get; set; } = new();
    public EditPropertyValue<ushort> MonthsOffset { get; set; } = new();
    public EditPropertyValue<ushort> WeeksOffset { get; set; } = new();
    public EditPropertyValue<ushort> YearsOffset { get; set; } = new();
    public EditPropertyValue<bool> IsRequiredCompleteInDueDate { get; set; } = new();
    public EditPropertyValue<TypeOfPeriodicity> TypeOfPeriodicity { get; set; } = new();
    public EditPropertyValue<DayOfWeek[]> WeeklyDays { get; set; } = new();
    public EditPropertyValue<bool> IsBookmark { get; set; } = new();
    public EditPropertyValue<string> Icon { get; set; } = new();
    public EditPropertyValue<string> Color { get; set; } = new();
    public EditPropertyValue<uint> RemindDaysBefore { get; set; } = new();
}