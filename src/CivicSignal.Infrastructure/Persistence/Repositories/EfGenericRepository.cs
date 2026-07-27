using CivicSignal.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CivicSignal.Infrastructure.Persistence.Repositories;

internal class EfGenericRepository<TEntity>(CivicSignalDbContext dbContext) : IGenericRepository<TEntity>
    where TEntity : class
{
    protected CivicSignalDbContext DbContext { get; } = dbContext;

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken)
    {
        await DbContext.Set<TEntity>().AddAsync(entity, cancellationToken);
    }

    public async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await DbContext.Set<TEntity>().FindAsync([id], cancellationToken);
    }

    public async Task<IReadOnlyCollection<TEntity>> ListAsync(CancellationToken cancellationToken)
    {
        return await DbContext.Set<TEntity>().ToArrayAsync(cancellationToken);
    }

    public void Update(TEntity entity)
    {
        DbContext.Set<TEntity>().Update(entity);
    }

    public void Remove(TEntity entity)
    {
        DbContext.Set<TEntity>().Remove(entity);
    }
}
