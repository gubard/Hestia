using Hestia.Contract.Models;
using Microsoft.EntityFrameworkCore;
using Nestor.Db.Services;

namespace Hestia.Contract.Services;

public interface IToDoDbContext : INestorDbContext
{
    DbSet<ToDoEntity> ToDos { get; }
}
