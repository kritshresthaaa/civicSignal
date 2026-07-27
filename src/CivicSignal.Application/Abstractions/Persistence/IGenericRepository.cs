namespace CivicSignal.Application.Abstractions.Persistence;

public interface IGenericRepository<TEntity>
    where TEntity : class
{
    Task AddAsync(TEntity entity, CancellationToken cancellationToken);

    Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TEntity>> ListAsync(CancellationToken cancellationToken);

    void Update(TEntity entity);

    void Remove(TEntity entity);
}
