namespace LightningArc.Persistence.Abstractions;

public interface IRepositoryProvider
{
    IRepository<TId, TEntity> GetRepository<TId, TEntity>() where TEntity : class, IEntity<TId>, new();
}