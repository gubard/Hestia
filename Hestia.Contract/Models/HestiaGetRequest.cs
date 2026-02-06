namespace Hestia.Contract.Models;

public class HestiaGetRequest
{
    public bool IsGetSelectors { get; set; }
    public GetToStringItem[] ToStringIds { get; set; } = [];
    public bool IsCurrentActive { get; set; }
    public Guid[] ActiveItems { get; set; } = [];
    public bool IsFavorites { get; set; } = true;
    public bool IsBookmarks { get; set; } = true;
    public Guid[] ChildrenIds { get; set; } = [];
    public Guid[] LeafIds { get; set; } = [];
    public GetSearch Search { get; set; } = new();
    public Guid[] ParentIds { get; set; } = [];
    public bool IsToday { get; set; }
    public bool IsRoots { get; set; }
    public Guid[] Items { get; set; } = [];
}
