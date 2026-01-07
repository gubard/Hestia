using Hestia.Contract.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hestia.Contract.Services;

public sealed class ToDoEntityTypeConfiguration : IEntityTypeConfiguration<ToDoEntity>
{
    public void Configure(EntityTypeBuilder<ToDoEntity> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.Name).HasMaxLength(255);
        builder.Property(e => e.NormalizeName).HasMaxLength(255);
        builder.Property(e => e.Description).HasMaxLength(10_000);
        builder.Property(e => e.Link).HasMaxLength(1000);

        builder
            .Property(e => e.CreatedDateTime)
            .Metadata.SetValueComparer(
                new ValueComparer<DateTimeOffset>(
                    (c1, c2) => c1.Equals(c2),
                    c => c.GetHashCode(),
                    c => c
                )
            );

        builder
            .Property(e => e.LastCompleted)
            .Metadata.SetValueComparer(
                new ValueComparer<DateTimeOffset?>(
                    (c1, c2) => c1 == null && c2 == null || c1 != null && c1.Equals(c2),
                    c => c.GetHashCode(),
                    c => c
                )
            );
    }
}
