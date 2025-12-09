namespace Hestia.Contract.Models;

public class ToDoSelector
{
    public ShortToDo Item { get; set; } = new();
    public ToDoSelector[] Children { get; set; } = [];
}