namespace Hestia.Contract.Models;

public class ToDoItemParameters
{
    public ShortToDo? ActiveItem { get; set; }
    public ToDoStatus Status { get; set; }
    public ToDoItemIsCan IsCan { get; set; }
}