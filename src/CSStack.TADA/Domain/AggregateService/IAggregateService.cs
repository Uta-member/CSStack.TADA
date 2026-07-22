namespace CSStack.TADA
{
	/// <summary>
	/// Interface for aggregate services. Names the aggregate: one entity, one repository, one service.
	/// </summary>
	/// <typeparam name="TEntity">Entity. The single entity of this aggregate.</typeparam>
	/// <typeparam name="TEntityIdentifier">Type of the entity identifier</typeparam>
	/// <typeparam name="TRepository">Repository. The single repository of this aggregate.</typeparam>
	/// <typeparam name="TOperateInfo">Type of operation info. See <see cref="IRepository{TEntity, TEntityIdentifier, TOperateInfo, TSession}"/>.</typeparam>
	/// <typeparam name="TSession">Transaction session type</typeparam>
	/// <remarks>
	/// <para>
	/// <b>The five type parameters are the point of this interface, not overhead.</b> Writing them out
	/// states that this aggregate has exactly one entity, exactly one repository over it, and exactly one
	/// service that can reach the entity — the rule TADA enforces about aggregates. They are not there to
	/// save you keystrokes, and the interface stays this shape even though
	/// <see cref="GetEntityByIdentifierAsync"/> is the only member that uses all of them.
	/// </para>
	/// <para>
	/// Everything an aggregate needs beyond reaching its entity — saving it, deleting it, deciding that a
	/// missing entity is an error — is added by the concrete service, which is free to use
	/// <typeparamref name="TRepository"/> directly. This is also the layer that turns
	/// <see cref="Optional{TValue}.Empty"/> into <see cref="ObjectNotFoundException"/> when the operation
	/// requires the entity to exist; the repository never makes that call.
	/// </para>
	/// </remarks>
	public interface IAggregateService<TEntity, TEntityIdentifier, TRepository, TOperateInfo, TSession>
		where TEntity : IEntity<TEntityIdentifier>
		where TRepository : IRepository<TEntity, TEntityIdentifier, TOperateInfo, TSession>
		where TEntityIdentifier : notnull
		where TSession : IDisposable
		where TOperateInfo : notnull
	{
		/// <summary>
		/// Get the entity of this aggregate.
		/// </summary>
		/// <remarks>
		/// Returns <see cref="Optional{TEntity}.Empty"/> when there is no such entity — not finding one is
		/// a normal outcome, and whether it is an error depends on the operation. Throw
		/// <see cref="ObjectNotFoundException"/> from the method that requires the entity, not from here.
		/// </remarks>
		/// <param name="session">Transaction session</param>
		/// <param name="identifier">Identifier</param>
		/// <param name="cancellationToken">Cancellation token</param>
		/// <returns>The entity, or <see cref="Optional{TEntity}.Empty"/> when it does not exist</returns>
		ValueTask<Optional<TEntity>> GetEntityByIdentifierAsync(
			TSession session,
			TEntityIdentifier identifier,
			CancellationToken cancellationToken = default);
	}
}
