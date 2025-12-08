namespace Hestia.Contract.Models;

public class LeafItem
{
    public Guid Id { get; set; }
    public FullToDoItem[] Leafs { get; set; } = [];
}