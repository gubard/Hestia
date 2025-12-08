using Gaia.Services;
using Hestia.Contract.Models;
using Microsoft.EntityFrameworkCore;

namespace Hestia.Contract.Services;

public interface IToDoService : IService<HestiaGetRequest, HestiaPostRequest, HestiaGetResponse, HestiaPostResponse>;
public interface IHttpToDoService : IToDoService;
public interface IEfToDoService : IToDoService;

public sealed class EfToDoService : IEfToDoService
{
    private readonly DbContext _dbContext;

    public EfToDoService(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public ValueTask<HestiaGetResponse> GetAsync(HestiaGetRequest request, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<HestiaPostResponse> PostAsync(HestiaPostRequest request, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}