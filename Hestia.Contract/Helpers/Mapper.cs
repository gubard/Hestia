using Hestia.Contract.Models;

namespace Hestia.Contract.Helpers;

public static class Mapper
{
    public static FullToDoItem ToFullToDoItem(this ToDoEntity entity,
        ToDoItemParameters parameters)
    {
        return new()
        {
            IsCan = parameters.IsCan,
            Item = entity.ToToDoShortItem(),
            Status = parameters.Status,
            Active = parameters.ActiveItem,
        };
    }

    public static ToDoShortItem ToToDoShortItem(this ToDoEntity entity)
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
            AnnuallyDays = entity.AnnuallyDays,
            MonthlyDays = entity.MonthlyDays,
            WeeklyDays = entity.WeeklyDays,
            DaysOffset = entity.DaysOffset,
            MonthsOffset = entity.MonthsOffset,
            WeeksOffset = entity.WeeksOffset,
            YearsOffset = entity.YearsOffset,
            ChildrenType = entity.ChildrenType,
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
        if (item.Type != ToDoItemType.Reference)
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