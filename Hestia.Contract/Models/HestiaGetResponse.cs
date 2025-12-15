using Gaia.Models;
using Gaia.Services;
using Nestor.Db.Models;

namespace Hestia.Contract.Models;

public class HestiaGetResponse : IValidationErrors, IResponse
{
    public ToDoSelector[]? Selectors { get; set; }
    public Dictionary<Guid, string> ToStrings { get; set; } = [];
    public ShortToDoResponse CurrentActive { get; set; } = new();
    public FullToDo[]? Favorites { get; set; }
    public ShortToDo[]? Bookmarks { get; set; }
    public Dictionary<Guid, FullToDo[]> Children { get; set; } = [];
    public Dictionary<Guid, FullToDo[]> Leafs { get; set; } = [];
    public FullToDo[] Search { get; set; } = [];
    public Dictionary<Guid, ShortToDo[]> Parents { get; set; } = [];
    public FullToDo[] Today { get; set; } = [];
    public FullToDo[]? Roots { get; set; }
    public FullToDo[] Items { get; set; } = [];
    public List<ValidationError> ValidationErrors { get; set; } = [];
    public EventEntity[] Events { get; set; } = [];
}
