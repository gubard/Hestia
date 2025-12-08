namespace Hestia.Contract.Models;

public class ToDoFullItemsResponse
{
    public bool IsResponse { get; set; }
    public FullToDoItem[] Items { get; set; } = [];
}