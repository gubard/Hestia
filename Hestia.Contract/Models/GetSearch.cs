namespace Hestia.Contract.Models;

public class GetSearch
{
    public string SearchText { get; set; } = string.Empty;
    public ToDoType[] Types { get; set; } = [];
}