using Hestia.Contract.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Nestor.Db.Helpers;

namespace Hestia.Contract.Services;

public sealed class ToDoEntityTypeConfiguration : IEntityTypeConfiguration<ToDoEntity>
{
    public void Configure(EntityTypeBuilder<ToDoEntity> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever().SetComparerStruct();
        builder.Property(e => e.ReferenceId).SetComparerNullStruct();
        builder.Property(e => e.ParentId).SetComparerNullStruct();
        builder.Property(e => e.DueDate).SetComparerStruct();
        builder.Property(e => e.Name).HasMaxLength(255).SetComparerClass();
        builder.Property(e => e.NormalizeName).HasMaxLength(255).SetComparerClass();
        builder.Property(e => e.Description).HasMaxLength(10_000).SetComparerClass();
        builder.Property(e => e.Link).HasMaxLength(1000).SetComparerClass();
        builder.Property(e => e.CreatedDateTime).SetComparerStruct();
        builder.Property(e => e.LastCompleted).SetComparerNullStruct();
        builder.Property(e => e.OrderIndex).SetComparerStruct();
        builder.Property(e => e.Type).SetComparerStruct();
        builder.Property(e => e.IsBookmark).SetComparerStruct();
        builder.Property(e => e.IsFavorite).SetComparerStruct();
        builder.Property(e => e.IsCompleted).SetComparerStruct();
        builder.Property(e => e.TypeOfPeriodicity).SetComparerStruct();
        builder.Property(e => e.WeeklyDays).SetComparerClass();
        builder.Property(e => e.MonthlyDays).SetComparerClass();
        builder.Property(e => e.AnnuallyDays).SetComparerClass();
        builder.Property(e => e.DaysOffset).SetComparerStruct();
        builder.Property(e => e.MonthsOffset).SetComparerStruct();
        builder.Property(e => e.WeeksOffset).SetComparerStruct();
        builder.Property(e => e.YearsOffset).SetComparerStruct();
        builder.Property(e => e.ChildrenCompletionType).SetComparerStruct();
        builder.Property(e => e.CurrentCircleOrderIndex).SetComparerStruct();
        builder.Property(e => e.Link).SetComparerClass();
        builder.Property(e => e.IsRequiredCompleteInDueDate).SetComparerStruct();
        builder.Property(e => e.DescriptionType).SetComparerStruct();
        builder.Property(e => e.Icon).SetComparerClass();
        builder.Property(e => e.Color).SetComparerClass();
        builder.Property(e => e.RemindDaysBefore).SetComparerStruct();
        builder.Property(e => e.ReferenceId).SetComparerNullStruct();
        builder.Property(e => e.ParentId).SetComparerNullStruct();
    }
}
