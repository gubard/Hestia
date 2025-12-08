namespace Hestia.Contract.Models;

public class GetToDo
{
    public bool IsSelectorItems { get; set; }
    public GetToStringItem[] ToStringItems { get; set; } = [];
    public bool IsCurrentActiveItem { get; set; }
    public Guid[] ActiveItems { get; set; } = [];
    public bool IsFavoriteItems { get; set; }
    public bool IsBookmarkItems { get; set; }
    public Guid[] ChildrenItems { get; set; } = [];
    public Guid[] LeafItems { get; set; } = [];
    public GetSearch Search { get; set; } = new();
    public Guid[] ParentItems { get; set; } = [];
    public bool IsTodayItems { get; set; }
    public bool IsRootItems { get; set; }
    public Guid[] Items { get; set; } = [];
}