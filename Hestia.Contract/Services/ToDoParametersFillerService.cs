using System.Collections.Frozen;
using Gaia.Helpers;
using Hestia.Contract.Helpers;
using Hestia.Contract.Models;

namespace Hestia.Contract.Services;

public class ToDoParametersFillerService
{
    public ToDoItemParameters GetToDoItemParameters(
        FrozenDictionary<Guid, ToDoEntity> allItems,
        Dictionary<Guid, FullToDoItem> fullToDoItems, ToDoEntity entity,
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

        if (parameters.Status == ToDoItemStatus.Planned
         && entity.RemindDaysBefore != 0 && today
         >= entity.DueDate.AddDays((int)-entity.RemindDaysBefore))
        {
            parameters.Status = ToDoItemStatus.ComingSoon;
        }

        return parameters;
    }

    private ToDoItemParameters GetToDoItemParameters(
        FrozenDictionary<Guid, ToDoEntity> allItems,
        Dictionary<Guid, FullToDoItem> fullToDoItems,
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
        Dictionary<Guid, FullToDoItem> fullToDoItems,
        ToDoEntity entity,
        DateOnly dueDate,
        TimeSpan offset,
        ToDoItemParameters parameters,
        bool useDueDate,
        List<Guid> ignoreIds
    )
    {
        if (entity.Type == ToDoItemType.Reference)
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
            parameters.Status = ToDoItemStatus.Miss;
            fullToDoItems[entity.Id] = entity.ToFullToDoItem(parameters);

            return parameters;
        }

        var isCompletable = IsCompletable(entity);

