using Gaia.Models;
using Gaia.Services;

namespace Hestia.Contract.Models;

public class HestiaGetResponse : IValidationErrors
{
    public ToDoSelectorItemsResponse SelectorItems { get; set; } = new();
    public ToStringItem[] ToStringItems { get; set; } = [];
    public ToDoShortItem? CurrentActive { get; set; }
    public ActiveItem[] ActiveItems { get; set; } = [];
    public ToDoFullItemsResponse FavoriteItems { get; set; } = new();
    public ToDoShortItemsResponse BookmarkItems { get; set; } = new();
    public ChildrenItem[] ChildrenItems { get; set; } = [];
    public LeafItem[] LeafItems { get; set; } = [];
    public ToDoFullItemsResponse SearchItems { get; set; } = new();
    public ParentItem[] ParentItems { get; set; } = [];
    public ToDoFullItemsResponse TodayItems { get; set; } = new();
    public ToDoFullItemsResponse RootItems { get; set; } = new();
    public ToDoFullItemsResponse Items { get; set; } = new();
    public List<ValidationError> ValidationErrors { get; set; } = [];
}