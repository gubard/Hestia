using Gaia.Helpers;
using Gaia.Models;
using Hestia.Contract.Models;

namespace Hestia.Contract.Helpers;

public static class Mapper
{
    public static IEnumerable<EditToDoEntity> ToEditToDoEntities(this EditToDos edit)
    {
        foreach (var id in edit.Ids)
        {
            yield return new(id)
            {
                IsEditName = edit.IsEditName,
                Name = edit.Name,

                IsEditDescription = edit.IsEditDescription,
                Description = edit.Description,

                IsEditType = edit.IsEditType,
                Type = edit.Type,

                IsEditDueDate = edit.IsEditDueDate,
                DueDate = edit.DueDate,

                IsEditTypeOfPeriodicity = edit.IsEditTypeOfPeriodicity,
                TypeOfPeriodicity = edit.TypeOfPeriodicity,

                IsEditAnnuallyDays = edit.IsEditAnnuallyDays,
                AnnuallyDays = edit.AnnuallyDays.Select(x => $"{x.Month}.{x.Day}").JoinString(";"),

                IsEditMonthlyDays = edit.IsEditMonthlyDays,
                MonthlyDays = edit.MonthlyDays.Select(x => $"{x}").JoinString(";"),

                IsEditWeeklyDays = edit.IsEditWeeklyDays,
                WeeklyDays = edit.WeeklyDays.Select(x => $"{x}").JoinString(";"),

                IsEditDaysOffset = edit.IsEditDaysOffset,
                DaysOffset = edit.DaysOffset,

                IsEditMonthsOffset = edit.IsEditMonthsOffset,
                MonthsOffset = edit.MonthsOffset,

                IsEditWeeksOffset = edit.IsEditWeeksOffset,
                WeeksOffset = edit.WeeksOffset,

                IsEditYearsOffset = edit.IsEditYearsOffset,
                YearsOffset = edit.YearsOffset,

                IsEditChildrenCompletionType = edit.IsEditChildrenCompletionType,
                ChildrenCompletionType = edit.ChildrenCompletionType,

                IsEditLink = edit.IsEditLink,
                Link = edit.Link,

                IsEditIsRequiredCompleteInDueDate = edit.IsEditIsRequiredCompleteInDueDate,
                IsRequiredCompleteInDueDate = edit.IsRequiredCompleteInDueDate,

                IsEditDescriptionType = edit.IsEditDescriptionType,
                DescriptionType = edit.DescriptionType,

                IsEditIcon = edit.IsEditIcon,
                Icon = edit.Icon,

                IsEditColor = edit.IsEditColor,
                Color = edit.Color,

                IsEditRemindDaysBefore = edit.IsEditRemindDaysBefore,
                RemindDaysBefore = edit.RemindDaysBefore,

                IsEditIsBookmark = edit.IsEditIsBookmark,
                IsBookmark = edit.IsBookmark,

                IsEditIsFavorite = edit.IsEditIsFavorite,
                IsFavorite = edit.IsFavorite,

                IsEditParentId = edit.IsEditParentId,
                ParentId = edit.ParentId,

                IsEditReferenceId = edit.IsEditReferenceId,
                ReferenceId = edit.ReferenceId,
            };
        }
    }

    public static FullToDo ToFullToDo(this ToDoEntity entity, ToDoItemParameters parameters)
    {
        return new()
        {
            IsCan = parameters.IsCan,
            Parameters = entity.ToToDoShort(),
            Status = parameters.Status,
            Active = parameters.ActiveItem,
        };
    }

    public static ShortToDo ToToDoShort(this ToDoEntity entity)
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
            AnnuallyDays = entity.GetDaysOfYear(),
            MonthlyDays = entity.GetDaysOfMonth(),
            WeeklyDays = entity.GetDaysOfWeek(),
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

    public static DayOfYear[] GetDaysOfYear(this ToDoEntity entity)
    {
        if (entity.AnnuallyDays.IsNullOrWhiteSpace() || uint.TryParse(entity.AnnuallyDays, out _))
        {
            return [new() { Day = 1, Month = Month.January }];
        }

        return entity
            .AnnuallyDays.Split(";")
            .Where(x => !uint.TryParse(x, out _))
            .Select(x => x.Split('.'))
            .Select(x => new DayOfYear { Day = byte.Parse(x[1]), Month = Enum.Parse<Month>(x[0]) })
            .ToArray();
    }

    public static int[] GetDaysOfMonth(this ToDoEntity entity)
    {
        if (entity.MonthlyDays.IsNullOrWhiteSpace())
        {
            return [1];
        }

        return entity.MonthlyDays.Split(";").Select(int.Parse).ToArray();
    }

    public static DayOfWeek[] GetDaysOfWeek(this ToDoEntity entity)
    {
        if (entity.WeeklyDays.IsNullOrWhiteSpace() || uint.TryParse(entity.WeeklyDays, out _))
        {
            return [DayOfWeek.Monday];
        }

        return entity.WeeklyDays.Split(";").Select(Enum.Parse<DayOfWeek>).ToArray();
    }

    public static ToDoEntity ToToDoEntity(this ShortToDo entity)
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
            AnnuallyDays = entity.AnnuallyDays.Select(x => $"{x.Month}.{x.Day}").JoinString(";"),
            MonthlyDays = entity.MonthlyDays.Select(x => $"{x}").JoinString(";"),
            WeeklyDays = entity.WeeklyDays.Select(x => $"{x}").JoinString(";"),
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
            ReferenceId = entity.ReferenceId,
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
