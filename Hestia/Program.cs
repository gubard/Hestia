using System.Collections.Frozen;
using Hestia.Contract.Helpers;
using Hestia.Contract.Models;
using Hestia.Contract.Services;
using Nestor.Db.Helpers;
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
        DbToDoService,
        HestiaGetRequest,
        HestiaPostRequest,
        HestiaGetResponse,
        HestiaPostResponse
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
