using Hestia.Contract.Models;
using Hestia.Contract.Services;
using Zeus.Helpers;

var builder = WebApplication.CreateBuilder(args).AddServicesZeus
<IToDoService, EfToDoService, HestiaGetRequest,
    HestiaPostRequest, HestiaGetResponse, HestiaPostResponse>("Hestia");

builder.Services.AddTransient<ToDoParametersFillerService>();
var app = builder.Build();
await app
   .RunZeusApp<IToDoService, HestiaGetRequest, HestiaPostRequest,
        HestiaGetResponse, HestiaPostResponse>();