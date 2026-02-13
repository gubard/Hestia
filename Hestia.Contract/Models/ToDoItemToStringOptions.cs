namespace Hestia.Contract.Models;

public sealed class ToDoItemToStringOptions
{
    public Guid Id { get; set; }
    public ToDoStatus[] Statuses { get; set; } = [];
}
