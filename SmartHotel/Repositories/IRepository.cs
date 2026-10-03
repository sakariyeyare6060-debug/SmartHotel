namespace SmartHotel.Repositories;

/// <summary>
/// Generic data-access abstraction. All repositories share the request-scoped
/// DbContext, so <see cref="SaveChangesAsync"/> commits every pending change
/// made through any repository in the same request (unit of work).
/// </summary>
public interface IRepository<T> where T : BaseEntity
{
    IQueryable<T> Query();
    IQueryable<T> QueryNoTracking();
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    void Add(T entity);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
