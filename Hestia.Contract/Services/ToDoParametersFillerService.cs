using System.Collections.Frozen;
using Gaia.Helpers;
using Hestia.Contract.Helpers;
using Hestia.Contract.Models;

namespace Hestia.Contract.Services;

public class ToDoParametersFillerService
{
    public ToDoItemParameters GetToDoItemParameters(
        FrozenDictionary<Guid, ToDoEntity> allItems,
        Dictionary<Guid, FullToDo> fullToDoItems, ToDoEntity entity,
        TimeSpan offset)
    {
        var parameters = GetToDoItemParameters(
            allItems,
            fullToDoItems,
            entity,
            offset,
            new()
        );

        if (parameters.ActiveItem is { } active && active.Id == entity.Id)
        {
            parameters.ActiveItem = null;
        }

        var today = DateTimeOffset.UtcNow.Add(offset).Date.ToDateOnly();

        if (parameters.Status == ToDoStatus.Planned
         && entity.RemindDaysBefore != 0 && today
         >= entity.DueDate.AddDays((int)-entity.RemindDaysBefore))
        {
            parameters.Status = ToDoStatus.ComingSoon;
        }

        return parameters;
    }

    private ToDoItemParameters GetToDoItemParameters(
        FrozenDictionary<Guid, ToDoEntity> allItems,
        Dictionary<Guid, FullToDo> fullToDoItems,
        ToDoEntity entity,
        TimeSpan offset,
        ToDoItemParameters parameters
    )
    {
        var isDueable = IsDueable(allItems, entity);

        if (isDueable)
        {
            return GetToDoItemParameters(
                allItems,
                fullToDoItems,
                entity,
                entity.DueDate,
                offset,
                parameters,
                false,
                new()
            );
        }

        return GetToDoItemParameters(
            allItems,
            fullToDoItems,
            entity,
            DateTimeOffset.UtcNow.Add(offset).Date.ToDateOnly(),
            offset,
            parameters,
            false,
            new()
        );
    }

