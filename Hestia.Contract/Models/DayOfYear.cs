namespace Hestia.Contract.Models;

public readonly struct DayOfYear : IComparable<DayOfYear>
{
    public DayOfYear(byte day, Month month)
    {
        Day = day;
        Month = month;
    }

    public byte Day { get; }
    public Month Month { get; }

    public int CompareTo(DayOfYear other)
    {
        var year = DateTime.Now.Year;
        var x = new DateOnly(year, (int)Month, Math.Min(DateTime.DaysInMonth(year, (int)Month), Day));
        var y = new DateOnly(year, (int)other.Month, Math.Min(DateTime.DaysInMonth(year, (int)other.Month), other.Day));

        return x.CompareTo(y);
    }
}