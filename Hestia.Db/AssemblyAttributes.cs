using Hestia.Contract.Models;
using Nestor.Db.LiteDb.Models;

[assembly: LiteDb(typeof(ToDoEntity), nameof(ToDoEntity.Id), false)]
[assembly: LiteDbSourceEntity(typeof(ToDoEntity), nameof(ToDoEntity.Id))]
