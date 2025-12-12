using Gaia.Models;
using Hestia.Contract.Models;

namespace Hestia.Contract.Helpers;

public static class Mapper
{
    public static FullToDo ToFullToDoItem(this ToDoEntity entity,
        ToDoItemParameters parameters)
    {
        return new()
        {
            IsCan = parameters.IsCan,
            Parameters = entity.ToToDoShortItem(),
            Status = parameters.Status,
            Active = parameters.ActiveItem,
        };
    }

    public static ShortToDo ToToDoShortItem(this ToDoEntity entity)
    {
        return new()
        {
            Id = entity.Id,
            Name = entity.Name,
            OrderIndex = entity.OrderIndex,
            Description = entity.Description,
            Type = entity.Type,
            IsBookmark = entity.IsBookmark,
            IsFavorite = entity.IsFavorite,
            DueDate = entity.DueDate,
            TypeOfPeriodicity = entity.TypeOfPeriodicity,
            AnnuallyDays = entity.AnnuallyDays.Split(";").Select(x => x.Split('.'))
               .Select(x => new DayOfYear(byte.Parse(x[1]), Enum.Parse<Month>(x[0]))).ToArray(),
            MonthlyDays = entity.MonthlyDays.Split(";").Select(int.Parse).ToArray(),
            WeeklyDays = entity.WeeklyDays.Split(";").Select(Enum.Parse<DayOfWeek>).ToArray(),
            DaysOffset = entity.DaysOffset,
            MonthsOffset = entity.MonthsOffset,
            WeeksOffset = entity.WeeksOffset,
            YearsOffset = entity.YearsOffset,
            ChildrenCompletionType = entity.ChildrenCompletionType,
            Link = entity.Link,
            IsRequiredCompleteInDueDate = entity.IsRequiredCompleteInDueDate,
            DescriptionType = entity.DescriptionType,
            Icon = entity.Icon,
            Color = entity.Color,
            ReferenceId = GetReferenceId(entity),
            ParentId = entity.ParentId,
            RemindDaysBefore = entity.RemindDaysBefore,
        };
    }

    private static Guid? GetReferenceId(ToDoEntity item)
    {
        if (item.Type != ToDoType.Reference)
        {
            return null;
        }

        if (item.ReferenceId == item.Id)
        {
            return null;
        }

        return item.ReferenceId;
    }
}