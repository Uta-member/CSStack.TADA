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
		/// <remarks>
		/// <b>Return <see cref="Optional{TEntity}.Empty"/> when the entity was not found — never <c>null</c>.</b>
		/// The implicit conversion on <see cref="Optional{TEntity}"/> turns <c>null</c> into <c>Some(null)</c>,
		/// so <c>return null;</c> produces a result whose <see cref="Optional{TEntity}.HasValue"/> is true and
		/// whose value is null, and callers using <see cref="Optional{TEntity}.TryGetValue(out TEntity)"/> will
		/// get true and then a <see cref="NullReferenceException"/>. Not finding the entity is a normal outcome
		/// and must not throw <see cref="ObjectNotFoundException"/>; leave that decision to the caller.
		/// </remarks>
		/// <param name="session">Transaction factor</param>
		/// <param name="identifier">Entity identifier</param>
		/// <param name="cancellationToken">Cancellation token</param>
		/// <returns>The entity wrapped in <see cref="Optional{TEntity}"/>, or <see
		/// cref="Optional{TEntity}.Empty"/> when no entity matches <paramref name="identifier"/></returns>
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
