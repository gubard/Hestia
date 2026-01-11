using Gaia.Models;
using Gaia.Services;
using Nestor.Db.Models;

namespace Hestia.Contract.Models;

public class HestiaPostResponse : IValidationErrors, IPostResponse
{
    public List<ValidationError> ValidationErrors { get; } = [];
    public EventEntity[] Events { get; set; } = [];
    public bool IsEventSaved { get; set; }
}
