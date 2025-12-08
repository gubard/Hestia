namespace Hestia.Contract.Models;

public class ToDoItemToStringOptions
{
    public Guid Id { get; set; }
    public ToDoItemStatus[] Statuses { get; set; } = [];
}