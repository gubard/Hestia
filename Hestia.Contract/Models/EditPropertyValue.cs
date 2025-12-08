namespace Hestia.Contract.Models;

public class EditPropertyValue<TValue>
{
    public bool IsEdit { get; set; }
    public TValue? Value { get; set; } 
}