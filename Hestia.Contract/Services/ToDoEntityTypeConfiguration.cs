using Hestia.Contract.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hestia.Contract.Services;

public sealed class ToDoEntityEntityTypeConfiguration : IEntityTypeConfiguration<ToDoEntity>
{
    public void Configure(EntityTypeBuilder<ToDoEntity> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(255);
        builder.Property(e => e.NormalizeName).HasMaxLength(255);
        builder.Property(e => e.Description).HasMaxLength(10_000);
        builder.Property(e => e.Link).HasMaxLength(1000);
    }
}
