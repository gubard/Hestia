namespace Hestia.Contract.Models;

public sealed class CloneToDoItem
{
    public Guid? ParentId { get; set; }
    public Guid[] CloneIds { get; set; } = [];
}
