namespace Hestia.Contract.Models;

public class FullToDo
{
    public ShortToDo Item { get; set; } = new();
    public ToDoItemStatus Status { get; set; }
    public ShortToDo? Active { get; set; }
    public ToDoItemIsCan IsCan { get; set; }
}