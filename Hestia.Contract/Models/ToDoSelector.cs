namespace Hestia.Contract.Models;

public sealed class ToDoSelector
{
    public ShortToDo Item { get; set; } = new();
    public ToDoSelector[] Children { get; set; } = [];
}
