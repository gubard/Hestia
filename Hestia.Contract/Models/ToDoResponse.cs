namespace Hestia.Contract.Models;

public class ToDoResponse
{
    public ToDoSelectorItemsResponse SelectorItems { get; set; }
    public ToStringItem[] ToStringItems { get; set; } = [];
    public ToDoShortItem? CurrentActive { get; set; }
    public ActiveItem[] ActiveItems { get; set; } = [];
    public ToDoFullItemsResponse FavoriteItems { get; set; }
    public ToDoShortItemsResponse BookmarkItems { get; set; }
    public ChildrenItem[] ChildrenItems { get; set; } = [];
    public LeafItem[] LeafItems { get; set; } = [];
    public ToDoFullItemsResponse SearchItems { get; set; }
    public ParentItem[] ParentItems { get; set; } = [];
    public ToDoFullItemsResponse TodayItems { get; set; }
    public ToDoFullItemsResponse RootItems { get; set; }
    public ToDoFullItemsResponse Items { get; set; }
}