namespace CSStack.TADA
{
    /// <summary>
    /// Repository interface.
    /// </summary>
    /// <typeparam name="TEntity">Entity type</typeparam>
    /// <typeparam name="TEntityIdentifier">Entity identifier type</typeparam>
    /// <typeparam name="TOperateInfo">Operate info type</typeparam>
    /// <typeparam name="TSession">Transaction factor type</typeparam>
    public interface IRepository<TEntity, TEntityIdentifier, TOperateInfo, TSession>
        where TEntity : IEntity<TEntityIdentifier>
        where TSession : IDisposable
        where TEntityIdentifier : notnull
        where TOperateInfo : notnull
    {
        /// <summary>
        /// Get an entity by identifier.
        /// </summary>
        /// <param name="session">Transaction factor</param>
        /// <param name="identifier">Entity identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Entity</returns>
        ValueTask<Optional<TEntity>> FindByIdentifierAsync(
            TSession session,
            TEntityIdentifier identifier,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Persist the entity.
        /// </summary>
        /// <param name="session">Transaction factor</param>
        /// <param name="entity">Entity</param>
        /// <param name="operateInfo">Operate info</param>
        /// <param name="cancellationToken">Cancellation token</param>
        ValueTask SaveAsync(
            TSession session,
            TEntity entity,
            TOperateInfo operateInfo,
            CancellationToken cancellationToken = default);
    }
}
