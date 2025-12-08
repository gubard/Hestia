namespace Hestia.Contract.Models;

public class FullToDoItem
{
    public ToDoShortItem Item { get; set; } = new();
    public ToDoItemStatus Status { get; set; }
    public ToDoShortItem? Active { get; set; }
    public ToDoItemIsCan IsCan { get; set; }
}