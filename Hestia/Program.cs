using System.Collections.Frozen;
using Hestia.Contract.Helpers;
using Hestia.Contract.Models;
using Hestia.Contract.Services;
using Hestia.Db.Services;
using Nestor.Db.Helpers;
using Zeus.Helpers;

InsertHelper.AddDefaultInsert(
    nameof(ToDoEntity),
    i => new ToDoEntity[] { new() { Id = i } }.CreateInsertQuery()
);

var migration = new Dictionary<int, string>();

foreach (var (key, value) in SqliteMigration.Migrations)
{
    migration.Add(key, value);
}

foreach (var (key, value) in HestiaMigration.Migrations)
{
    migration.Add(key, value);
}

foreach (var (key, value) in IdempotenceMigration.Migrations)
{
    migration.Add(key, value);
}

await WebApplication
    .CreateBuilder(args)
    .CreateAndRunZeusApp<
        IToDoService,
        ToDoLiteDbService,
        HestiaGetRequest,
        HestiaPostRequest,
        HestiaGetResponse,
        HestiaPostResponse
    >(
        migration.ToFrozenDictionary(),
        "Hestia",
        builder =>
            builder
                .Services.AddSingleton(HestiaJsonContext.Default.Options)
                .AddTransient<ToDoParametersFillerService>()
                .AddTransient<IToDoValidator, ToDoValidator>()
    );
