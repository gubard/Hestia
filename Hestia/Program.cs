using System.Collections.Frozen;
using Hestia.Contract.Helpers;
using Hestia.Contract.Models;
using Hestia.Contract.Services;
using Hestia.Services;
using Nestor.Db.Sqlite.Helpers;
using Zeus.Helpers;

var migration = new Dictionary<int, string>();

foreach (var (key, value) in SqliteMigration.Migrations)
{
    migration.Add(key, value);
}

foreach (var (key, value) in HestiaMigration.Migrations)
{
    migration.Add(key, value);
}

var builder = WebApplication
    .CreateBuilder(args)
    .AddServicesZeus<
        IToDoService,
        EfToDoService,
        HestiaGetRequest,
        HestiaPostRequest,
        HestiaGetResponse,
        HestiaPostResponse,
        HestiaDbContext
    >(migration.ToFrozenDictionary(), "Hestia");

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
