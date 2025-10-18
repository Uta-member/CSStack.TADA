namespace CSStack.TADA
{
    /// <summary>
    /// Base class for aggregate services.
    /// </summary>
    /// <typeparam name="TEntity">Entity type</typeparam>
    /// <typeparam name="TEntityIdentifier">Entity identifier type</typeparam>
    /// <typeparam name="TRepository">Repository type</typeparam>
    /// <typeparam name="TOperateInfo">Operate info type</typeparam>
    /// <typeparam name="TSession">Transaction session type</typeparam>
    public class AggregateServiceBase<TEntity, TEntityIdentifier, TRepository, TOperateInfo, TSession>
        : IAggregateService<TEntity, TEntityIdentifier, TRepository, TOperateInfo, TSession>
        where TEntity : IEntity<TEntityIdentifier>
        where TRepository : IRepository<TEntity, TEntityIdentifier, TOperateInfo, TSession>
        where TEntityIdentifier : notnull
        where TSession : IDisposable
        where TOperateInfo : notnull
    {
        /// <summary>
        /// Repository
        /// </summary>
        protected readonly TRepository Repository;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="repository"></param>
        public AggregateServiceBase(TRepository repository)
        {
            Repository = repository;
        }

        /// <inheritdoc/>
        public ValueTask<Optional<TEntity>> GetEntityByIdentifierAsync(
            TSession session,
            TEntityIdentifier identifier,
            CancellationToken cancellationToken = default)
        {
            return Repository.FindByIdentifierAsync(session, identifier, cancellationToken);
        }
    }
}
