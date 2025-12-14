namespace Hestia.Contract.Models;

public class ToDoChangeOrder
{
    public Guid StartId { get; set; }
    public Guid[] InsertIds { get; set; } = [];
    public  bool IsAfter{ get; set; }
}