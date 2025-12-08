namespace Hestia.Contract.Models;

public class ChildrenItem
{
    public Guid Id { get; set; }
    public FullToDoItem[] Children { get; set; } = [];
}