namespace Hestia.Contract.Models;

public sealed class GetToStringItem
{
    public Guid[] Ids { get; set; } = [];
    public ToDoStatus[] Statuses { get; set; } = [];
}
