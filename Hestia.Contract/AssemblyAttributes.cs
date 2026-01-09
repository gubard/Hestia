using Hestia.Contract.Models;
using Nestor.Db.Models;

[assembly: SqliteAdo(typeof(ToDoEntity), nameof(ToDoEntity.Id))]
[assembly: SourceEntity(typeof(ToDoEntity), nameof(ToDoEntity.Id))]
