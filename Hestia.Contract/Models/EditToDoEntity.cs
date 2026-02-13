using Gaia.Models;
using Gaia.Services;

namespace Hestia.Contract.Models;

public sealed partial class EditToDoEntity : IStaticFactory<Guid, EditToDoEntity>, IId<Guid>
{
    public static EditToDoEntity Create(Guid input)
    {
        return new(input);
    }
}
