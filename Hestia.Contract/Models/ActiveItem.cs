namespace Hestia.Contract.Models;

public class ActiveItem
{
    public Guid Id { get; set; }
    public ToDoShortItem? Item { get; set; }
}