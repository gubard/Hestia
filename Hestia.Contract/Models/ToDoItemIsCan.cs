namespace Hestia.Contract.Models;

[Flags]
public enum ToDoItemIsCan
{
    None = 0,
    CanComplete = 1,
    CanIncomplete = 2,
}