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

var builder = WebApplication
    .CreateBuilder(args)
    .AddServicesZeus<
        IToDoService,
        ToDoDbService,
        HestiaGetRequest,
        HestiaPostRequest,
        HestiaGetResponse,
        HestiaPostResponse
    >(migration.ToFrozenDictionary(), HestiaJsonContext.Default.Options, "Hestia");

builder.Services.AddTransient<ToDoParametersFillerService>();
builder.Services.AddTransient<IToDoValidator, ToDoValidator>();
var app = builder.Build();

await app.RunZeusApp<
    IToDoService,
    HestiaGetRequest,
    HestiaPostRequest,
    HestiaGetResponse,
    HestiaPostResponse
>();
