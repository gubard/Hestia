namespace Hestia.Contract.Models;

public sealed class ToDoItemParameters
{
    public ShortToDo? ActiveItem { get; set; }
    public ToDoStatus Status { get; set; }
    public ToDoIsCanDo IsCanDo { get; set; }
}
