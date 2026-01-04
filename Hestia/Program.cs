using Hestia.Contract.Models;
using Hestia.Contract.Services;
using Hestia.Services;
using Zeus.Helpers;

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
    >("Hestia");

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