    private ToDoItemParameters GetToDoItemParameters(
        FrozenDictionary<Guid, ToDoEntity> allItems,
        Dictionary<Guid, FullToDo> fullToDoItems,
        ToDoEntity entity,
        DateOnly dueDate,
        TimeSpan offset,
        ToDoItemParameters parameters,
        bool useDueDate,
        List<Guid> ignoreIds
    )
    {
        if (entity.Type == ToDoType.Reference)
        {
            if (entity.ReferenceId.HasValue
             && entity.ReferenceId.Value != entity.Id)
            {
                ignoreIds.Add(entity.Id);

                return GetToDoItemParameters(
                    allItems,
                    fullToDoItems,
                    allItems[entity.ReferenceId.Value],
                    dueDate,
                    offset,
                    parameters,
                    useDueDate,
                    ignoreIds
                );
            }

            parameters.IsCan = ToDoItemIsCan.None;
            parameters.ActiveItem = null;
            parameters.Status = ToDoStatus.Miss;
            fullToDoItems[entity.Id] = entity.ToFullToDo(parameters);

            return parameters;
        }

        var isCompletable = IsCompletable(entity);

        if (entity.IsCompleted && isCompletable)
        {
            parameters.IsCan = ToDoItemIsCan.CanIncomplete;
            parameters.ActiveItem = null;
            parameters.Status = ToDoStatus.Completed;
            fullToDoItems[entity.Id] = entity.ToFullToDo(parameters);

            return parameters;
        }

        var isMiss = false;
        var isDueable = IsDueable(allItems, entity);

        if (isDueable)
        {
            if (useDueDate)
            {
                if (entity.DueDate < dueDate
                 && entity.IsRequiredCompleteInDueDate)
                {
                    isMiss = true;
                }

                if (entity.DueDate > dueDate)
                {
                    parameters.ActiveItem = null;
                    parameters.Status = ToDoStatus.Planned;
                    parameters.IsCan = ToDoItemIsCan.None;
                    fullToDoItems[entity.Id] =
                        entity.ToFullToDo(parameters);

                    return parameters;
                }
            }
            else
            {
                if (entity.DueDate < DateTimeOffset.UtcNow.Add(offset).Date
                       .ToDateOnly() && entity.IsRequiredCompleteInDueDate)
                {
                    isMiss = true;
                }

                if (entity.DueDate > DateTimeOffset.UtcNow.Add(offset).Date
                       .ToDateOnly())
                {
                    parameters.ActiveItem = null;
                    parameters.Status = ToDoStatus.Planned;
                    parameters.IsCan = ToDoItemIsCan.None;
                    fullToDoItems[entity.Id] =
                        entity.ToFullToDo(parameters);

                    return parameters;
                }
            }
        }

        var items = allItems.Values
           .Where(x => x.ParentId == entity.Id && !ignoreIds.Contains(x.Id))
           .OrderBy(x => x.OrderIndex).ToArray();
        ShortToDo? firstReadyForComplete = null;
        ShortToDo? firstMiss = null;
        var hasPlanned = false;

        foreach (var item in items)
        {
            if (firstMiss is not null)
            {
                break;
            }

            parameters = GetToDoItemParameters(
                allItems,
                fullToDoItems,
                item,
                dueDate,
                offset,
                parameters,
                true,
                ignoreIds
            );

            switch (parameters.Status)
            {
                case ToDoStatus.Miss:
                {
                    if (firstMiss is not null)
                    {
                        break;
                    }

                    firstMiss = parameters.ActiveItem ?? ToActiveToDoItem(item);

                    break;
                }
                case ToDoStatus.ReadyForComplete:
                {
                    if (firstReadyForComplete is not null)
                    {
                        break;
                    }

                    firstReadyForComplete = parameters.ActiveItem
                     ?? ToActiveToDoItem(item);

                    break;
                }
                case ToDoStatus.Planned:
                    hasPlanned = true;

                    break;
                case ToDoStatus.Completed:
                    break;
                case ToDoStatus.ComingSoon:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        var firstActive =
            firstMiss ?? firstReadyForComplete;

        var isGroup = IsGroup(entity);

        if (isGroup)
        {
            if (isMiss)
            {
                parameters.ActiveItem = firstActive ?? ToActiveToDoItem(entity);
                parameters.Status = ToDoStatus.Miss;
                parameters.IsCan = ToDoItemIsCan.None;
                fullToDoItems[entity.Id] = entity.ToFullToDo(parameters);

                return parameters;
            }

            if (firstMiss is not null)
            {
                parameters.ActiveItem = firstMiss;
                parameters.Status = ToDoStatus.Miss;
                parameters.IsCan = ToDoItemIsCan.None;
                fullToDoItems[entity.Id] = entity.ToFullToDo(parameters);

                return parameters;
            }

            if (firstReadyForComplete is not null)
            {
                parameters.ActiveItem = firstReadyForComplete;
                parameters.Status = ToDoStatus.ReadyForComplete;
                parameters.IsCan = ToDoItemIsCan.None;
                fullToDoItems[entity.Id] = entity.ToFullToDo(parameters);

                return parameters;
            }

            if (hasPlanned)
            {
                parameters.ActiveItem = null;
                parameters.Status = ToDoStatus.Planned;
                parameters.IsCan = ToDoItemIsCan.None;
                fullToDoItems[entity.Id] = entity.ToFullToDo(parameters);

                return parameters;
            }

            parameters.ActiveItem = null;
            parameters.Status = ToDoStatus.Completed;
            parameters.IsCan = ToDoItemIsCan.None;
            fullToDoItems[entity.Id] = entity.ToFullToDo(parameters);

            return parameters;
        }

        if (isMiss)
        {
            switch (entity.ChildrenCompletionType)
            {
                case ChildrenCompletionType.RequireCompletion:
                    if (firstActive is not null)
                    {
                        parameters.ActiveItem = firstActive;
                        parameters.Status = ToDoStatus.Miss;
                        parameters.IsCan = ToDoItemIsCan.None;
                        fullToDoItems[entity.Id] = entity.ToFullToDo(parameters);

                        return parameters;
                    }

                    parameters.ActiveItem = ToActiveToDoItem(entity);
                    parameters.Status = ToDoStatus.Miss;
                    parameters.IsCan = ToDoItemIsCan.CanComplete;
                    fullToDoItems[entity.Id] = entity.ToFullToDo(parameters);

                    return parameters;
                case ChildrenCompletionType.IgnoreCompletion:
                    parameters.ActiveItem =
                        firstActive ?? ToActiveToDoItem(entity);
                    parameters.Status = ToDoStatus.Miss;
                    parameters.IsCan = ToDoItemIsCan.CanComplete;
                    fullToDoItems[entity.Id] = entity.ToFullToDo(parameters);

                    return parameters;
                default:
                    throw new ArgumentOutOfRangeException(entity.ChildrenCompletionType
                       .ToString());
            }
        }

        if (firstMiss is not null)
        {
            switch (entity.ChildrenCompletionType)
            {
                case ChildrenCompletionType.RequireCompletion:
                    parameters.ActiveItem = firstMiss;
                    parameters.Status = ToDoStatus.Miss;
                    parameters.IsCan = ToDoItemIsCan.None;
                    break;
                case ChildrenCompletionType.IgnoreCompletion:
                    parameters.ActiveItem = firstMiss;
                    parameters.Status = ToDoStatus.Miss;
                    parameters.IsCan = ToDoItemIsCan.CanComplete;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(entity.ChildrenCompletionType
                       .ToString());
            }

            fullToDoItems[entity.Id] = entity.ToFullToDo(parameters);

            return parameters;
        }

        if (firstReadyForComplete is not null)
        {
            switch (entity.ChildrenCompletionType)
            {
                case ChildrenCompletionType.RequireCompletion:
                    parameters.ActiveItem = firstReadyForComplete;
                    parameters.Status = ToDoStatus.ReadyForComplete;
                    parameters.IsCan = ToDoItemIsCan.None;

                    break;
                case ChildrenCompletionType.IgnoreCompletion:
                    parameters.ActiveItem = firstReadyForComplete;
                    parameters.Status = ToDoStatus.ReadyForComplete;
                    parameters.IsCan = ToDoItemIsCan.CanComplete;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(entity.ChildrenCompletionType
                       .ToString());
            }

            fullToDoItems[entity.Id] = entity.ToFullToDo(parameters);

            return parameters;
        }

        parameters.ActiveItem = null;
        parameters.Status = ToDoStatus.ReadyForComplete;
        parameters.IsCan = ToDoItemIsCan.CanComplete;
        fullToDoItems[entity.Id] = entity.ToFullToDo(parameters);

        return parameters;
    }

    private bool IsDueable(
        FrozenDictionary<Guid, ToDoEntity> allItems, ToDoEntity entity)
    {
        return entity.Type switch
        {
            ToDoType.Value => false,
            ToDoType.Group => false,
            ToDoType.FixedDate => true,
            ToDoType.Periodicity => true,
            ToDoType.PeriodicityOffset => true,
            ToDoType.Circle => false,
            ToDoType.Step => false,
            ToDoType.Reference => entity.ReferenceId.HasValue
             && entity.ReferenceId != entity.Id && IsDueable(allItems, allItems[entity.ReferenceId.Value]),
            _ => throw new ArgumentOutOfRangeException(entity.Type.ToString()),
        };
    }

    private bool IsCompletable(ToDoEntity entity)
    {
        return entity.Type switch
        {
            ToDoType.Value => true,
            ToDoType.Group => false,
            ToDoType.FixedDate => true,
            ToDoType.Periodicity => false,
            ToDoType.PeriodicityOffset => false,
            ToDoType.Circle => true,
            ToDoType.Step => true,
            ToDoType.Reference => false,
            _ => throw new ArgumentOutOfRangeException(entity.Type.ToString()),
        };
    }

    private bool IsGroup(ToDoEntity entity)
    {
        return entity.Type switch
        {
            ToDoType.Value => false,
            ToDoType.Group => true,
            ToDoType.FixedDate => false,
            ToDoType.Periodicity => false,
            ToDoType.PeriodicityOffset => false,
            ToDoType.Circle => false,
            ToDoType.Step => false,
            ToDoType.Reference => false,
            _ => throw new ArgumentOutOfRangeException(entity.Type.ToString()),
        };
    }

    private ShortToDo? ToActiveToDoItem(ToDoEntity entity)
    {
        return entity.ParentId is null ? null : entity.ToToDoShort();
    }
}