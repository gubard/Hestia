namespace Hestia.Contract.Models;

public class HestiaPostRequest
{
    public Guid[] DeletedIds { get; set; } = [];
    public Guid[] RandomizeChildrenOrderIndexIds { get; set; } = [];
    public ResetToDoItemOptions[] Resets { get; set; } = [];
    public EditToDoItems[] Edits { get; set; } = [];
    public Guid[] SwitchCompleteIds { get; set; } = [];
    public UpdateOrderIndexToDoItemOptions[] UpdateOrderIndex { get; set; } = [];
    public ToDoShortItem[] Creates { get; set; } = [];
    public CloneToDoItem[] Clones { get; set; } = [];
}