namespace Hestia.Contract.Models;

public class ToDoItemParameters
{
    public ShortToDo? ActiveItem { get; set; }
    public ToDoItemStatus Status { get; set; }
    public ToDoItemIsCan IsCan { get; set; }
}