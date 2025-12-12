using Gaia.Models;
using Hestia.Contract.Models;

namespace Hestia.Contract.Services;

public interface IToDo
{
    public ToDoType Type { get; }
    public DateOnly DueDate { get; }
    public TypeOfPeriodicity TypeOfPeriodicity { get; }
    public IEnumerable<DayOfYear> AnnuallyDays { get; }
    public IEnumerable<int> MonthlyDays { get; }
    public IEnumerable<DayOfWeek> WeeklyDays { get; }
    public ushort DaysOffset { get; }
    public ushort MonthsOffset { get; }
    public ushort WeeksOffset { get; }
    public ushort YearsOffset { get; }
    public string Link { get; }
    public Guid? ReferenceId { get; }
}