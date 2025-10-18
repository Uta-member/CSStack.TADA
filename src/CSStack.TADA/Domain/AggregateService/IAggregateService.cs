namespace CSStack.TADA
{
    /// <summary>
    /// Interface for aggregate services.
    /// </summary>
    /// <typeparam name="TEntity">Entity</typeparam>
    /// <typeparam name="TEntityIdentifier">Type of the entity identifier</typeparam>
    /// <typeparam name="TRepository">Repository</typeparam>
    /// <typeparam name="TOperateInfo">Type of operation info</typeparam>
    /// <typeparam name="TSession">Transaction session type</typeparam>
    public interface IAggregateService<TEntity, TEntityIdentifier, TRepository, TOperateInfo, TSession>
        where TEntity : IEntity<TEntityIdentifier>
        where TRepository : IRepository<TEntity, TEntityIdentifier, TOperateInfo, TSession>
        where TEntityIdentifier : notnull
        where TSession : IDisposable
        where TOperateInfo : notnull
    {
        /// <summary>
        /// Get the entity.
        /// </summary>
        /// <param name="session">Transaction session</param>
        /// <param name="identifier">Identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Optional entity</returns>
        ValueTask<Optional<TEntity>> GetEntityByIdentifierAsync(
            TSession session,
            TEntityIdentifier identifier,
            CancellationToken cancellationToken = default);
    }
}
