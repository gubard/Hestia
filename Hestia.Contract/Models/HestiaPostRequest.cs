using Gaia.Models;
using Nestor.Db.Models;

namespace Hestia.Contract.Models;

public sealed class HestiaPostRequest : IPostRequest, IDragChangeOrder<EditToDos>
{
    public Guid[] DeleteIds { get; set; } = [];
    public Guid[] RandomizeChildrenOrderIndexIds { get; set; } = [];
    public ResetToDoItemOptions[] Resets { get; set; } = [];
    public ChangeOrder[] ChangeOrders { get; set; } = [];
    public EditToDos[] Edits { get; set; } = [];
    public Guid[] SwitchCompleteIds { get; set; } = [];
    public ShortToDo[] Creates { get; set; } = [];
    public CloneToDoItem[] Clones { get; set; } = [];
    public long LastLocalId { get; set; }
    public EventEntity[] Events { get; set; } = [];
}
