using Gaia.Services;

namespace Hestia.Contract.Models;

public class HestiaPostRequest : IPostRequest
{
    public Guid[] DeleteIds { get; set; } = [];
    public Guid[] RandomizeChildrenOrderIndexIds { get; set; } = [];
    public ResetToDoItemOptions[] Resets { get; set; } = [];
    public EditToDos[] Edits { get; set; } = [];
    public Guid[] SwitchCompleteIds { get; set; } = [];
    public ToDoChangeOrder[] ChangeOrder { get; set; } = [];
    public ShortToDo[] Creates { get; set; } = [];
    public CloneToDoItem[] Clones { get; set; } = [];
    public long LastLocalId { get; set; }
}
