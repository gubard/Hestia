using System.Buffers;
using Gaia.Helpers;
using Gaia.Models;
using Gaia.Services;
using Hestia.Contract.Models;

namespace Hestia.Contract.Services;

public interface IToDoValidator : IValidator<string>, IValidator<IToDo>;

public sealed class ToDoValidator : IToDoValidator
{
    private const string ValidNameChars =
        StringHelper.UpperLatin + StringHelper.LowerLatin + StringHelper.Number + StringHelper.SpecialSymbols + " ";

    private const string ValidDescriptionChars =
        StringHelper.UpperLatin + StringHelper.LowerLatin + StringHelper.Number + StringHelper.SpecialSymbols + " ";

    private static readonly SearchValues<char> ValidNameValues = SearchValues.Create(ValidNameChars);
    private static readonly SearchValues<char> ValidDescriptionValues = SearchValues.Create(ValidDescriptionChars);

    public ValidationError[] Validate(string value, string identity)
    {
        switch (identity)
        {
            case "Name":
            {
                if (value.Length > 255)
                {
                    return [new PropertyMaxSizeValidationError("Name", (ulong)value.Length, 255),];
                }

                if (value.IsNullOrWhiteSpace())
                {
                    return [new PropertyEmptyValidationError("Name"),];
                }

                if (value.Length < 1)
                {
                    return [new PropertyMinSizeValidationError("Name", (ulong)value.Length, 3),];
                }

                var index = value.IndexOfAnyExcept(ValidNameValues);

                if (index >= 0)
                {
                    return
                    [
                        new PropertyContainsInvalidValueValidationError<char>("Name", value[index],
                            ValidNameChars.ToCharArray()),
                    ];
                }

                return [];
            }
            case "Description":
            {
                if (value.Length > 10_000)
                {
                    return [new PropertyMaxSizeValidationError("Description", (ulong)value.Length, 10_000),];
                }

                if (value.IsNullOrWhiteSpace())
                {
                    return [];
                }

                var index = value.IndexOfAnyExcept(ValidDescriptionValues);

                if (index >= 0)
                {
                    return
                    [
                        new PropertyContainsInvalidValueValidationError<char>("Description", value[index],
                            ValidDescriptionChars.ToCharArray()),
                    ];
                }

                return [];
            }
            default: throw new ArgumentOutOfRangeException(nameof(identity), identity, null);
        }
    }

    public ValidationError[] Validate(IToDo value, string identity)
    {
        switch (value.Type)
        {
            case ToDoType.FixedDate:
                switch (identity)
                {
                    case nameof(value.DueDate): return ValidateDueDate(value.DueDate);
                    case nameof(value.Link): return ValidateLink(value.Link);
                }

                return [];
            case ToDoType.Periodicity:
                switch (identity)
                {
                    case nameof(value.DueDate): return ValidateDueDate(value.DueDate);
                    case nameof(value.Link): return ValidateLink(value.Link);
                    case nameof(value.AnnuallyDays):
                    {
                        if (value.TypeOfPeriodicity == TypeOfPeriodicity.Annually && !value.AnnuallyDays.Any())
                        {
                            return
                            [
                                new PropertyInvalidValidationError(nameof(value.AnnuallyDays)),
                            ];
                        }

                        break;
                    }
                    case nameof(value.MonthlyDays):
                    {
                        if (value.TypeOfPeriodicity == TypeOfPeriodicity.Monthly && !value.MonthlyDays.Any())
                        {
                            return
                            [
                                new PropertyInvalidValidationError(nameof(value.MonthlyDays)),
                            ];
                        }

                        break;
                    }
                    case nameof(value.WeeklyDays):
                    {
                        if (value.TypeOfPeriodicity == TypeOfPeriodicity.Weekly && !value.WeeklyDays.Any())
                        {
                            return
                            [
                                new PropertyInvalidValidationError(nameof(value.WeeklyDays)),
                            ];
                        }

                        break;
                    }
                }

                return [];
            case ToDoType.PeriodicityOffset:
                switch (identity)
                {
                    case nameof(value.DueDate): return ValidateDueDate(value.DueDate);
                    case nameof(value.Link): return ValidateLink(value.Link);
                    case nameof(value.DaysOffset):
                    {
                        if (value is { DaysOffset: 0, WeeksOffset: 0, MonthsOffset: 0, YearsOffset: 0, })
                        {
                            return [new PropertyEmptyValidationError("Offset"),];
                        }

                        break;
                    }
                }

                return [];
            case ToDoType.Reference:
                switch (identity)
                {
                    case "Reference":
                    {
                        if (value.ReferenceId is null)
                        {
                            return [new PropertyEmptyValidationError("Reference"),];
                        }

                        break;
                    }
                }

                return [];
            case ToDoType.Circle:
            case ToDoType.Step:
            case ToDoType.Value:
            case ToDoType.Group:
                return identity switch
                {
                    nameof(value.Link) => ValidateLink(value.Link),
                    _ => [],
                };

            default: throw new ArgumentOutOfRangeException(nameof(identity), identity, null);
        }
    }

    private ValidationError[] ValidateLink(string link)
    {
        if (link.Length > 1000)
        {
            return [new PropertyMaxSizeValidationError(nameof(IToDo.Link), (ulong)link.Length, 1000),];
        }

        if (link.IsNullOrWhiteSpace())
        {
            return [];
        }

        if (!link.IsLink())
        {
            return [new PropertyInvalidValidationError(nameof(IToDo.Link)),];
        }

        return [];
    }

    private ValidationError[] ValidateDueDate(DateOnly dueDate)
    {
        var now = DateTime.Now;

        if (dueDate.ToDateTime(TimeOnly.MinValue) < now)
        {
            return
            [
                new PropertyTheDateHasExpiredValidationError("DueDate", dueDate,
                    now.ToDateOnly()),
            ];
        }

        return [];
    }
}