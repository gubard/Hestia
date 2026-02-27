using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Gaia.Helpers;
using Gaia.Models;
using Gaia.Services;
using Hestia.Contract.Helpers;
using Hestia.Contract.Models;
using Hestia.Contract.Services;
using Nestor.Db.Helpers;
using Nestor.Db.Models;
using Nestor.Db.Services;

namespace Hestia.Db.Services;

public sealed class ToDoDbService
    : DbService<HestiaGetRequest, HestiaPostRequest, HestiaGetResponse, HestiaPostResponse>,
        IToDoDbService,
        IToDoDbCache
{
    public ToDoDbService(
        IDbConnectionFactory factory,
        IFactory<DbValues> dbValuesFactory,
        ToDoParametersFillerService toDoParametersFillerService,
        IToDoValidator toDoValidator,
        IFactory<DbServiceOptions> factoryOptions
    )
        : base(factory, nameof(ToDoEntity))
    {
        _dbValuesFactory = dbValuesFactory;
        _toDoParametersFillerService = toDoParametersFillerService;
        _toDoValidator = toDoValidator;
        _factoryOptions = factoryOptions;
    }

    public override ConfiguredValueTaskAwaitable<HestiaGetResponse> GetAsync(
        HestiaGetRequest request,
        CancellationToken ct
    )
    {
        return GetCore(request, ct).ConfigureAwait(false);
    }

    public ConfiguredValueTaskAwaitable UpdateAsync(HestiaPostRequest source, CancellationToken ct)
    {
        return UpdateCore(source, ct).ConfigureAwait(false);
    }

    public ConfiguredValueTaskAwaitable UpdateAsync(HestiaGetResponse source, CancellationToken ct)
    {
        return UpdateCore(source, ct).ConfigureAwait(false);
    }

    private async ValueTask UpdateCore(HestiaPostRequest source, CancellationToken ct)
    {
        await ExecuteAsync(Guid.NewGuid(), new(), source, ct);
    }

    private async ValueTask UpdateCore(HestiaGetResponse source, CancellationToken ct)
    {
        await using var session = await Factory.CreateSessionAsync(ct);
        var entities = GetToDoEntities(source);

        if (entities.Length == 0)
        {
            return;
        }

        var exists = await session.IsExistsAsync(entities, ct);

        var updateQueries = entities
            .Where(x => exists.Contains(x.Id))
            .Select(x => x.CreateUpdateToDosQuery())
            .ToArray();

        var inserts = entities.Where(x => !exists.Contains(x.Id)).ToArray();

        if (inserts.Length != 0)
        {
            await session.ExecuteNonQueryAsync(inserts.CreateInsertQuery(), ct);
        }

        foreach (var query in updateQueries)
        {
            await session.ExecuteNonQueryAsync(query, ct);
        }

        if (source.Selectors is not null)
        {
            var ids = source
                .Selectors.SelectMany(x => GetToDoEntities(x).Select(y => y.Id))
                .ToArray();

            var deleteIds = await session.GetGuidAsync(
                new(
                    ToDosExt.SelectIdsQuery + $" WHERE Id NOT IN ({ids.ToParameterNames("Id")})",
                    ids.ToQueryParameters("Id")
                ),
                ct
            );

            if (deleteIds.Length != 0)
            {
                await session.ExecuteNonQueryAsync(deleteIds.CreateDeleteToDosQuery(), ct);
            }
        }

        await session.CommitAsync(ct);
    }

    protected override ConfiguredValueTaskAwaitable ExecuteAsync(
        Guid idempotentId,
        HestiaPostResponse response,
        HestiaPostRequest request,
        CancellationToken ct
    )
    {
        return ExecuteCore(idempotentId, response, request, ct).ConfigureAwait(false);
    }

    private readonly IFactory<DbValues> _dbValuesFactory;
    private readonly ToDoParametersFillerService _toDoParametersFillerService;
    private readonly IToDoValidator _toDoValidator;
    private readonly IFactory<DbServiceOptions> _factoryOptions;

    private async ValueTask UpdateChildrenOrderIndexAsync(
        FrozenDictionary<Guid, ToDoEntity> allEntities,
        HestiaPostRequest request,
        Guid idempotentId,
        CancellationToken ct
    )
    {
        var gaiaValues = _dbValuesFactory.Create();
        await using var session = await Factory.CreateSessionAsync(ct);
        var options = _factoryOptions.Create();
        var result = new List<EditToDoEntity>();

        var sibling = request
            .DeleteIds.Concat(
                request.Edits.Where(x => x.IsEditParentId).Select(x => x.Ids).SelectMany(x => x)
            )
            .Distinct()
            .ToArray();

        if (sibling.Length == 0)
        {
            return;
        }

        var ids = allEntities
            .Values.Where(x => sibling.Contains(x.Id))
            .Select(x => x.ParentId)
            .Concat(request.Clones.Select(x => x.ParentId))
            .Concat(request.Edits.Where(x => x.IsEditParentId).Select(x => x.ParentId))
            .Distinct()
            .ToArray();

        foreach (var id in ids)
        {
            var children = allEntities
                .Values.Where(x => x.ParentId == id)
                .OrderBy(x => x.OrderIndex)
                .ToArray();

            for (var index = 0; index < children.Length; index++)
            {
                var child = children[index];

                if (child.OrderIndex == (uint)index + 1)
                {
                    continue;
                }

                result.Add(new(child.Id) { IsEditOrderIndex = true, OrderIndex = (uint)index + 1 });
            }
        }

        await session.EditEntitiesAsync(
            gaiaValues.UserId.ToString(),
            idempotentId,
            options.IsUseEvents,
            result.ToArray(),
            ct
        );

        await session.CommitAsync(ct);
    }

    private async ValueTask<HestiaGetResponse> GetCore(
        HestiaGetRequest request,
        CancellationToken ct
    )
    {
        var gaiaValues = _dbValuesFactory.Create();
        await using var session = await Factory.CreateSessionAsync(ct);
        var items = await session.GetToDosAsync(ToDosExt.SelectQuery, ct);
        var response = CreateGetResponse(request, items, gaiaValues);

        return response;
    }

    private async ValueTask ExecuteCore(
        Guid idempotentId,
        HestiaPostResponse response,
        HestiaPostRequest request,
        CancellationToken ct
    )
    {
        var gaiaValues = _dbValuesFactory.Create();
        var fullDictionary = new Dictionary<Guid, FullToDo>();
        var edits = new AutoDictionary<Guid, EditToDoEntity>();
        await using var session = await Factory.CreateSessionAsync(ct);
        var options = _factoryOptions.Create();

        await CreateAsync(
            session,
            options,
            idempotentId,
            response,
            request.Creates,
            gaiaValues,
            ct
        );

        var allItems = (await session.GetToDosAsync(ToDosExt.SelectQuery, ct))
            .ToDictionary(x => x.Id)
            .ToFrozenDictionary();

        await CloneItemsAsync(
            session,
            options,
            idempotentId,
            allItems,
            request.Clones,
            gaiaValues,
            ct
        );

        Edit(request.Edits, edits);
        await ChangeOrderAsync(session, request.ChangeOrders, response.ValidationErrors, edits, ct);
        SwitchComplete(request.SwitchCompleteIds, allItems, fullDictionary, edits, gaiaValues);
        RandomizeChildrenOrderIndex(request.RandomizeChildrenOrderIndexIds, allItems, edits);

        await session.EditEntitiesAsync(
            gaiaValues.UserId.ToString(),
            idempotentId,
            options.IsUseEvents,
            edits.ToItemsArray(),
            ct
        );

        await DeleteAsync(
            session,
            options,
            idempotentId,
            request.DeleteIds,
            allItems,
            edits,
            gaiaValues,
            ct
        );

        await session.CommitAsync(ct);
        await UpdateChildrenOrderIndexAsync(allItems, request, idempotentId, ct);
    }

    private ConfiguredValueTaskAwaitable CloneItemsAsync(
        DbSession session,
        DbServiceOptions options,
        Guid idempotentId,
        FrozenDictionary<Guid, ToDoEntity> allEntities,
        CloneToDoItem[] cloneItems,
        DbValues dbValues,
        CancellationToken ct
    )
    {
        var items = cloneItems
            .Select(x =>
                x.CloneIds.Select(y => Clone(allEntities, allEntities[y], x.ParentId))
                    .SelectMany(y => y)
            )
            .SelectMany(x => x)
            .ToArray();

        return session.AddEntitiesAsync(
            dbValues.UserId.ToString(),
            idempotentId,
            options.IsUseEvents,
            items,
            ct
        );
    }

    private IEnumerable<ToDoEntity> Clone(
        FrozenDictionary<Guid, ToDoEntity> allEntities,
        ToDoEntity entity,
        Guid? parentId
    )
    {
        var clone = allEntities[entity.Id].ToToDoShort().ToToDoEntity();
        clone.Id = Guid.NewGuid();
        clone.ParentId = parentId;

        yield return clone;

        var children = allEntities.Values.Where(x => x.ParentId == entity.Id).ToArray();

        foreach (var child in children)
        {
            foreach (var cloneChild in Clone(allEntities, child, clone.Id))
            {
                yield return cloneChild;
            }
        }
    }

    private void RandomizeChildrenOrderIndex(
        Guid[] ids,
        FrozenDictionary<Guid, ToDoEntity> allEntities,
        AutoDictionary<Guid, EditToDoEntity> edits
    )
    {
        foreach (var id in ids)
        {
            var children = allEntities.Values.Where(x => x.ParentId == id).ToArray();
            var newOrder = new Dictionary<Guid, uint>();

            for (var index = 0; index < children.Length; index++)
            {
                var child = children[index];
                newOrder[child.Id] = BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(4));
            }

            var newIds = newOrder.OrderBy(x => x.Value).Select(x => x.Key).ToArray();

            for (var index = 0; index < newIds.Length; index++)
            {
                var edit = edits.GetItem(newIds[index]);
                edit.IsEditOrderIndex = true;
                edit.OrderIndex = (uint)index + 1;
            }
        }
    }

    private void SwitchComplete(
        Guid[] ids,
        FrozenDictionary<Guid, ToDoEntity> allItems,
        Dictionary<Guid, FullToDo> fullDictionary,
        AutoDictionary<Guid, EditToDoEntity> edits,
        DbValues dbValues
    )
    {
        foreach (var id in ids)
        {
            var item = allItems[id];

            var parameters = _toDoParametersFillerService.GetToDoItemParameters(
                allItems,
                fullDictionary,
                item,
                dbValues.Offset
            );

            if (parameters.IsCanDo == ToDoIsCanDo.None)
            {
                continue;
            }

            if (item.IsCompleted && parameters.IsCanDo == ToDoIsCanDo.CanIncomplete)
            {
                var edit = edits.GetItem(id);
                edit.IsEditIsCompleted = true;
                edit.IsCompleted = false;
            }
            else if (!item.IsCompleted && parameters.IsCanDo == ToDoIsCanDo.CanComplete)
            {
                switch (item.Type)
                {
                    case ToDoType.Circle:
                    case ToDoType.Step:
                    case ToDoType.Value:
                    case ToDoType.FixedDate:
                        var edit = edits.GetItem(id);
                        edit.IsEditIsCompleted = true;
                        edit.IsCompleted = true;

                        break;
                    case ToDoType.Group:
                    case ToDoType.Periodicity:
                    case ToDoType.PeriodicityOffset:
                    case ToDoType.Reference:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                MoveNextDueDate(item, allItems, edits, dbValues);
                CircleCompletion(allItems, item, true, false, false, edits);
                StepCompletion(allItems, item, false, edits);
            }
        }
    }

    private void StepCompletion(
        FrozenDictionary<Guid, ToDoEntity> allItems,
        ToDoEntity item,
        bool completeTask,
        AutoDictionary<Guid, EditToDoEntity> edits
    )
    {
        var steps = allItems
            .Where(x => x.Value.ParentId == item.Id && x.Value.Type == ToDoType.Step)
            .Select(x => x.Value)
            .ToArray();

        foreach (var step in steps)
        {
            if (step.IsCompleted == completeTask)
            {
                continue;
            }

            var edit = edits.GetItem(step.Id);
            edit.IsEditIsCompleted = true;
            edit.IsCompleted = completeTask;
        }

        var groups = allItems
            .Where(x => x.Value.ParentId == item.Id && x.Value.Type == ToDoType.Group)
            .Select(x => x.Value)
            .ToArray();

        foreach (var group in groups)
        {
            StepCompletion(allItems, group, completeTask, edits);
        }

        var referenceIds = allItems
            .Where(x =>
                x.Value.ParentId == item.Id
                && x.Value.Type == ToDoType.Reference
                && x.Value.ReferenceId.HasValue
            )
            .Select(x => x.Value.ReferenceId.ThrowIfNullStruct())
            .ToArray();

        foreach (var referenceId in referenceIds)
        {
            var reference = allItems[referenceId];

            switch (reference.Type)
            {
                case ToDoType.Value:
                    continue;
                case ToDoType.Group:
                    StepCompletion(allItems, reference, completeTask, edits);

                    continue;
                case ToDoType.FixedDate:
                case ToDoType.Periodicity:
                case ToDoType.PeriodicityOffset:
                case ToDoType.Circle:
                    continue;
                case ToDoType.Step:
                    var edit = edits.GetItem(referenceId);
                    edit.IsEditIsCompleted = true;
                    edit.IsCompleted = completeTask;

                    continue;
                case ToDoType.Reference:
                    continue;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    private void CircleCompletion(
        FrozenDictionary<Guid, ToDoEntity> allItems,
        ToDoEntity item,
        bool moveCircleOrderIndex,
        bool completeTask,
        bool onlyCompletedTasks,
        AutoDictionary<Guid, EditToDoEntity> edits
    )
    {
        var circles = allItems
            .Where(x => x.Value.ParentId == item.Id && x.Value.Type == ToDoType.Circle)
            .Select(x => x.Value)
            .OrderBy(x => x.OrderIndex)
            .ToArray();

        if (circles.Any() && (!onlyCompletedTasks || circles.All(x => x.IsCompleted)))
        {
            var nextCurrentCircleOrderIndex = item.CurrentCircleOrderIndex;

            if (moveCircleOrderIndex)
            {
                var next = circles.FirstOrDefault(x => x.OrderIndex > item.CurrentCircleOrderIndex);
                nextCurrentCircleOrderIndex = next?.OrderIndex ?? circles[0].OrderIndex;
                var edit = edits.GetItem(item.Id);
                edit.IsEditCurrentCircleOrderIndex = true;
                edit.CurrentCircleOrderIndex = nextCurrentCircleOrderIndex;
            }

            foreach (var circle in circles)
            {
                var edit = edits.GetItem(circle.Id);
                edit.IsEditIsCompleted = true;

                if (completeTask)
                {
                    edit.IsCompleted = true;
                }
                else
                {
                    edit.IsCompleted = circle.OrderIndex != nextCurrentCircleOrderIndex;
                }
            }
        }

        var groups = allItems
            .Where(x => x.Value.ParentId == item.Id && x.Value.Type == ToDoType.Group)
            .Select(x => x.Value)
            .ToArray();

        foreach (var group in groups)
        {
            CircleCompletion(
                allItems,
                group,
                moveCircleOrderIndex,
                completeTask,
                onlyCompletedTasks,
                edits
            );
        }

        var referenceIds = allItems
            .Where(x =>
                x.Value.ParentId == item.Id
                && x.Value.Type == ToDoType.Reference
                && x.Value.ReferenceId.HasValue
            )
            .Select(x => x.Value.ReferenceId.ThrowIfNullStruct())
            .ToArray();

        foreach (var referenceId in referenceIds)
        {
            var reference = allItems[referenceId];

            switch (reference.Type)
            {
                case ToDoType.Value:
                    continue;
                case ToDoType.Group:
                    CircleCompletion(
                        allItems,
                        reference,
                        moveCircleOrderIndex,
                        completeTask,
                        onlyCompletedTasks,
                        edits
                    );

                    continue;
                case ToDoType.FixedDate:
                case ToDoType.Periodicity:
                case ToDoType.PeriodicityOffset:
                case ToDoType.Circle:
                case ToDoType.Step:
                case ToDoType.Reference:
                    continue;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    private void MoveNextDueDate(
        ToDoEntity item,
        FrozenDictionary<Guid, ToDoEntity> allEntities,
        AutoDictionary<Guid, EditToDoEntity> edits,
        DbValues dbValues
    )
    {
        switch (item.Type)
        {
            case ToDoType.Circle:
            case ToDoType.Step:
            case ToDoType.Value:
            case ToDoType.Group:
            case ToDoType.FixedDate:
            case ToDoType.Periodicity:
                AddPeriodicity(item, edits, dbValues);

                return;
            case ToDoType.PeriodicityOffset:
                AddPeriodicityOffset(item, edits, dbValues);

                return;
            case ToDoType.Reference:
                if (!item.ReferenceId.HasValue)
                {
                    return;
                }

                MoveNextDueDate(allEntities[item.ReferenceId.Value], allEntities, edits, dbValues);

                return;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void AddPeriodicity(
        ToDoEntity item,
        AutoDictionary<Guid, EditToDoEntity> edits,
        DbValues dbValues
    )
    {
        var currentDueDate = item.IsRequiredCompleteInDueDate
            ? item.DueDate
            : DateTimeOffset.UtcNow.Add(dbValues.Offset).Date.ToDateOnly();

        switch (item.TypeOfPeriodicity)
        {
            case TypeOfPeriodicity.Daily:
            {
                var edit = edits.GetItem(item.Id);
                edit.IsEditDueDate = true;
                edit.DueDate = currentDueDate.AddDays(1);

                break;
            }
            case TypeOfPeriodicity.Weekly:
            {
                var dayOfWeek = currentDueDate.DayOfWeek;

                var daysOfWeek = item.GetDaysOfWeek()
                    .OrderBy(x => x)
                    .Select(x => (DayOfWeek?)x)
                    .ToArray();

                var nextDay = daysOfWeek.FirstOrDefault(x => x > dayOfWeek);
                var edit = edits.GetItem(item.Id);
                edit.IsEditDueDate = true;

                edit.DueDate = nextDay is not null
                    ? currentDueDate.AddDays((int)nextDay - (int)dayOfWeek)
                    : currentDueDate.AddDays(
                        7 - (int)dayOfWeek + (int)daysOfWeek.First().ThrowIfNullStruct()
                    );

                break;
            }
            case TypeOfPeriodicity.Monthly:
            {
                var dayOfMonth = currentDueDate.Day;

                var daysOfMonth = item.GetDaysOfMonth()
                    .ToArray()
                    .Order()
                    .Select(x => (byte?)x)
                    .ToArray();

                var nextDay = daysOfMonth.FirstOrDefault(x => x > dayOfMonth);

                var daysInCurrentMonth = DateTime.DaysInMonth(
                    currentDueDate.Year,
                    currentDueDate.Month
                );

                var daysInNextMonth = DateTime.DaysInMonth(
                    currentDueDate.AddMonths(1).Year,
                    currentDueDate.AddMonths(1).Month
                );

                var edit = edits.GetItem(item.Id);
                edit.IsEditDueDate = true;

                edit.DueDate = nextDay is not null
                    ? item.DueDate.WithDay(Math.Min(nextDay.Value, daysInCurrentMonth))
                    : item
                        .DueDate.AddMonths(1)
                        .WithDay(
                            Math.Min(daysOfMonth.First().ThrowIfNullStruct(), daysInNextMonth)
                        );

                break;
            }
            case TypeOfPeriodicity.Annually:
            {
                var daysOfYear = item.GetDaysOfYear()
                    .OrderBy(x => x)
                    .Select(x => (DayOfYear?)x)
                    .ToArray();

                var nextDay = daysOfYear.FirstOrDefault(x =>
                    x.ThrowIfNull().Month >= (Month)currentDueDate.Month
                    && x.ThrowIfNull().Day > currentDueDate.Day
                );

                var daysInNextMonth = DateTime.DaysInMonth(
                    currentDueDate.Year + 1,
                    (byte)daysOfYear.First().ThrowIfNull().Month
                );

                var edit = edits.GetItem(item.Id);
                edit.IsEditDueDate = true;

                edit.DueDate = nextDay is not null
                    ? item
                        .DueDate.WithMonth((byte)nextDay.Month)
                        .WithDay(
                            Math.Min(
                                DateTime.DaysInMonth(currentDueDate.Year, (byte)nextDay.Month),
                                nextDay.Day
                            )
                        )
                    : item
                        .DueDate.AddYears(1)
                        .WithMonth((byte)daysOfYear.First().ThrowIfNull().Month)
                        .WithDay(Math.Min(daysInNextMonth, daysOfYear.First().ThrowIfNull().Day));

                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void AddPeriodicityOffset(
        ToDoEntity item,
        AutoDictionary<Guid, EditToDoEntity> edits,
        DbValues dbValues
    )
    {
        var edit = edits.GetItem(item.Id);
        edit.IsEditDueDate = true;

        if (item.IsRequiredCompleteInDueDate)
        {
            edit.DueDate = item
                .DueDate.AddDays(item.DaysOffset + item.WeeksOffset * 7)
                .AddMonths(item.MonthsOffset)
                .AddYears(item.YearsOffset);
        }
        else
        {
            edit.DueDate = DateTimeOffset
                .UtcNow.Add(dbValues.Offset)
                .Date.ToDateOnly()
                .AddDays(item.DaysOffset + item.WeeksOffset * 7)
                .AddMonths(item.MonthsOffset)
                .AddYears(item.YearsOffset);
        }
    }

    private ConfiguredValueTaskAwaitable DeleteAsync(
        DbSession session,
        DbServiceOptions options,
        Guid idempotentId,
        Guid[] ids,
        FrozenDictionary<Guid, ToDoEntity> allItems,
        AutoDictionary<Guid, EditToDoEntity> edits,
        DbValues dbValues,
        CancellationToken ct
    )
    {
        if (ids.Length == 0)
        {
            return TaskHelper.ConfiguredCompletedTask;
        }

        var allIds = GetChildrenIds(allItems, ids).ToArray();

        var referenceIds = allItems
            .Values.Where(x => x.ReferenceId.HasValue && allIds.Contains(x.ReferenceId.Value))
            .Select(x => x.Id)
            .Distinct()
            .ToArray();

        foreach (var referenceId in referenceIds)
        {
            var edit = edits.GetItem(referenceId);
            edit.IsEditReferenceId = true;
            edit.ReferenceId = null;
        }

        return session.DeleteEntitiesAsync(
            dbValues.UserId.ToString(),
            idempotentId,
            options.IsUseEvents,
            allIds,
            ct
        );
    }

    private IEnumerable<Guid> GetChildrenIds(
        FrozenDictionary<Guid, ToDoEntity> allItems,
        Guid[] ids
    )
    {
        foreach (var id in ids)
        {
            yield return id;

            var childrenIds = allItems
                .Where(x => x.Value.ParentId == id)
                .Select(x => x.Key)
                .ToArray();

            foreach (var childId in childrenIds)
            {
                yield return childId;
            }
        }
    }

    private async ValueTask ChangeOrderAsync(
        DbSession session,
        ChangeOrder[] changeOrders,
        List<ValidationError> errors,
        AutoDictionary<Guid, EditToDoEntity> edits,
        CancellationToken ct
    )
    {
        if (changeOrders.Length == 0)
        {
            return;
        }

        var allInsertIds = changeOrders.SelectMany(x => x.InsertIds).Distinct().ToArray();
        var insertItems = await session.GetToDosAsync(allInsertIds, ct);
        var insertItemsDictionary = insertItems.ToDictionary(x => x.Id).ToFrozenDictionary();
        var startIds = changeOrders.Select(x => x.StartId).Distinct().ToArray();
        var startItems = await session.GetToDosAsync(startIds, ct);
        var startItemsDictionary = startItems.ToDictionary(x => x.Id).ToFrozenDictionary();

        var parentItems = startItems
            .Select(x => x.ParentId)
            .WhereNotNullStruct()
            .Distinct()
            .ToArray();

        var allSiblings = await session.GetToDosAsync(
            new SqlQuery(
                ToDosExt.SelectQuery
                    + $" WHERE ParentId IN ({parentItems.ToParameterNames("ParentId")})",
                parentItems.ToQueryParameters("ParentId")
            ),
            ct
        );

        if (startItems.Any(x => x.ParentId is null))
        {
            var siblingsRoots = await session.GetToDosAsync(
                ToDosExt.SelectQuery + " WHERE ParentId IS NULL",
                ct
            );

            allSiblings = allSiblings.Concat(siblingsRoots).ToArray();
        }

        for (var index = 0; index < changeOrders.Length; index++)
        {
            var changeOrder = changeOrders[index];

            var inserts = changeOrder
                .InsertIds.Select(x => insertItemsDictionary[x])
                .OrderBy(x => x.OrderIndex)
                .ToFrozenSet();

            if (!startItemsDictionary.TryGetValue(changeOrder.StartId, out var item))
            {
                errors.Add(new NotFoundValidationError(changeOrder.StartId.ToString()));

                continue;
            }

            var siblings = allSiblings
                .Where(x => x.ParentId == item.ParentId && !changeOrder.InsertIds.Contains(x.Id))
                .OrderBy(x => x.OrderIndex)
                .ToList();

            var startItem = siblings.First(x => x.Id == changeOrder.StartId);
            var startIndex = siblings.IndexOf(startItem);
            siblings.InsertRange(changeOrder.IsAfter ? startIndex + 1 : startIndex, inserts);

            for (var i = 0; i < siblings.Count; i++)
            {
                var isEditOrderIndex = siblings[i].OrderIndex != i + 1;
                var isEditParentId = siblings[i].ParentId != startItem.ParentId;

                if (isEditOrderIndex || isEditParentId)
                {
                    var edit = edits.GetItem(siblings[i].Id);
                    edit.IsEditOrderIndex = isEditOrderIndex;
                    edit.IsEditParentId = isEditParentId;
                    edit.OrderIndex = (uint)i + 1;
                    edit.ParentId = item.ParentId;
                }
            }
        }
    }

    private void Edit(EditToDos[] edits, AutoDictionary<Guid, EditToDoEntity> editEntities)
    {
        foreach (var edit in edits)
        {
            editEntities.AddRange(edit.ToEditToDoEntities());
        }
    }

    private async ValueTask CreateAsync(
        DbSession session,
        DbServiceOptions options,
        Guid idempotentId,
        HestiaPostResponse response,
        ShortToDo[] creates,
        DbValues dbValues,
        CancellationToken ct
    )
    {
        if (creates.Length == 0)
        {
            return;
        }

        var adds = new List<ToDoEntity>();

        foreach (var create in creates)
        {
            var errorCount = response.ValidationErrors.Count;
            response.ValidationErrors.AddRange(
                _toDoValidator.Validate(create.Name, nameof(create.Name))
            );
            response.ValidationErrors.AddRange(
                _toDoValidator.Validate(create.Description, nameof(create.Description))
            );
            response.ValidationErrors.AddRange(
                _toDoValidator.Validate(create, nameof(create.DueDate))
            );
            response.ValidationErrors.AddRange(
                _toDoValidator.Validate(create, nameof(create.Link))
            );
            response.ValidationErrors.AddRange(
                _toDoValidator.Validate(create, nameof(create.AnnuallyDays))
            );
            response.ValidationErrors.AddRange(
                _toDoValidator.Validate(create, nameof(create.MonthlyDays))
            );
            response.ValidationErrors.AddRange(
                _toDoValidator.Validate(create, nameof(create.WeeklyDays))
            );
            response.ValidationErrors.AddRange(
                _toDoValidator.Validate(create, nameof(create.DaysOffset))
            );
            response.ValidationErrors.AddRange(_toDoValidator.Validate(create, "Reference"));

            if (errorCount != response.ValidationErrors.Count)
            {
                continue;
            }

            var entity = create.ToToDoEntity();
            adds.Add(entity);

            int siblingCount;

            if (entity.ParentId is null)
            {
                siblingCount = await session.ExecuteScalarInt32Async(
                    new(ToDosExt.SelectCountQuery + " WHERE ParentId IS NULL"),
                    ct
                );
            }
            else
            {
                siblingCount = await session.ExecuteScalarInt32Async(
                    new(
                        ToDosExt.SelectCountQuery + " WHERE ParentId = @ParentId",
                        new QueryParameter("@ParentId", entity.ParentId)
                    ),
                    ct
                );
            }

            entity.OrderIndex = (uint)siblingCount + 1;
        }

        await session.AddEntitiesAsync(
            dbValues.UserId.ToString(),
            idempotentId,
            options.IsUseEvents,
            adds.ToArray(),
            ct
        );
    }

    private HestiaGetResponse CreateGetResponse(
        HestiaGetRequest request,
        ToDoEntity[] items,
        DbValues dbValues
    )
    {
        var response = new HestiaGetResponse();
        var dictionary = items.ToDictionary(x => x.Id).ToFrozenDictionary();
        var fullDictionary = new Dictionary<Guid, FullToDo>();
        var roots = dictionary.Values.Where(x => x.ParentId is null).ToArray();

        if (request.IsGetSelectors)
        {
            response.Selectors = roots
                .Select(x => new ToDoSelector
                {
                    Item = x.ToToDoShort(),
                    Children = GetToDoSelectorItems(items, x.Id).ToArray(),
                })
                .ToArray();
        }

        if (request.ToStringIds.Length != 0)
        {
            foreach (var item in request.ToStringIds)
            {
                foreach (var id in item.Ids)
                {
                    var builder = new StringBuilder();

                    ToDoItemToString(
                        dictionary,
                        fullDictionary,
                        new() { Id = id, Statuses = item.Statuses },
                        0,
                        builder,
                        dbValues.Offset
                    );

                    response.ToStrings.Add(id, builder.ToString().Trim());
                }
            }
        }

        if (request.IsCurrentActive)
        {
            response.CurrentActive.HasResponse = true;
            var rootsFullItems = roots
                .Select(i => GetFullItem(dictionary, fullDictionary, i, dbValues.Offset))
                .OrderBy(x => x.Item.OrderIndex)
                .ToArray();

            foreach (var rootsFullItem in rootsFullItems)
            {
                if (rootsFullItem.Status == ToDoStatus.Miss)
                {
                    response.CurrentActive.Item = rootsFullItem.Active;

                    break;
                }

                switch (rootsFullItem.Status)
                {
                    case ToDoStatus.ReadyForComplete:
                        response.CurrentActive.Item ??= rootsFullItem.Active;

                        break;
                    case ToDoStatus.Planned:
                    case ToDoStatus.Completed:
                    case ToDoStatus.ComingSoon:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        if (request.IsFavorites)
        {
            response.Favorites = dictionary
                .Where(x => x.Value.IsFavorite)
                .ToArray()
                .Select(x => GetFullItem(dictionary, fullDictionary, x.Value, dbValues.Offset))
                .ToArray();
        }

        if (request.IsBookmarks)
        {
            response.Bookmarks = dictionary
                .Where(x => x.Value.IsBookmark)
                .Select(x => x.Value.ToToDoShort())
                .ToArray();
        }

        if (request.ChildrenIds.Length != 0)
        {
            foreach (var id in request.ChildrenIds)
            {
                response.Children.Add(
                    id,
                    dictionary
                        .Values.Where(x => x.ParentId == id)
                        .ToArray()
                        .Select(item =>
                            GetFullItem(dictionary, fullDictionary, item, dbValues.Offset)
                        )
                        .ToArray()
                );
            }
        }

        if (request.LeafIds.Length != 0)
        {
            foreach (var id in request.LeafIds)
            {
                response.Leafs.Add(
                    id,
                    GetLeafToDoItems(
                            dictionary,
                            fullDictionary,
                            dictionary[id],
                            new(),
                            dbValues.Offset
                        )
                        .ToArray()
                );
            }
        }

        var isEmptySearchText = request.Search.SearchText.IsNullOrWhiteSpace();

        if (!isEmptySearchText || request.Search.Types.Length != 0)
        {
            response.Search = dictionary
                .Values.Where(x =>
                    isEmptySearchText
                    || x.Name.Contains(
                        request.Search.SearchText,
                        StringComparison.InvariantCultureIgnoreCase
                    )
                )
                .Where(x =>
                    request.Search.Types.Length == 0 || request.Search.Types.Contains(x.Type)
                )
                .ToArray()
                .Select(x => GetFullItem(dictionary, fullDictionary, x, dbValues.Offset))
                .ToArray();
        }

        if (request.ParentIds.Length != 0)
        {
            foreach (var id in request.ParentIds)
            {
                response.Parents.Add(id, GetParents(dictionary, id).Reverse().ToArray());
            }
        }

        if (request.IsToday)
        {
            var today = DateTimeOffset.UtcNow.Add(dbValues.Offset).Date.ToDateOnly();

            response.Today = dictionary
                .Values.Where(x =>
                    x is { Type: ToDoType.Periodicity or ToDoType.PeriodicityOffset }
                        && (
                            x.DueDate <= today
                            || x.RemindDaysBefore != 0
                                && today >= x.DueDate.AddDays((int)-x.RemindDaysBefore)
                        )
                    || x is { Type: ToDoType.FixedDate, IsCompleted: false }
                        && (
                            x.DueDate <= today
                            || x.RemindDaysBefore != 0
                                && today >= x.DueDate.AddDays((int)-x.RemindDaysBefore)
                        )
                )
                .ToArray()
                .Select(x => GetFullItem(dictionary, fullDictionary, x, dbValues.Offset))
                .ToArray();
        }

        if (request.IsRoots)
        {
            response.Roots = roots
                .Select(x => GetFullItem(dictionary, fullDictionary, x, dbValues.Offset))
                .ToArray();
        }

        if (request.Items.Length != 0)
        {
            response.Items = request
                .Items.Select(x =>
                    GetFullItem(dictionary, fullDictionary, dictionary[x], dbValues.Offset)
                )
                .ToArray();
        }

        return response;
    }

    private ToDoSelector[] GetToDoSelectorItems(ToDoEntity[] items, Guid id)
    {
        var children = items.Where(x => x.ParentId == id).OrderBy(x => x.OrderIndex).ToArray();

        var result = new ToDoSelector[children.Length];

        for (var i = 0; i < children.Length; i++)
        {
            result[i] = new()
            {
                Item = children[i].ToToDoShort(),
                Children = GetToDoSelectorItems(items, children[i].Id),
            };
        }

        return result;
    }

    private void ToDoItemToString(
        FrozenDictionary<Guid, ToDoEntity> allItems,
        Dictionary<Guid, FullToDo> fullToDoItems,
        ToDoItemToStringOptions options,
        ushort level,
        StringBuilder builder,
        TimeSpan offset
    )
    {
        var items = allItems
            .Values.Where(x => x.ParentId == options.Id)
            .OrderBy(x => x.OrderIndex)
            .ToArray();

        foreach (var item in items)
        {
            var parameters = GetFullItem(allItems, fullToDoItems, item, offset);

            if (!options.Statuses.Select(x => x).Contains(parameters.Status))
            {
                continue;
            }

            builder.Duplicate(" ", level);
            builder.Append(item.Name);
            builder.AppendLine();

            ToDoItemToString(
                allItems,
                fullToDoItems,
                new() { Id = item.Id, Statuses = options.Statuses },
                (ushort)(level + 1),
                builder,
                offset
            );
        }
    }

    private FullToDo GetFullItem(
        FrozenDictionary<Guid, ToDoEntity> allItems,
        Dictionary<Guid, FullToDo> fullToDoItems,
        ToDoEntity entity,
        TimeSpan offset
    )
    {
        if (fullToDoItems.TryGetValue(entity.Id, out var value))
        {
            return value;
        }

        var parameters = _toDoParametersFillerService.GetToDoItemParameters(
            allItems,
            fullToDoItems,
            entity,
            offset
        );

        return entity.ToFullToDo(parameters);
    }

    private IEnumerable<FullToDo> GetLeafToDoItems(
        FrozenDictionary<Guid, ToDoEntity> allItems,
        Dictionary<Guid, FullToDo> fullToDoItems,
        ToDoEntity entity,
        List<Guid> ignoreIds,
        TimeSpan offset
    )
    {
        if (ignoreIds.Contains(entity.Id))
        {
            yield break;
        }

        if (entity.Type == ToDoType.Reference)
        {
            ignoreIds.Add(entity.Id);

            if (entity.ReferenceId is null)
            {
                yield return GetFullItem(allItems, fullToDoItems, entity, offset);

                yield break;
            }

            var reference = allItems[entity.ReferenceId.Value];

            foreach (
                var item in GetLeafToDoItems(allItems, fullToDoItems, reference, ignoreIds, offset)
            )
            {
                yield return item;
            }

            yield break;
        }

        var entities = allItems
            .Values.Where(x => x.ParentId == entity.Id)
            .OrderBy(x => x.OrderIndex)
            .ToArray();

        if (entities.Length == 0)
        {
            yield return GetFullItem(allItems, fullToDoItems, entity, offset);

            yield break;
        }

        foreach (var e in entities)
        {
            foreach (var item in GetLeafToDoItems(allItems, fullToDoItems, e, ignoreIds, offset))
            {
                yield return item;
            }
        }
    }

    private IEnumerable<ShortToDo> GetParents(FrozenDictionary<Guid, ToDoEntity> allItems, Guid id)
    {
        var parent = allItems[id];

        yield return parent.ToToDoShort();

        if (parent.ParentId is null)
        {
            yield break;
        }

        foreach (var item in GetParents(allItems, parent.ParentId.Value))
        {
            yield return item;
        }
    }

    private static ToDoEntity[] GetToDoEntities(HestiaGetResponse source)
    {
        return source
            .Children.SelectMany(x => x.Value)
            .Select(x => x.Item.ToToDoEntity())
            .Concat(source.Parents.SelectMany(x => x.Value).Select(x => x.ToToDoEntity()))
            .Concat(source.Items.Select(x => x.Item.ToToDoEntity()))
            .Concat(source.Search.Select(x => x.Item.ToToDoEntity()))
            .Concat(source.Today.Select(x => x.Item.ToToDoEntity()))
            .Concat(
                source.Selectors?.SelectMany(x => GetToDoEntities(x))
                    ?? Enumerable.Empty<ToDoEntity>()
            )
            .Concat(source.Leafs.SelectMany(x => x.Value).Select(x => x.Item.ToToDoEntity()))
            .Concat(
                source.Favorites?.Select(x => x.Item.ToToDoEntity())
                    ?? Enumerable.Empty<ToDoEntity>()
            )
            .Concat(
                source.Bookmarks?.Select(x => x.ToToDoEntity()) ?? Enumerable.Empty<ToDoEntity>()
            )
            .Concat(
                source.Roots?.Select(x => x.Item.ToToDoEntity()) ?? Enumerable.Empty<ToDoEntity>()
            )
            .GroupBy(x => x.Id)
            .Select(x => x.First())
            .ToArray();
    }

    private static IEnumerable<ToDoEntity> GetToDoEntities(ToDoSelector selector)
    {
        yield return selector.Item.ToToDoEntity();

        foreach (var child in selector.Children)
        {
            foreach (var item in GetToDoEntities(child))
            {
                yield return item;
            }
        }
    }
}
