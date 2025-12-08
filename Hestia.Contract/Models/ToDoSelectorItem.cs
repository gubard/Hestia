namespace Hestia.Contract.Models;

public class ToDoSelectorItem
{
    public ToDoShortItem Item { get; set; } = new();
    public ToDoSelectorItem[] Children { get; set; } = [];
}