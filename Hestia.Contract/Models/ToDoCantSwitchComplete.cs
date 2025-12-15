using Gaia.Models;

namespace Hestia.Contract.Models;

public class ToDoCantSwitchComplete : IdentityValidationError
{
    public ToDoCantSwitchComplete(string identity)
        : base(identity) { }
}
