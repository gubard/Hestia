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
using Nestor.Db.LiteDb.Services;
using Nestor.Db.Models;
using UltraLiteDB;

namespace Hestia.Db.Services;

public sealed class ToDoLiteDbService
    : LiteDbService<HestiaGetRequest, HestiaPostRequest, HestiaGetResponse, HestiaPostResponse>,
        IToDoDbService,
        IToDoDbCache
{
    public ToDoLiteDbService(
        IDatabaseFactory factory,
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
        var gaiaValues = _dbValuesFactory.Create();
        using var database = Factory.Create();
        var collection = database.GetToDoEntityCollection();
        var items = collection.FindAll().Select(x => x.ToToDoEntity()).ToArray();
        var response = CreateGetResponse(request, items, gaiaValues);

        return TaskHelper.FromResult(response);
    }

    public ConfiguredValueTaskAwaitable UpdateAsync(HestiaPostRequest source, CancellationToken ct)
    {
        return UpdateCore(source, ct).ConfigureAwait(false);
    }

    public ConfiguredValueTaskAwaitable UpdateAsync(HestiaGetResponse source, CancellationToken ct)
    {
        using var database = Factory.Create();
        var collection = database.GetToDoEntityCollection();
        var updateValues = GetToDoEntities(source);
        var entities = updateValues.Select(x => x.item).ToArray();

        if (entities.Length == 0)
        {
            return TaskHelper.ConfiguredCompletedTask;
        }

        var exists = entities
            .Where(x => collection.Exists(Query.EQ("_id", x.Id)))
            .Select(x => x.Id)
            .ToArray();

        var updates = updateValues
            .Where(x => exists.Contains(x.item.Id))
            .Select(x =>
            {
                var bsonDocument = x.item.ToBsonDocument();

                if (!x.isUpdateIsComplited)
                {
                    var d = collection.FindById(x.item.Id);

                    bsonDocument[nameof(ToDoEntity.IsCompleted)] = d[
                        nameof(ToDoEntity.IsCompleted)
                    ];
                }

                return bsonDocument;
            })
            .ToArray();

        var inserts = entities
            .Where(x => !exists.Contains(x.Id))
            .Select(x => x.ToBsonDocument())
            .ToArray();

        if (inserts.Length != 0)
        {
            collection.Insert(inserts);
        }

        if (updates.Length != 0)
        {
            collection.Update(updates);
        }

        if (source.Selectors is not null)
        {
            var ids = source
                .Selectors.SelectMany(x => GetToDoEntities(x).Select(y => y.Id))
                .ToArray();

            var deleteIds = collection
                .Find(Query.Not(Query.In("_id", ids.Select(x => new BsonValue(x)))))
                .Select(x => x["_id"])
                .ToArray();

            if (deleteIds.Length != 0)
            {
                collection.Delete(Query.In("_id", deleteIds));
            }
        }

        database.SaveChanges();

        return TaskHelper.ConfiguredCompletedTask;
    }

    private async ValueTask UpdateCore(HestiaPostRequest source, CancellationToken ct)
    {
        await ExecuteAsync(Guid.NewGuid(), new(), source, ct);
    }

    protected override ConfiguredValueTaskAwaitable ExecuteAsync(
        Guid idempotentId,
        HestiaPostResponse response,
        HestiaPostRequest request,
        CancellationToken ct
    )
    {
        var gaiaValues = _dbValuesFactory.Create();
        var fullDictionary = new Dictionary<Guid, FullToDo>();
        var edits = new AutoDictionary<Guid, EditToDoEntity>();
        using var database = Factory.Create();
        var collection = database.GetToDoEntityCollection();
        var options = _factoryOptions.Create();
        Create(database, collection, options, idempotentId, response, request.Creates, gaiaValues);

        var allItems = collection
            .FindAll()
            .Select(x => x.ToToDoEntity())
            .ToDictionary(x => x.Id)
            .ToFrozenDictionary();

        CloneItems(database, options, idempotentId, allItems, request.Clones, gaiaValues);
        Edit(request.Edits, edits);
        ChangeOrder(database, collection, request.ChangeOrders, response.ValidationErrors, edits);
        SwitchComplete(request.SwitchCompleteIds, allItems, fullDictionary, edits, gaiaValues);
        RandomizeChildrenOrderIndex(request.RandomizeChildrenOrderIndexIds, allItems, edits);

        database.EditEntities(
            gaiaValues.UserId.ToString(),
            idempotentId,
            options.IsUseEvents,
            edits.ToItemsArray()
        );

        Delete(database, options, idempotentId, request.DeleteIds, allItems, edits, gaiaValues, ct);
        database.SaveChanges();

        return TaskHelper.ConfiguredCompletedTask;
    }

    private readonly IFactory<DbValues> _dbValuesFactory;
    private readonly ToDoParametersFillerService _toDoParametersFillerService;
    private readonly IToDoValidator _toDoValidator;
    private readonly IFactory<DbServiceOptions> _factoryOptions;

    private void CloneItems(
        IDatabase database,
        DbServiceOptions options,
        Guid idempotentId,
        FrozenDictionary<Guid, ToDoEntity> allEntities,
        CloneToDoItem[] cloneItems,
        DbValues dbValues
    )
    {
        var items = cloneItems
            .Select(x =>
                x.CloneIds.Select(y => Clone(allEntities, allEntities[y], x.ParentId))
                    .SelectMany(y => y)
            )
            .SelectMany(x => x)
            .ToArray();

        database.AddEntities(dbValues.UserId.ToString(), idempotentId, options.IsUseEvents, items);
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

    private void Delete(
        IDatabase database,
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
            return;
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

        database.DeleteEntities(
            dbValues.UserId.ToString(),
            idempotentId,
            options.IsUseEvents,
            allIds
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

    private void ChangeOrder(
        IDatabase database,
        UltraLiteCollection<BsonDocument> collection,
        ChangeOrder[] changeOrders,
        List<ValidationError> errors,
        AutoDictionary<Guid, EditToDoEntity> edits
    )
    {
        if (changeOrders.Length == 0)
        {
            return;
        }

        var allInsertIds = changeOrders.SelectMany(x => x.InsertIds).Distinct().ToArray();

        var insertItems = collection
            .Find(Query.In("_id", allInsertIds.Select(x => new BsonValue(x))))
            .Select(x => x.ToToDoEntity())
            .ToArray();

        var insertItemsDictionary = insertItems.ToDictionary(x => x.Id).ToFrozenDictionary();
        var startIds = changeOrders.Select(x => x.StartId).Distinct().ToArray();

        var startItems = collection
            .Find(Query.In("_id", startIds.Select(x => new BsonValue(x))))
            .Select(x => x.ToToDoEntity())
            .ToArray();

        var startItemsDictionary = startItems.ToDictionary(x => x.Id).ToFrozenDictionary();

        var parentItems = startItems
            .Select(x => x.ParentId)
            .WhereNotNullStruct()
            .Distinct()
            .ToArray();

        var allSiblings = collection
            .Find(Query.In(nameof(ToDoEntity.ParentId), parentItems.Select(x => new BsonValue(x))))
            .Select(x => x.ToToDoEntity())
            .ToArray();

        if (startItems.Any(x => x.ParentId is null))
        {
            allSiblings = collection
                .Find(Query.EQ(nameof(ToDoEntity.ParentId), BsonValue.Null))
                .Select(x => x.ToToDoEntity())
                .Concat(allSiblings)
                .ToArray();
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

    private void Create(
        IDatabase database,
        UltraLiteCollection<BsonDocument> collection,
        DbServiceOptions options,
        Guid idempotentId,
        HestiaPostResponse response,
        ShortToDo[] creates,
        DbValues dbValues
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

            var query = entity.ParentId is null
                ? Query.EQ(nameof(ToDoEntity.ParentId), BsonValue.Null)
                : Query.EQ(nameof(ToDoEntity.ParentId), entity.ParentId);

            var siblingCount = collection.Count(query);
            entity.OrderIndex = (uint)siblingCount + 1;
        }

        database.AddEntities(
            dbValues.UserId.ToString(),
            idempotentId,
            options.IsUseEvents,
            adds.ToArray()
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

        if (request.IsFull)
        {
            foreach (var root in roots)
            {
                GetFullItem(dictionary, fullDictionary, root, dbValues.Offset);
            }

            response.Full = fullDictionary.Values.ToArray();
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

    private static (bool isUpdateIsComplited, ToDoEntity item)[] GetToDoEntities(
        HestiaGetResponse source
    )
    {
        return source
            .Items.Select(x => (true, x.ToToDoEntity()))
            .Concat(source.Children.SelectMany(x => x.Value).Select(x => (true, x.ToToDoEntity())))
            .Concat(source.Search.Select(x => (true, x.ToToDoEntity())))
            .Concat(source.Today.Select(x => (true, x.ToToDoEntity())))
            .Concat(
                source.Leafs.SelectMany(x => x.Value).Select(x => (true, x.Item.ToToDoEntity()))
            )
            .Concat(
                source.Roots?.Select(x => (true, x.Item.ToToDoEntity()))
                    ?? Enumerable.Empty<(bool, ToDoEntity)>()
            )
            .Concat(
                source.Favorites?.Select(x => (true, x.Item.ToToDoEntity()))
                    ?? Enumerable.Empty<(bool, ToDoEntity)>()
            )
            .Concat(source.Parents.SelectMany(x => x.Value).Select(x => (false, x.ToToDoEntity())))
            .Concat(
                source.Selectors?.SelectMany(GetToDoEntities).Select(x => (false, x))
                    ?? Enumerable.Empty<(bool, ToDoEntity)>()
            )
            .Concat(
                source.Bookmarks?.Select(x => (false, x.ToToDoEntity()))
                    ?? Enumerable.Empty<(bool, ToDoEntity)>()
            )
            .GroupBy(x => x.Item2.Id)
            .Select(x => x.Any(y => y.Item1) ? x.First(y => y.Item1) : x.First())
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
