namespace Hestia.Contract.Models;

public sealed class ShortToDoResponse
{
    public bool HasResponse { get; set; }
    public ShortToDo? Item { get; set; }
}
