using Gaia.Services;
using Hestia.Contract.Models;
using Microsoft.EntityFrameworkCore;
using Nestor.Db.Models;
using Nestor.Db.Services;

namespace Hestia.Contract.Services;

public interface IToDoService : IService<HestiaGetRequest, HestiaPostRequest, HestiaGetResponse, HestiaPostResponse>;
public interface IHttpToDoService : IToDoService;
public interface IEfToDoService : IToDoService, IEfService<HestiaGetRequest, HestiaPostRequest, HestiaGetResponse, HestiaPostResponse>;

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

    public ValueTask SaveEventsAsync(ReadOnlyMemory<EventEntity> events, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public ValueTask<long> GetLastIdAsync(CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}