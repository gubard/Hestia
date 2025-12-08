namespace Hestia.Contract.Models;

public class ToDoShortItemsResponse
{
    public bool IsResponse { get; set; }
    public ToDoShortItem[] Items { get; set; } = [];
}