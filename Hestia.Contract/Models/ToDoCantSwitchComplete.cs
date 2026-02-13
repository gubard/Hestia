using Gaia.Models;

namespace Hestia.Contract.Models;

public sealed class ToDoCantSwitchComplete : IdentityValidationError
{
    public ToDoCantSwitchComplete(string identity)
        : base(identity) { }
}
