using Hestia.Contract.Models;
using Hestia.Contract.Services;
using Zeus.Helpers;

await WebApplication.CreateBuilder(args)
   .RunZeusApp<IToDoService, EfToDoService, HestiaGetRequest,
        HestiaPostRequest, HestiaGetResponse, HestiaPostResponse>("Hestia");