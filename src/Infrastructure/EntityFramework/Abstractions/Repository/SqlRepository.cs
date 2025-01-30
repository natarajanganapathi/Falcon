namespace Falcon.Infrastructure.EntityFramework.Abstractions.Repository;

public abstract class SqlRepository<TId, TEntity> : IRepository<TId, TEntity> where TEntity : class, IEntity<TId>, new()
{
    public SqlDbContext DbContext { get; }
    internal readonly ILogger<SqlRepository<TId, TEntity>> logger;
    internal readonly IRequestContext requestContext;
    private DbSet<TEntity>? _dbSet;
    public virtual DbSet<TEntity> DbSet
    {
        get
        {
            return _dbSet ??= GetDbSet();
        }
    }
    public DbSet<TEntity> GetDbSet()
    {
        return DbContext.Set<TEntity>();
    }
    public DbSet<Entity> GetDbSet<Entity>() where Entity : class, IEntity<TId>, new()
    {
        return DbContext.Set<Entity>();
    }
    protected SqlRepository(IServiceProvider serviceProvider, SqlDbContext dbContext)
    {
        DbContext = dbContext;
        logger = serviceProvider.GetRequiredService<ILogger<SqlRepository<TId, TEntity>>>();
        requestContext = serviceProvider.GetRequiredService<IRequestContext>();
    }

    public async Task<IList<JObject>> QueryAsync(IQueryRequest request, CancellationToken cancellationToken = default)
    {
        var query = GetQueryBuilder(request).BuildProjection(DbSet);
#if DEBUG
        logger.LogInformation("Sql Query: {Query}", query.ToQueryString());
#endif
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<IList<TEntity>> FindAsync(IQueryRequest request, CancellationToken cancellationToken = default)
    {
        var query = GetQueryBuilder(request).BuildSelection(DbSet);
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<long> CountAsync(IQueryRequest request, CancellationToken cancellationToken = default)
    {
        return await QueryBuilder<TEntity>.New()
                        .Where(request.Where)
                        .BuildCountAsync(DbSet, cancellationToken);
    }

    public async Task<TEntity> GetAsync(TId id, CancellationToken cancellationToken = default)
    {
        // Need to add the default filter condition to filter deleted items.
        var tid = id ?? throw new QueryException("The provided Id is null or invalid");
        var result = await DbSet.FindAsync([tid], cancellationToken);
        if (result is IDeletableEntity obj)
        {
            result = obj.Status == DeletableEntityStatus.Active ? result : null;
        }
        return result ?? throw new QueryException($"Entity with ID '{id}' not found or has been deleted.");
    }
    public async Task<IEnumerable<TEntity>> GetManyAsync(TId[] ids, CancellationToken cancellationToken = default)
    {
        var results = await DbSet.AsNoTracking().Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);
        var deletedEntities = results.OfType<IDeletableEntity>().Where(x => x.Status is not DeletableEntityStatus.Active);
        if (deletedEntities.Any() || ids.Length != results.Count)
        {
            throw new QueryException("One or more entities with the provided IDs were not found or have been deleted.");
        }
        return results;
    }
    private async Task<DeletableEntityStatus> GetStats(TId id, CancellationToken cancellationToken = default)
    {
        if (id is not null)
        {
            var request = new QueryRequest(["id", "status"])
                          .Where("Id", FieldOperator.Equal, id);
            var record = (await QueryAsync(request, cancellationToken)).SingleOrDefault();
            if (record != null && record.TryGetValue("status", out var statusValue))
            {
                return (DeletableEntityStatus)statusValue.Value<int>();
            }
        }
        return DeletableEntityStatus.Deleted;
    }
    public async Task<TEntity> CreateAsync(ICommandRequest<TEntity> request, CancellationToken cancellationToken = default)
    {
        var entity = request.Data ?? throw new PersistenceException("Entity is null");
        if (entity is IDeletableEntity obj) { obj.Status = DeletableEntityStatus.Active; }
        var entityEntry = await DbSet.AddAsync(entity, cancellationToken);
        await DbContext.SaveChangesAsync(requestContext.UserId, cancellationToken);
        return entityEntry.Entity;
    }
    public async Task<IEnumerable<TEntity>> CreateManyAsync(ICommandRequest<TEntity[]> request, CancellationToken cancellationToken = default)
    {
        var entities = request.Data ?? throw new PersistenceException("Entities are null");
        foreach (var entity in entities)
        {
            if (entity is IDeletableEntity obj) { obj.Status = DeletableEntityStatus.Active; }
        }
        await DbSet.AddRangeAsync(entities, cancellationToken);
        await DbContext.SaveChangesAsync(requestContext.UserId, cancellationToken);
        return entities;
    }
    public async Task<TEntity> UpdateAsync(TId id, ICommandRequest<TEntity> request, CancellationToken cancellationToken = default)
    {
        var entity = request.Data ?? throw new PersistenceException("The provided entity is null or invalid");
        entity.Id = id ?? throw new PersistenceException("The provided entity Id is null or invalid");
        if (entity is IDeletableEntity)
        {
            var status = await GetStats(id, cancellationToken);
            if (status != DeletableEntityStatus.Active)
            {
                throw new PersistenceException($"Entity with ID '{id}' is not active and cannot be updated.");
            }
        }
        return await UpdateAsync(entity, cancellationToken);
    }

    private async Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        DbSet.Attach(entity);
        if (entity is IConcurrencyEntity obj) { obj.Revision ++; }
        DbContext.Entry(entity).State = EntityState.Modified;
        await DbContext.SaveChangesAsync(requestContext.UserId, cancellationToken);
        return entity;
    }
    public async Task<TEntity> DeleteAsync(TId id, CancellationToken cancellationToken = default)
    {
        TEntity entityToDelete = await GetAsync(id, cancellationToken);
        if (entityToDelete is IDeletableEntity obj) { obj.Status = DeletableEntityStatus.Deleted; }
        return await UpdateAsync(entityToDelete, cancellationToken);
    }
    public async Task<TEntity> PatchAsync(TId id, ICommandRequest<JsonPatchDocument<TEntity>> request, CancellationToken cancellationToken = default)
    {
        var patchDocument = request.Data ?? throw new PersistenceException("The provided JsonPatchDocument<TEntity> is null.");
        TEntity original = await GetAsync(id, cancellationToken);
        patchDocument.ApplyTo(original);
        return await UpdateAsync(original, cancellationToken);
    }
    public async Task<TEntity> ReplaceAsync(TId id, ICommandRequest<TEntity> request, CancellationToken cancellationToken)
    {
        var entity = request.Data ?? throw new PersistenceException("The provided entity is null or invalid");
        var sourceEntity = await GetAsync(id, cancellationToken);
        sourceEntity = sourceEntity ?? throw new PersistenceException($"Entity with ID '{id}' not found or has been deleted.");
        DbContext.Entry(sourceEntity).CurrentValues.SetValues(entity);
        var modifiedCount = await DbContext.SaveChangesAsync(requestContext.UserId, cancellationToken);
        if (modifiedCount == 0) 
            throw new PersistenceException("No records were replaced. The entity might not exist or has already been deleted.");
        return sourceEntity;
    }

    private static QueryBuilder<TEntity> GetQueryBuilder(IQueryRequest request)
    {
        return QueryBuilder<TEntity>
            .New()
            .Select(request.Select)
            .Where(request.Where)
            .Sort(request.Sort)
            .Includes(request.Includes)
            .PageContext(request.Page);
    }
}