        if (entity.IsCompleted && isCompletable)
        {
            parameters.IsCan = ToDoItemIsCan.CanIncomplete;
            parameters.ActiveItem = null;
            parameters.Status = ToDoItemStatus.Completed;
            fullToDoItems[entity.Id] = entity.ToFullToDoItem(parameters);

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
                    parameters.Status = ToDoItemStatus.Planned;
                    parameters.IsCan = ToDoItemIsCan.None;
                    fullToDoItems[entity.Id] =
                        entity.ToFullToDoItem(parameters);

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
                    parameters.Status = ToDoItemStatus.Planned;
                    parameters.IsCan = ToDoItemIsCan.None;
                    fullToDoItems[entity.Id] =
                        entity.ToFullToDoItem(parameters);

                    return parameters;
                }
            }
        }

        var items = allItems.Values
           .Where(x => x.ParentId == entity.Id && !ignoreIds.Contains(x.Id))
           .OrderBy(x => x.OrderIndex).ToArray();
        ToDoShortItem? firstReadyForComplete = null;
        ToDoShortItem? firstMiss = null;
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
                case ToDoItemStatus.Miss:
                {
                    if (firstMiss is not null)
                    {
                        break;
                    }

                    firstMiss = parameters.ActiveItem ?? ToActiveToDoItem(item);

                    break;
                }
                case ToDoItemStatus.ReadyForComplete:
                {
                    if (firstReadyForComplete is not null)
                    {
                        break;
                    }

                    firstReadyForComplete = parameters.ActiveItem
                     ?? ToActiveToDoItem(item);

                    break;
                }
                case ToDoItemStatus.Planned:
                    hasPlanned = true;

                    break;
                case ToDoItemStatus.Completed:
                    break;
                case ToDoItemStatus.ComingSoon:
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
                parameters.Status = ToDoItemStatus.Miss;
                parameters.IsCan = ToDoItemIsCan.None;
                fullToDoItems[entity.Id] = entity.ToFullToDoItem(parameters);

                return parameters;
            }

            if (firstMiss is not null)
            {
                parameters.ActiveItem = firstMiss;
                parameters.Status = ToDoItemStatus.Miss;
                parameters.IsCan = ToDoItemIsCan.None;
                fullToDoItems[entity.Id] = entity.ToFullToDoItem(parameters);

                return parameters;
            }

            if (firstReadyForComplete is not null)
            {
                parameters.ActiveItem = firstReadyForComplete;
                parameters.Status = ToDoItemStatus.ReadyForComplete;
                parameters.IsCan = ToDoItemIsCan.None;
                fullToDoItems[entity.Id] = entity.ToFullToDoItem(parameters);

                return parameters;
            }

            if (hasPlanned)
            {
                parameters.ActiveItem = null;
                parameters.Status = ToDoItemStatus.Planned;
                parameters.IsCan = ToDoItemIsCan.None;
                fullToDoItems[entity.Id] = entity.ToFullToDoItem(parameters);

                return parameters;
            }

            parameters.ActiveItem = null;
            parameters.Status = ToDoItemStatus.Completed;
            parameters.IsCan = ToDoItemIsCan.None;
            fullToDoItems[entity.Id] = entity.ToFullToDoItem(parameters);

            return parameters;
        }

        if (isMiss)
        {
            switch (entity.ChildrenType)
            {
                case ToDoItemChildrenType.RequireCompletion:
                    if (firstActive is not null)
                    {
                        parameters.ActiveItem = firstActive;
                        parameters.Status = ToDoItemStatus.Miss;
                        parameters.IsCan = ToDoItemIsCan.None;
                        fullToDoItems[entity.Id] =
                            entity.ToFullToDoItem(parameters);

                        return parameters;
                    }

                    parameters.ActiveItem = ToActiveToDoItem(entity);
                    parameters.Status = ToDoItemStatus.Miss;
                    parameters.IsCan = ToDoItemIsCan.CanComplete;
                    fullToDoItems[entity.Id] =
                        entity.ToFullToDoItem(parameters);

                    return parameters;
                case ToDoItemChildrenType.IgnoreCompletion:
                    parameters.ActiveItem =
                        firstActive ?? ToActiveToDoItem(entity);
                    parameters.Status = ToDoItemStatus.Miss;
                    parameters.IsCan = ToDoItemIsCan.CanComplete;
                    fullToDoItems[entity.Id] =
                        entity.ToFullToDoItem(parameters);

                    return parameters;
                default:
                    throw new ArgumentOutOfRangeException(entity.ChildrenType
                       .ToString());
            }
        }

        if (firstMiss is not null)
        {
            switch (entity.ChildrenType)
            {
                case ToDoItemChildrenType.RequireCompletion:
                    parameters.ActiveItem = firstMiss;
                    parameters.Status = ToDoItemStatus.Miss;
                    parameters.IsCan = ToDoItemIsCan.None;
                    break;
                case ToDoItemChildrenType.IgnoreCompletion:
                    parameters.ActiveItem = firstMiss;
                    parameters.Status = ToDoItemStatus.Miss;
                    parameters.IsCan = ToDoItemIsCan.CanComplete;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(entity.ChildrenType
                       .ToString());
            }

            fullToDoItems[entity.Id] = entity.ToFullToDoItem(parameters);

            return parameters;
        }

        if (firstReadyForComplete is not null)
        {
            switch (entity.ChildrenType)
            {
                case ToDoItemChildrenType.RequireCompletion:
                    parameters.ActiveItem = firstReadyForComplete;
                    parameters.Status = ToDoItemStatus.ReadyForComplete;
                    parameters.IsCan = ToDoItemIsCan.None;

                    break;
                case ToDoItemChildrenType.IgnoreCompletion:
                    parameters.ActiveItem = firstReadyForComplete;
                    parameters.Status = ToDoItemStatus.ReadyForComplete;
                    parameters.IsCan = ToDoItemIsCan.CanComplete;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(entity.ChildrenType
                       .ToString());
            }

            fullToDoItems[entity.Id] = entity.ToFullToDoItem(parameters);

            return parameters;
        }

        parameters.ActiveItem = null;
        parameters.Status = ToDoItemStatus.ReadyForComplete;
        parameters.IsCan = ToDoItemIsCan.CanComplete;
        fullToDoItems[entity.Id] = entity.ToFullToDoItem(parameters);

        return parameters;
    }

    private bool IsDueable(
        FrozenDictionary<Guid, ToDoEntity> allItems, ToDoEntity entity)
    {
        return entity.Type switch
        {
            ToDoItemType.Value => false,
            ToDoItemType.Group => false,
            ToDoItemType.Planned => true,
            ToDoItemType.Periodicity => true,
            ToDoItemType.PeriodicityOffset => true,
            ToDoItemType.Circle => false,
            ToDoItemType.Step => false,
            ToDoItemType.Reference => entity.ReferenceId.HasValue
             && entity.ReferenceId != entity.Id
                    ? IsDueable(allItems, allItems[entity.ReferenceId.Value])
                    : false,
            _ => throw new ArgumentOutOfRangeException(entity.Type.ToString()),
        };
    }

    private bool IsCompletable(ToDoEntity entity)
    {
        return entity.Type switch
        {
            ToDoItemType.Value => true,
            ToDoItemType.Group => false,
            ToDoItemType.Planned => true,
            ToDoItemType.Periodicity => false,
            ToDoItemType.PeriodicityOffset => false,
            ToDoItemType.Circle => true,
            ToDoItemType.Step => true,
            ToDoItemType.Reference => false,
            _ => throw new ArgumentOutOfRangeException(entity.Type.ToString()),
        };
    }

    private bool IsGroup(ToDoEntity entity)
    {
        return entity.Type switch
        {
            ToDoItemType.Value => false,
            ToDoItemType.Group => true,
            ToDoItemType.Planned => false,
            ToDoItemType.Periodicity => false,
            ToDoItemType.PeriodicityOffset => false,
            ToDoItemType.Circle => false,
            ToDoItemType.Step => false,
            ToDoItemType.Reference => false,
            _ => throw new ArgumentOutOfRangeException(entity.Type.ToString()),
        };
    }

    private ToDoShortItem? ToActiveToDoItem(ToDoEntity entity)
    {
        return entity.ParentId is null
            ? null
            : entity.ToToDoShortItem();
    }
}