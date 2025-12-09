namespace Hestia.Contract.Models;

public class ToDoFullsResponse
{
    public bool IsResponse { get; set; }
    public FullToDo[] Items { get; set; } = [];
}