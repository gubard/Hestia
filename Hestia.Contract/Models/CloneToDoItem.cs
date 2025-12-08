namespace Hestia.Contract.Models;

public class CloneToDoItem
{
    public Guid ParentId { get; set; }
    public Guid[] CloneIds { get; set; } = [];
}