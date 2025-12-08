using Gaia.Models;
using Gaia.Services;

namespace Hestia.Contract.Models;

public class HestiaPostResponse : IValidationErrors
{
    public List<ValidationError> ValidationErrors { get; } = [];
}