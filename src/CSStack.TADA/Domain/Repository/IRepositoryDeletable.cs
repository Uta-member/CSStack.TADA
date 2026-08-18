namespace CSStack.TADA
{
	/// <summary>
	/// Defines a repository interface that supports deleting entities by their identifier.
	/// </summary>
	/// <remarks>
	/// This interface extends <see cref="IRepository{TEntity, TEntityIdentifier, TOperateInfo, TSession}"/> and adds
	/// functionality for deleting entities by their identifier. It is designed for scenarios where entity deletion is a
	/// supported operation within the repository.
	/// </remarks>
	/// <typeparam name="TEntity">The type of the entity managed by the repository.</typeparam>
	/// <typeparam name="TEntityIdentifier">The type of the unique identifier for the entity.</typeparam>
	/// <typeparam name="TOperateInfo">The type of the operation metadata used during repository operations.</typeparam>
	/// <typeparam name="TSession">
	/// The type of the session or transaction context used for repository operations. Must implement <see
	/// cref="IDisposable"/>.
	/// </typeparam>
	public interface IRepositoryDeletable<TEntity, TEntityIdentifier, TOperateInfo, TSession>
		: IRepository<TEntity, TEntityIdentifier, TOperateInfo, TSession>
		where TEntity : IEntity<TEntityIdentifier>
		where TSession : IDisposable
		where TEntityIdentifier : notnull
		where TOperateInfo : notnull
	{
		/// <summary>
		/// Delete the entity.
		/// </summary>
		/// <remarks>
		/// Deleting an entity that is no longer there is not a failure of this method, so it must not throw.
		/// Whether the entity has to exist is decided by the caller,
		/// which normally reads it through
		/// <see cref="IRepository{TEntity, TEntityIdentifier, TOperateInfo, TSession}.FindByIdentifierAsync"/>
		/// first — that is also why this method takes the entity rather than its identifier.
		/// </remarks>
		/// <param name="session">Transaction factor</param>
		/// <param name="entity">Entity to delete</param>
		/// <param name="operateInfo">Operate info to record alongside the deletion</param>
		/// <param name="cancellationToken">Cancellation token</param>
		ValueTask DeleteAsync(
			TSession session,
			TEntity entity,
			TOperateInfo operateInfo,
			CancellationToken cancellationToken = default);
	}
}
