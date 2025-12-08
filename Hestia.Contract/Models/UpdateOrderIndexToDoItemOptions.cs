namespace Hestia.Contract.Models;

public class UpdateOrderIndexToDoItemOptions
{
    public Guid Id { get; set; }
    public Guid TargetId { get; set; }
    public bool IsAfter { get; set; }
}