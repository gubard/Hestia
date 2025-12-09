using Gaia.Services;

namespace Hestia.Contract.Models;

public class HestiaPostRequest : IPostRequest
{
    public Guid[] DeletedIds { get; set; } = [];
    public Guid[] RandomizeChildrenOrderIndexIds { get; set; } = [];
    public ResetToDoItemOptions[] Resets { get; set; } = [];
    public EditToDoItems[] Edits { get; set; } = [];
    public Guid[] SwitchCompleteIds { get; set; } = [];
    public UpdateOrderIndexToDoItemOptions[] UpdateOrderIndex { get; set; } = [];
    public ShortToDo[] Creates { get; set; } = [];
    public CloneToDoItem[] Clones { get; set; } = [];
    public long LastLocalId { get; set; }
}