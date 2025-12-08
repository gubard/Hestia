namespace Hestia.Contract.Models;

public class GetToStringItem
{
    public Guid[] Ids { get; set; } = [];
    public ToDoItemStatus[] Statuses { get; set; } = [];
}