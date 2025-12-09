using System.Collections.Frozen;
using System.Text;
using Gaia.Helpers;
using Gaia.Models;
using Gaia.Services;
using Hestia.Contract.Helpers;
using Hestia.Contract.Models;
using Microsoft.EntityFrameworkCore;
using Nestor.Db.Models;
using Nestor.Db.Services;

namespace Hestia.Contract.Services;

public interface IToDoService : IService<HestiaGetRequest, HestiaPostRequest,
    HestiaGetResponse, HestiaPostResponse>;

public interface IHttpToDoService : IToDoService;

public interface IEfToDoService : IToDoService,
    IEfService<HestiaGetRequest, HestiaPostRequest, HestiaGetResponse,
        HestiaPostResponse>;

public sealed class EfToDoService :
    EfService<HestiaGetRequest, HestiaPostRequest, HestiaGetResponse,
        HestiaPostResponse>, IEfToDoService
{
    private readonly GaiaValues _gaiaValues;
    private readonly ToDoParametersFillerService _toDoParametersFillerService;

    public EfToDoService(DbContext dbContext, GaiaValues gaiaValues,
        ToDoParametersFillerService toDoParametersFillerService) : base(
        dbContext)
    {
        _gaiaValues = gaiaValues;
        _toDoParametersFillerService = toDoParametersFillerService;
    }

    public override async ValueTask<HestiaGetResponse> GetAsync(
        HestiaGetRequest request, CancellationToken ct)
    {
        var response = new HestiaGetResponse();
        var items =
            await ToDoEntity.GetToDoEntitysAsync(DbContext.Set<EventEntity>(),
                ct);
        var dictionary = items.ToDictionary(x => x.Id).ToFrozenDictionary();
        var fullDictionary = new Dictionary<Guid, FullToDo>();
        var roots = dictionary.Values.Where(x => x.ParentId is null).ToArray();


        if (request.IsSelectors)
        {
            response.Selectors = roots.Select(x => new ToDoSelector
            {
                Item = x.ToToDoShortItem(),
                Children = GetToDoSelectorItems(items, x.Id).ToArray(),
            }).ToArray();
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
                        new()
                        {
                            Id = id,
                            Statuses = item.Statuses,
                        },
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
               .Select(i =>
                    GetFullItem(dictionary, fullDictionary, i,
                        _gaiaValues.Offset))
               .OrderBy(x => x.Item.OrderIndex).ToArray();

            foreach (var rootsFullItem in rootsFullItems)
            {
                if (rootsFullItem.Status == ToDoItemStatus.Miss)
                {
                    response.CurrentActive.Item = rootsFullItem.Active;

                    break;
                }

                switch (rootsFullItem.Status)
                {
                    case ToDoItemStatus.ReadyForComplete:
                        response.CurrentActive.Item ??= rootsFullItem.Active;

                        break;
                    case ToDoItemStatus.Planned:
                    case ToDoItemStatus.Completed:
                    case ToDoItemStatus.ComingSoon:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        if (request.IsFavorites)
        {
            response.Favorites = dictionary.Where(x => x.Value.IsFavorite)
               .ToArray()
               .Select(x =>
                    GetFullItem(dictionary, fullDictionary, x.Value,
                        _gaiaValues.Offset)).ToArray();
        }

        if (request.IsBookmarks)
        {
            response.Bookmarks = dictionary.Where(x => x.Value.IsBookmark)
               .Select(x => x.Value.ToToDoShortItem())
               .ToArray();
        }

        if (request.ChildrenIds.Length != 0)
        {
            foreach (var id in request.ChildrenIds)
            {
                response.Children.Add(id, dictionary.Values
                   .Where(x => x.ParentId == id)
                   .ToArray()
                   .Select(
                        item => GetFullItem(dictionary, fullDictionary,
                            item, _gaiaValues.Offset)
                    ).ToArray());
            }
        }

        if (request.LeafIds.Length != 0)
        {
            foreach (var id in request.LeafIds)
            {
                response.Leafs.Add(id, GetLeafToDoItems(
                        dictionary,
                        fullDictionary,
                        dictionary[id],
                        new(),
                        _gaiaValues.Offset
                    )
                   .ToArray());
            }
        }

        var isEmptySearchText = request.Search.SearchText.IsNullOrWhiteSpace();

        if (!isEmptySearchText || request.Search.Types.Length != 0)
        {
            response.Search = dictionary.Values
               .Where(
                    x => isEmptySearchText
                     || x.Name.Contains(
                            request.Search.SearchText,
                            StringComparison.InvariantCultureIgnoreCase
                        )
                )
               .Where(
                    x => request.Search.Types.Length == 0
                     || request.Search.Types.Contains(x.Type)
                )
               .ToArray()
               .Select(x =>
                    GetFullItem(dictionary, fullDictionary, x,
                        _gaiaValues.Offset)).ToArray();
        }

        if (request.ParentIds.Length != 0)
        {
            foreach (var id in request.ParentIds)
            {
                response.Parents.Add(id,
                    GetParents(dictionary, id).Reverse().ToArray());
            }
        }

        if (request.IsToday)
        {
            var today = DateTimeOffset.UtcNow.Add(_gaiaValues.Offset).Date
               .ToDateOnly();

            response.Today = dictionary.Values
               .Where(
                    x => x is
                        {
                            Type: ToDoItemType.Periodicity
                            or ToDoItemType.PeriodicityOffset,
                        }
                     && (x.DueDate <= today
                         || x.RemindDaysBefore != 0
                         && today
                         >= x.DueDate.AddDays((int)-x.RemindDaysBefore))
                     || x is
                        {
                            Type: ToDoItemType.Planned, IsCompleted: false,
                        }
                     && (x.DueDate <= today
                         || x.RemindDaysBefore != 0
                         && today
                         >= x.DueDate.AddDays((int)-x.RemindDaysBefore))
                )
               .ToArray()
               .Select(x =>
                    GetFullItem(dictionary, fullDictionary, x,
                        _gaiaValues.Offset)).ToArray();
        }

        if (request.IsRoots)
        {
            response.Roots = roots.Select(x =>
                GetFullItem(dictionary, fullDictionary, x,
                    _gaiaValues.Offset)).ToArray();
        }

        if (request.Items.Length != 0)
        {
            response.Items = request.Items
               .Select(
                    x => GetFullItem(dictionary, fullDictionary,
                        dictionary[x], _gaiaValues.Offset)
                ).ToArray();
        }

        if (request.LastId != -1)
        {
            response.Events = await DbContext.Set<EventEntity>()
               .Where(x => x.Id > request.LastId)
               .ToArrayAsync(ct);
        }

        return response;
    }

    public override ValueTask<HestiaPostResponse> PostAsync(
        HestiaPostRequest request, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    private List<ToDoSelector> GetToDoSelectorItems(ToDoEntity[] items,
        Guid id)
    {
        var children = items.Where(x => x.ParentId == id)
           .OrderBy(x => x.OrderIndex).ToArray();

        var result = new List<ToDoSelector>();

        for (var i = 0; i < children.Length; i++)
        {
            result.AddRange(GetToDoSelectorItems(items, children[i].Id));
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
        var items = allItems.Values
           .Where(x => x.ParentId == options.Id)
           .OrderBy(x => x.OrderIndex)
           .ToArray();

        foreach (var item in items)
        {
            var parameters = GetFullItem(allItems, fullToDoItems, item, offset);

            if (!options.Statuses.Select(x => x)
                   .Contains(parameters.Status))
            {
                continue;
            }

            builder.Duplicate(" ", level);
            builder.Append(item.Name);
            builder.AppendLine();

            ToDoItemToString(
                allItems,
                fullToDoItems,
                new()
                {
                    Id = item.Id,
                    Statuses = options.Statuses,
                },
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

        var parameters = _toDoParametersFillerService
           .GetToDoItemParameters(allItems, fullToDoItems, entity, offset);

        return entity.ToFullToDoItem(parameters);
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

        if (entity.Type == ToDoItemType.Reference)
        {
            ignoreIds.Add(entity.Id);

            if (entity.ReferenceId is null)
            {
                yield return GetFullItem(allItems, fullToDoItems, entity,
                    offset);

                yield break;
            }

            var reference = allItems[entity.ReferenceId.Value];

            foreach (var item in GetLeafToDoItems(
                         allItems,
                         fullToDoItems,
                         reference,
                         ignoreIds,
                         offset
                     ))
            {
                yield return item;
            }

            yield break;
        }

        var entities = allItems.Values.Where(x => x.ParentId == entity.Id)
           .OrderBy(x => x.OrderIndex).ToArray();

        if (entities.Length == 0)
        {
            yield return GetFullItem(allItems, fullToDoItems, entity, offset);

            yield break;
        }

        foreach (var e in entities)
        {
            foreach (var item in GetLeafToDoItems(
                         allItems,
                         fullToDoItems,
                         e,
                         ignoreIds,
                         offset
                     ))
            {
                yield return item;
            }
        }
    }

    private IEnumerable<ShortToDo> GetParents(
        FrozenDictionary<Guid, ToDoEntity> allItems, Guid id)
    {
        var parent = allItems[id];

        yield return parent.ToToDoShortItem();

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