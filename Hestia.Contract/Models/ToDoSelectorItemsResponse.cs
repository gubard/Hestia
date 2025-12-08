namespace Hestia.Contract.Models;

public class ToDoSelectorItemsResponse
{
    public bool IsResponse { get; set; }
    public ToDoSelectorItem[] Items { get; set; } = [];
}