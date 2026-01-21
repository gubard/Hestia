using Hestia.Contract.Models;

namespace Hestia.Contract.Helpers;

public static class ToDoTypeExtension
{
    public static bool IsHasDueDate(this ToDoType type)
    {
        return type switch
        {
            ToDoType.Value => false,
            ToDoType.Step => false,
            ToDoType.Circle => false,
            ToDoType.Group => false,
            ToDoType.FixedDate => true,
            ToDoType.Periodicity => true,
            ToDoType.PeriodicityOffset => true,
            ToDoType.Reference => false,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
        };
    }
}
