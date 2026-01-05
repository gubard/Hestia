using Gaia.Services;
using Hestia.Contract.Models;
using Hestia.Contract.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Nestor.Db.Services;

namespace Hestia.Services;

public sealed class HestiaDbContext
    : NestorDbContext,
        IStaticFactory<DbContextOptions, NestorDbContext>
{
    public HestiaDbContext() { }

    public HestiaDbContext(DbContextOptions options)
        : base(options) { }

    public DbSet<ToDoEntity> ToDos { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.UseModel(HestiaDbContextModel.Instance);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new ToDoEntityTypeConfiguration());
    }

    public static NestorDbContext Create(DbContextOptions input)
    {
        return new HestiaDbContext(input);
    }
}

public class HestiaDbContextFactory : IDesignTimeDbContextFactory<HestiaDbContext>
{
    public HestiaDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<HestiaDbContext>();
        optionsBuilder.UseSqlite("");

        return new(optionsBuilder.Options);
    }
}
