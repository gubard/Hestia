namespace Hestia.Contract.Models;

public class ParentItem
{
    public Guid Id { get; set; }
    public ToDoShortItem[] Parents { get; set; } = [];
}