namespace Hestia.Contract.Models;

public class ResetToDoItemOptions
{
    public Guid Id { get; set; }
    public bool IsCompleteCurrentTask { get; set; }
    public bool IsCompleteChildrenTask { get; set; }
    public bool IsMoveCircleOrderIndex { get; set; }
    public bool IsOnlyCompletedTasks { get; set; }
}
