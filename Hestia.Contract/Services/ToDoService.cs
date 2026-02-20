using Gaia.Services;
using Hestia.Contract.Models;
using Nestor.Db.Services;

namespace Hestia.Contract.Services;

public interface IToDoDbCache : IDbCache<HestiaPostRequest, HestiaGetResponse>;

public interface IToDoService
    : IService<HestiaGetRequest, HestiaPostRequest, HestiaGetResponse, HestiaPostResponse>;

public interface IToDoHttpService
    : IToDoService,
        IHttpService<HestiaGetRequest, HestiaPostRequest, HestiaGetResponse, HestiaPostResponse>;

public interface IToDoDbService
    : IToDoService,
        IDbService<HestiaGetRequest, HestiaPostRequest, HestiaGetResponse, HestiaPostResponse>;

public sealed class EmptyToDoDbCache
    : EmptyDbCache<HestiaPostRequest, HestiaGetResponse>,
        IToDoDbCache;

public sealed class EmptyToDoDbService
    : EmptyDbService<HestiaGetRequest, HestiaPostRequest, HestiaGetResponse, HestiaPostResponse>,
        IToDoDbService;
