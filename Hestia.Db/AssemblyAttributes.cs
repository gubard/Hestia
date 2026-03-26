using Hestia.Contract.Models;
using Nestor.Db.LiteDb.Models;
using Nestor.Db.Models;

[assembly: LiteDb(typeof(ToDoEntity), nameof(ToDoEntity.Id), false)]
[assembly: LiteDbSourceEntity(typeof(ToDoEntity), nameof(ToDoEntity.Id))]
[assembly: Ado(typeof(ToDoEntity), nameof(ToDoEntity.Id), false)]
[assembly: AdoSourceEntity(typeof(ToDoEntity), nameof(ToDoEntity.Id))]
