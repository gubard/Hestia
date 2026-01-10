using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Gaia.Helpers;
using Gaia.Models;
using Gaia.Services;
using Hestia.Contract.Helpers;
using Hestia.Contract.Models;
using Microsoft.Data.Sqlite;
using Nestor.Db.Helpers;
using Nestor.Db.Models;
using Nestor.Db.Services;

namespace Hestia.Contract.Services;

public interface IToDoService
    : IService<HestiaGetRequest, HestiaPostRequest, HestiaGetResponse, HestiaPostResponse>;

public interface IHttpToDoService
    : IToDoService,
        IHttpService<HestiaGetRequest, HestiaPostRequest, HestiaGetResponse, HestiaPostResponse>;

public interface IDbToDoService
    : IToDoService,
        IDbService<HestiaGetRequest, HestiaPostRequest, HestiaGetResponse, HestiaPostResponse>;

public sealed class DbToDoService
    : DbService<HestiaGetRequest, HestiaPostRequest, HestiaGetResponse, HestiaPostResponse>,
        IDbToDoService
{
    private readonly GaiaValues _gaiaValues;
    private readonly ToDoParametersFillerService _toDoParametersFillerService;
    private readonly IToDoValidator _toDoValidator;
    private readonly IFactory<DbServiceOptions> _factoryOptions;

    public DbToDoService(
        IDbConnectionFactory factory,
        GaiaValues gaiaValues,
        ToDoParametersFillerService toDoParametersFillerService,
        IToDoValidator toDoValidator,
        IFactory<DbServiceOptions> factoryOptions
    )
        : base(factory)
    {
        _gaiaValues = gaiaValues;
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

    private async ValueTask<HestiaGetResponse> GetCore(
        HestiaGetRequest request,
        CancellationToken ct
    )
    {
        await using var session = await Factory.CreateSessionAsync(ct);
        var items = await session.GetToDosAsync(ToDosExt.SelectQuery, ct);
        var response = CreateGetResponse(request, items);

        if (request.LastId != -1)
        {
            await using var reader = await session.ExecuteReaderAsync(
                new(
                    $"{EventsExt.SelectQuery} WHERE Id > @LastId",
                    new SqliteParameter[] { new("@LastId", request.LastId) }
                ),
                ct
            );

            response.Events = (await reader.ReadEventsAsync(ct).ToEnumerableAsync()).ToArray();
        }

        return response;
    }

    protected override ConfiguredValueTaskAwaitable<HestiaPostResponse> ExecuteAsync(
        Guid idempotentId,
        HestiaPostRequest request,
        CancellationToken ct
    )
    {
        return PostCore(idempotentId, request, ct).ConfigureAwait(false);
    }

    private async ValueTask<HestiaPostResponse> PostCore(
        Guid idempotentId,
        HestiaPostRequest request,
        CancellationToken ct
    )
    {
        var response = new HestiaPostResponse();
        Dictionary<Guid, FullToDo> fullDictionary = new();
        var editEntities = new List<EditToDoEntity>();
        var session = await Factory.CreateSessionAsync(ct);
        var options = _factoryOptions.Create();
        await CreateAsync(session, options, idempotentId, response, request.Creates, ct);
        Edit(request.Edits, editEntities);
        ChangeOrder(session, request.ChangeOrder, response.ValidationErrors, editEntities);

        var allItems = (await session.GetToDosAsync(ToDosExt.SelectQuery, ct))
            .ToDictionary(x => x.Id)
            .ToFrozenDictionary();

        SwitchComplete(request.SwitchCompleteIds, allItems, fullDictionary, editEntities);
        RandomizeChildrenOrderIndex(request.RandomizeChildrenOrderIndexIds, allItems, editEntities);

        await session.EditEntitiesAsync(
            _gaiaValues.UserId.ToString(),
            idempotentId,
            options.IsUseEvents,
            editEntities.ToArray(),
            ct
        );

        await DeleteAsync(session, options, idempotentId, request.DeleteIds, ct);
        await session.CommitAsync(ct);

        return response;
    }

    protected override HestiaPostResponse Execute(Guid idempotentId, HestiaPostRequest request)
    {
        var response = new HestiaPostResponse();
        Dictionary<Guid, FullToDo> fullDictionary = new();
        var editEntities = new List<EditToDoEntity>();
        var session = Factory.CreateSession();
        var options = _factoryOptions.Create();
        Create(session, options, idempotentId, response, request.Creates);
        Edit(request.Edits, editEntities);
        ChangeOrder(session, request.ChangeOrder, response.ValidationErrors, editEntities);

        var allItems = session
            .GetToDos(ToDosExt.SelectQuery)
            .ToDictionary(x => x.Id)
            .ToFrozenDictionary();

        SwitchComplete(request.SwitchCompleteIds, allItems, fullDictionary, editEntities);
        RandomizeChildrenOrderIndex(request.RandomizeChildrenOrderIndexIds, allItems, editEntities);

        session.EditEntities(
            _gaiaValues.UserId.ToString(),
            idempotentId,
            options.IsUseEvents,
            editEntities.ToArray()
        );

        Delete(session, options, idempotentId, request.DeleteIds);
        session.Commit();

        return response;
    }

    public override HestiaGetResponse Get(HestiaGetRequest request)
    {
        using var session = Factory.CreateSession();
        var items = session.GetToDos(ToDosExt.SelectQuery);
        var response = CreateGetResponse(request, items);

        if (request.LastId != -1)
        {
            using var reader = session.ExecuteReader(
                new(
                    $"{EventsExt.SelectQuery} WHERE Id > @LastId",
                    new SqliteParameter[] { new("@LastId", request.LastId) }
                )
            );

            response.Events = reader.ReadEvents().ToArray();
        }

        return response;
    }

    private void RandomizeChildrenOrderIndex(
        Guid[] ids,
        FrozenDictionary<Guid, ToDoEntity> allEntities,
        List<EditToDoEntity> editEntities
    )
    {
        foreach (var id in ids)
        {
            var children = allEntities
                .Values.Where(x => x.ParentId == id)
                .ForEach(x =>
                    x.OrderIndex = BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(4))
                )
                .ToArray();

            editEntities.AddRange(
                children.Select(
                    (child, index) =>
                        new EditToDoEntity(child.Id)
                        {
                            IsEditOrderIndex = true,
                            OrderIndex = (uint)index + 1,
                        }
                )
            );
        }
    }

    private void SwitchComplete(
        Guid[] ids,
        FrozenDictionary<Guid, ToDoEntity> allItems,
        Dictionary<Guid, FullToDo> fullDictionary,
        List<EditToDoEntity> editToDoEntities
    )
    {
        foreach (var id in ids)
        {
            var item = allItems[id];

            var parameters = _toDoParametersFillerService.GetToDoItemParameters(
                allItems,
                fullDictionary,
                item,
                _gaiaValues.Offset
            );

            if (parameters.IsCan == ToDoIsCan.None)
            {
                continue;
            }

            if (item.IsCompleted && parameters.IsCan == ToDoIsCan.CanIncomplete)
            {
                editToDoEntities.Add(new(id) { IsEditIsCompleted = true, IsCompleted = false });
            }
            else if (!item.IsCompleted && parameters.IsCan == ToDoIsCan.CanComplete)
            {
                switch (item.Type)
                {
                    case ToDoType.Circle:
                    case ToDoType.Step:
                    case ToDoType.Value:
                    case ToDoType.FixedDate:
                        editToDoEntities.Add(
                            new(id) { IsEditIsCompleted = true, IsCompleted = true }
                        );

                        break;
                    case ToDoType.Group:
                    case ToDoType.Periodicity:
                    case ToDoType.PeriodicityOffset:
                    case ToDoType.Reference:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                MoveNextDueDate(item, allItems, editToDoEntities);
                CircleCompletionAsync(allItems, item, true, false, false, editToDoEntities);
                StepCompletionAsync(allItems, item, false, editToDoEntities);
            }
        }
    }

    private void StepCompletionAsync(
        FrozenDictionary<Guid, ToDoEntity> allItems,
        ToDoEntity item,
        bool completeTask,
        List<EditToDoEntity> editToDoEntities
    )
    {
        var steps = allItems
            .Where(x => x.Value.ParentId == item.Id && x.Value.Type == ToDoType.Step)
            .Select(x => x.Value)
            .ToArray();

        foreach (var step in steps)
        {
            step.IsCompleted = completeTask;
        }

        var groups = allItems
            .Where(x => x.Value.ParentId == item.Id && x.Value.Type == ToDoType.Group)
            .Select(x => x.Value)
            .ToArray();

        foreach (var group in groups)
        {
            StepCompletionAsync(allItems, group, completeTask, editToDoEntities);
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
                    StepCompletionAsync(allItems, reference, completeTask, editToDoEntities);
                    continue;
                case ToDoType.FixedDate:
                case ToDoType.Periodicity:
                case ToDoType.PeriodicityOffset:
                case ToDoType.Circle:
                    continue;
                case ToDoType.Step:
                    editToDoEntities.Add(
                        new(referenceId) { IsCompleted = completeTask, IsEditIsCompleted = true }
                    );
                    continue;
                case ToDoType.Reference:
                    continue;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    private void CircleCompletionAsync(
        FrozenDictionary<Guid, ToDoEntity> allItems,
        ToDoEntity item,
        bool moveCircleOrderIndex,
        bool completeTask,
        bool onlyCompletedTasks,
        List<EditToDoEntity> editToDoEntities
    )
    {
        var circles = allItems
            .Where(x => x.Value.ParentId == item.Id && x.Value.Type == ToDoType.Circle)
            .Select(x => x.Value)
            .OrderBy(x => x.OrderIndex)
            .ToArray();

        if (circles.Any() && (!onlyCompletedTasks || circles.All(x => x.IsCompleted)))
        {
            var nextOrderIndex = item.CurrentCircleOrderIndex;

            if (moveCircleOrderIndex)
            {
                var next = circles.FirstOrDefault(x => x.OrderIndex > item.CurrentCircleOrderIndex);

                nextOrderIndex = next?.OrderIndex ?? circles[0].OrderIndex;
                item.CurrentCircleOrderIndex = nextOrderIndex;
            }

            foreach (var circle in circles)
            {
                if (completeTask)
                {
                    editToDoEntities.Add(
                        new(circle.Id) { IsCompleted = true, IsEditIsCompleted = true }
                    );
                }
                else
                {
                    editToDoEntities.Add(
                        new(circle.Id)
                        {
                            IsCompleted = circle.OrderIndex != nextOrderIndex,
                            IsEditIsCompleted = true,
                        }
                    );
                }
            }
        }

        var groups = allItems
            .Where(x => x.Value.ParentId == item.Id && x.Value.Type == ToDoType.Group)
            .Select(x => x.Value)
            .ToArray();

        foreach (var group in groups)
        {
            CircleCompletionAsync(
                allItems,
                group,
                moveCircleOrderIndex,
                completeTask,
                onlyCompletedTasks,
                editToDoEntities
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
                    CircleCompletionAsync(
                        allItems,
                        reference,
                        moveCircleOrderIndex,
                        completeTask,
                        onlyCompletedTasks,
                        editToDoEntities
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
        List<EditToDoEntity> editToDoEntities
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
                AddPeriodicity(item, editToDoEntities);
                return;
            case ToDoType.PeriodicityOffset:
                AddPeriodicityOffset(item, editToDoEntities);
                return;
            case ToDoType.Reference:
                if (!item.ReferenceId.HasValue)
                {
                    return;
                }

                MoveNextDueDate(allEntities[item.ReferenceId.Value], allEntities, editToDoEntities);

                return;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void AddPeriodicity(ToDoEntity item, List<EditToDoEntity> editToDoEntities)
    {
        var currentDueDate = item.IsRequiredCompleteInDueDate
            ? item.DueDate
            : DateTimeOffset.UtcNow.Add(_gaiaValues.Offset).Date.ToDateOnly();

        switch (item.TypeOfPeriodicity)
        {
            case TypeOfPeriodicity.Daily:
                editToDoEntities.Add(
                    new(item.Id) { IsEditDueDate = true, DueDate = currentDueDate.AddDays(1) }
                );
                break;
            case TypeOfPeriodicity.Weekly:
            {
                var dayOfWeek = currentDueDate.DayOfWeek;
                var daysOfWeek = item.GetDaysOfWeek()
                    .OrderBy(x => x)
                    .Select(x => (DayOfWeek?)x)
                    .ToArray();
                var nextDay = daysOfWeek.FirstOrDefault(x => x > dayOfWeek);

                editToDoEntities.Add(
                    new(item.Id)
                    {
                        IsEditDueDate = true,
                        DueDate = nextDay is not null
                            ? currentDueDate.AddDays((int)nextDay - (int)dayOfWeek)
                            : currentDueDate.AddDays(
                                7 - (int)dayOfWeek + (int)daysOfWeek.First().ThrowIfNullStruct()
                            ),
                    }
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

                editToDoEntities.Add(
                    new(item.Id)
                    {
                        IsEditDueDate = true,
                        DueDate = nextDay is not null
                            ? item.DueDate.WithDay(Math.Min(nextDay.Value, daysInCurrentMonth))
                            : item
                                .DueDate.AddMonths(1)
                                .WithDay(
                                    Math.Min(
                                        daysOfMonth.First().ThrowIfNullStruct(),
                                        daysInNextMonth
                                    )
                                ),
                    }
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

                editToDoEntities.Add(
                    new(item.Id)
                    {
                        IsEditDueDate = true,
                        DueDate = nextDay is not null
                            ? item
                                .DueDate.WithMonth((byte)nextDay.Month)
                                .WithDay(
                                    Math.Min(
                                        DateTime.DaysInMonth(
                                            currentDueDate.Year,
                                            (byte)nextDay.Month
                                        ),
                                        nextDay.Day
                                    )
                                )
                            : item
                                .DueDate.AddYears(1)
                                .WithMonth((byte)daysOfYear.First().ThrowIfNull().Month)
                                .WithDay(
                                    Math.Min(daysInNextMonth, daysOfYear.First().ThrowIfNull().Day)
                                ),
                    }
                );

                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void AddPeriodicityOffset(ToDoEntity item, List<EditToDoEntity> editToDoEntities)
    {
        if (item.IsRequiredCompleteInDueDate)
        {
            editToDoEntities.Add(
                new(item.Id)
                {
                    IsEditDueDate = true,
                    DueDate = item
                        .DueDate.AddDays(item.DaysOffset + item.WeeksOffset * 7)
                        .AddMonths(item.MonthsOffset)
                        .AddYears(item.YearsOffset),
                }
            );
        }
        else
        {
            editToDoEntities.Add(
                new(item.Id)
                {
                    IsEditDueDate = true,
                    DueDate = DateTimeOffset
                        .UtcNow.Add(_gaiaValues.Offset)
                        .Date.ToDateOnly()
                        .AddDays(item.DaysOffset + item.WeeksOffset * 7)
                        .AddMonths(item.MonthsOffset)
                        .AddYears(item.YearsOffset),
                }
            );
        }
    }

    private ConfiguredValueTaskAwaitable DeleteAsync(
        DbSession session,
        DbServiceOptions options,
        Guid idempotentId,
        Guid[] ids,
        CancellationToken ct
    )
    {
        if (ids.Length == 0)
        {
            return TaskHelper.ConfiguredCompletedTask;
        }

        return session.DeleteEntitiesAsync(
            _gaiaValues.UserId.ToString(),
            idempotentId,
            options.IsUseEvents,
            ids,
            ct
        );
    }

    private void Delete(DbSession session, DbServiceOptions options, Guid idempotentId, Guid[] ids)
    {
        if (ids.Length == 0)
        {
            return;
        }

        session.DeleteEntities(
            _gaiaValues.UserId.ToString(),
            idempotentId,
            options.IsUseEvents,
            ids
        );
    }

    private void ChangeOrder(
        DbSession session,
        ToDoChangeOrder[] changeOrders,
        List<ValidationError> errors,
        List<EditToDoEntity> editEntities
    )
    {
        if (changeOrders.Length == 0)
        {
            return;
        }

        var insertIds = changeOrders.SelectMany(x => x.InsertIds).Distinct().ToArray();
        var insertItems = session.GetToDos(insertIds);
        var insertItemsDictionary = insertItems.ToDictionary(x => x.Id).ToFrozenDictionary();
        var startIds = changeOrders.Select(x => x.StartId).Distinct().ToArray();
        var startItems = session.GetToDos(startIds);
        var startItemsDictionary = startItems.ToDictionary(x => x.Id).ToFrozenDictionary();
        var parentItems = startItems.Select(x => x.ParentId).WhereNotNull().Distinct().ToArray();
        var siblings = session.GetToDos(parentItems);

        for (var index = 0; index < changeOrders.Length; index++)
        {
            var changeOrder = changeOrders[index];

            var inserts = changeOrder.InsertIds.Select(x => insertItemsDictionary[x]).ToFrozenSet();

            if (!startItemsDictionary.TryGetValue(changeOrder.StartId, out var item))
            {
                errors.Add(new NotFoundValidationError(changeOrder.StartId.ToString()));

                continue;
            }

            var startIndex = changeOrder.IsAfter ? item.OrderIndex + 1 : item.OrderIndex;
            var items = siblings.Where(x => x.ParentId == item.ParentId).OrderBy(x => x.OrderIndex);

            var usedItems = changeOrder.IsAfter
                ? items.Where(x => x.OrderIndex > item.OrderIndex)
                : items.Where(x => x.OrderIndex >= item.OrderIndex);

            var newOrder = inserts
                .Concat(usedItems.Where(x => !insertIds.Contains(x.Id)))
                .ToFrozenSet();

            foreach (var newItem in newOrder)
            {
                editEntities.Add(
                    new(newItem.Id)
                    {
                        IsEditOrderIndex = startIndex != newItem.OrderIndex,
                        OrderIndex = startIndex++,
                        IsEditParentId = newItem.ParentId != item.ParentId,
                        ParentId = item.ParentId,
                    }
                );
            }
        }
    }

    private void Edit(EditToDos[] edits, List<EditToDoEntity> editEntities)
    {
        foreach (var edit in edits)
        {
            editEntities.AddRange(edit.ToEditToDoEntities());
        }
    }

    private void Create(
        DbSession session,
        DbServiceOptions options,
        Guid idempotentId,
        HestiaPostResponse response,
        ShortToDo[] creates
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

            adds.Add(create.ToToDoEntity());
        }

        session.AddEntities(
            _gaiaValues.UserId.ToString(),
            idempotentId,
            options.IsUseEvents,
            adds.ToArray()
        );
    }

    private ConfiguredValueTaskAwaitable CreateAsync(
        DbSession session,
        DbServiceOptions options,
        Guid idempotentId,
        HestiaPostResponse response,
        ShortToDo[] creates,
        CancellationToken ct
    )
    {
        if (creates.Length == 0)
        {
            return TaskHelper.ConfiguredCompletedTask;
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

            adds.Add(create.ToToDoEntity());
        }

        return session.AddEntitiesAsync(
            _gaiaValues.UserId.ToString(),
            idempotentId,
            options.IsUseEvents,
            adds.ToArray(),
            ct
        );
    }

    private HestiaGetResponse CreateGetResponse(HestiaGetRequest request, ToDoEntity[] items)
    {
        var response = new HestiaGetResponse();
        var dictionary = items.ToDictionary(x => x.Id).ToFrozenDictionary();
        var fullDictionary = new Dictionary<Guid, FullToDo>();
        var roots = dictionary.Values.Where(x => x.ParentId is null).ToArray();

        if (request.IsSelectors)
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
                        _gaiaValues.Offset
                    );

                    response.ToStrings.Add(id, builder.ToString().Trim());
                }
            }
        }

        if (request.IsCurrentActive)
        {
            response.CurrentActive.IsResponse = true;
            var rootsFullItems = roots
                .Select(i => GetFullItem(dictionary, fullDictionary, i, _gaiaValues.Offset))
                .OrderBy(x => x.Parameters.OrderIndex)
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
                .Select(x => GetFullItem(dictionary, fullDictionary, x.Value, _gaiaValues.Offset))
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
                            GetFullItem(dictionary, fullDictionary, item, _gaiaValues.Offset)
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
                            _gaiaValues.Offset
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
                .Select(x => GetFullItem(dictionary, fullDictionary, x, _gaiaValues.Offset))
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
            var today = DateTimeOffset.UtcNow.Add(_gaiaValues.Offset).Date.ToDateOnly();

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
                .Select(x => GetFullItem(dictionary, fullDictionary, x, _gaiaValues.Offset))
                .ToArray();
        }

        if (request.IsRoots)
        {
            response.Roots = roots
                .Select(x => GetFullItem(dictionary, fullDictionary, x, _gaiaValues.Offset))
                .ToArray();
        }

        if (request.Items.Length != 0)
        {
            response.Items = request
                .Items.Select(x =>
                    GetFullItem(dictionary, fullDictionary, dictionary[x], _gaiaValues.Offset)
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
}
