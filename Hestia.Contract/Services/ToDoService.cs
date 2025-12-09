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
        var fullDictionary = new Dictionary<Guid, FullToDoItem>();
        var roots = dictionary.Values.Where(x => x.ParentId is null).ToArray();


        if (request.IsSelectorItems)
        {
            response.SelectorItems = new()
            {
                IsResponse = true,
                Items = roots.Select(x => new ToDoSelectorItem
                {
                    Item = x.ToToDoShortItem(),
                    Children = GetToDoSelectorItems(items, x.Id).ToArray(),
                }).ToArray(),
            };
        }

        if (request.ToStringItems.Length != 0)
        {
            foreach (var item in request.ToStringItems)
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

                    response.ToStringItems.Add(new()
                    {
                        Id = id,
                        Text = builder.ToString().Trim(),
                    });
                }
            }
        }

        if (request.IsCurrentActiveItem)
        {
            var rootsFullItems = roots
               .Select(i =>
                    GetFullItem(dictionary, fullDictionary, i,
                        _gaiaValues.Offset))
               .OrderBy(x => x.Item.OrderIndex).ToArray();

            foreach (var rootsFullItem in rootsFullItems)
            {
                if (rootsFullItem.Status == ToDoItemStatus.Miss)
                {
                    response.CurrentActive = rootsFullItem.Active;

                    break;
                }

                switch (rootsFullItem.Status)
                {
                    case ToDoItemStatus.ReadyForComplete:
                        if (response.CurrentActive is null)
                        {
                            response.CurrentActive = rootsFullItem.Active;
                        }

                        break;
                    case ToDoItemStatus.Planned:
                        break;
                    case ToDoItemStatus.Completed:
                        break;
                    case ToDoItemStatus.ComingSoon:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        if (request.ActiveItems.Length != 0)
        {
            response.ActiveItems = request.ActiveItems
               .Select(
                    x => GetFullItem(dictionary, fullDictionary, dictionary[x],
                        _gaiaValues.Offset)
                )
               .Select(x =>
                    new ActiveItem
                    {
                        Id = x.Item.Id,
                        Item = x.Active,
                    }).ToArray();
        }

        if (request.IsFavoriteItems)
        {
            response.FavoriteItems = new()
            {
                IsResponse = true,
                Items = dictionary.Where(x => x.Value.IsFavorite)
                   .ToArray()
                   .Select(x =>
                        GetFullItem(dictionary, fullDictionary, x.Value,
                            _gaiaValues.Offset)).ToArray(),
            };
        }

        if (request.IsBookmarkItems)
        {
            response.BookmarkItems = new()
            {
                IsResponse = true,
                Items = dictionary.Where(x => x.Value.IsBookmark)
                   .Select(x => x.Value.ToToDoShortItem())
                   .ToArray(),
            };
        }

        if (request.ChildrenItems.Length != 0)
        {
            response.ChildrenItems = request.ChildrenItems
               .Select(
                    id => new ChildrenItem
                    {
                        Id = id,
                        Children = dictionary.Values
                           .Where(x => x.ParentId == id)
                           .ToArray()
                           .Select(
                                item => GetFullItem(dictionary, fullDictionary,
                                    item, _gaiaValues.Offset)
                            ).ToArray(),
                    }
                ).ToArray();
        }

        if (request.LeafItems.Length != 0)
        {
            response.LeafItems = request.LeafItems
               .Select(
                    id => new LeafItem
                    {
                        Id = id,
                        Leafs = GetLeafToDoItems(
                                dictionary,
                                fullDictionary,
                                dictionary[id],
                                new(),
                                _gaiaValues.Offset
                            )
                           .ToArray(),
                    }
                ).ToArray();
        }

        var isEmptySearchText = request.Search.SearchText.IsNullOrWhiteSpace();

        if (!isEmptySearchText || request.Search.Types.Length != 0)
        {
            response.SearchItems = new()
            {
                IsResponse = true,
                Items = dictionary.Values
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
                            _gaiaValues.Offset)).ToArray(),
            };
        }

        if (request.ParentItems.Length != 0)
        {
            response.ParentItems = request.ParentItems
               .Select(
                    x => new ParentItem
                    {
                        Id = x,
                        Parents = GetParents(dictionary, x).Reverse().ToArray(),
                    }
                ).ToArray();
        }

        if (request.IsTodayItems)
        {
            var today = DateTimeOffset.UtcNow.Add(_gaiaValues.Offset).Date
               .ToDateOnly();

            response.TodayItems = new()
            {
                IsResponse = true,
                Items = dictionary.Values
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
                            _gaiaValues.Offset)).ToArray(),
            };
        }

        if (request.IsRootItems)
        {
            response.RootItems = new()
            {
                IsResponse = true,
                Items = roots.Select(x =>
                    GetFullItem(dictionary, fullDictionary, x,
                        _gaiaValues.Offset)).ToArray(),
            };
        }

        if (request.Items.Length != 0)
        {
            response.Items = new()
            {
                IsResponse = true,
                Items = request.Items
                   .Select(
                        x => GetFullItem(dictionary, fullDictionary,
                            dictionary[x], _gaiaValues.Offset)
                    ).ToArray(),
            };
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

    private List<ToDoSelectorItem> GetToDoSelectorItems(ToDoEntity[] items,
        Guid id)
    {
        var children = items.Where(x => x.ParentId == id)
           .OrderBy(x => x.OrderIndex).ToArray();

        var result = new List<ToDoSelectorItem>();

        for (var i = 0; i < children.Length; i++)
        {
            result.AddRange(GetToDoSelectorItems(items, children[i].Id));
        }

        return result;
    }

    private void ToDoItemToString(
        FrozenDictionary<Guid, ToDoEntity> allItems,
        Dictionary<Guid, FullToDoItem> fullToDoItems,
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

    private FullToDoItem GetFullItem(
        FrozenDictionary<Guid, ToDoEntity> allItems,
        Dictionary<Guid, FullToDoItem> fullToDoItems,
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

    private IEnumerable<FullToDoItem> GetLeafToDoItems(
        FrozenDictionary<Guid, ToDoEntity> allItems,
        Dictionary<Guid, FullToDoItem> fullToDoItems,
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

    private IEnumerable<ToDoShortItem> GetParents(
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