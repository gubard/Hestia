namespace Hestia.Contract.Models;

public class FullToDo
{
    public ShortToDo Parameters { get; set; } = new();
    public ToDoStatus Status { get; set; }
    public ShortToDo? Active { get; set; }
    public ToDoIsCan IsCan { get; set; }
}
