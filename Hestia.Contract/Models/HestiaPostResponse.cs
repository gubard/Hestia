using Gaia.Models;
using Gaia.Services;
using Nestor.Db.Models;

namespace Hestia.Contract.Models;

public class HestiaPostResponse : IValidationErrors, IResponse
{
    public List<ValidationError> ValidationErrors { get; } = [];
    public EventEntity[] Events { get; set; } = [];
}
