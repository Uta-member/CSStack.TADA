namespace CSStack.TADA
{
	/// <summary>
	/// Interface for aggregate services. Names the aggregate: one entity, one repository, one service.
	/// </summary>
	/// <typeparam name="TEntity">Entity. The single entity of this aggregate.</typeparam>
	/// <typeparam name="TEntityIdentifier">Type of the entity identifier</typeparam>
	/// <typeparam name="TRepository">Repository. The single repository of this aggregate.</typeparam>
	/// <typeparam name="TOperateInfo">Type of operation info. See <see cref="IRepository{TEntity, TEntityIdentifier, TOperateInfo, TSession}"/>.</typeparam>
	/// <typeparam name="TSession">
	/// Transaction session type. Keep it as a type parameter — see
	/// <see cref="IRepository{TEntity, TEntityIdentifier, TOperateInfo, TSession}"/>.
	/// </typeparam>
	/// <remarks>
	/// <para>
	/// <b>The five type parameters are the point of this interface, not overhead.</b> Writing them out
	/// states that this aggregate has exactly one entity, exactly one repository over it, and exactly one
	/// service that can reach the entity — the rule TADA enforces about aggregates. They are not there to
	/// save you keystrokes, and the interface stays this shape even though
	/// <see cref="GetEntityByIdentifierAsync"/> is the only member that uses all of them.
	/// </para>
	/// <para>
	/// Everything an aggregate needs beyond reaching its entity — registering one, applying a change to
	/// it, deleting it, deciding that a missing entity is an error — is added by the concrete service,
	/// which is free to use <typeparamref name="TRepository"/> directly. This is also the layer that turns
	/// <see cref="Optional{TValue}.Empty"/> into <see cref="ObjectNotFoundException"/> when the operation
	/// requires the entity to exist; the repository never makes that call.
	/// </para>
	/// <para>
	/// <b>Name those operations after what the domain does, and do not declare a general
	/// <c>SaveAsync</c>.</b> The derived interface is in practice the aggregate root: the methods on it
	/// are the list of what may happen to the aggregate. A <c>SaveAsync</c> next to a <c>RegisterAsync</c>
	/// will be the one callers reach for — it accepts anything — and the rule <c>RegisterAsync</c> was
	/// holding ("fail if it already exists") is then simply bypassed, which is the whole reason the two
	/// were separated. Close each "read, change, write" round trip inside one named operation
	/// (<c>RenameAsync(session, identifier, newName, operateInfo, ct)</c>) so the entity never has to
	/// leave the aggregate for a caller to mutate and hand back. <c>SaveAsync</c> on
	/// <see cref="IRepository{TEntity, TEntityIdentifier, TOperateInfo, TSession}"/> is an upsert meant to
	/// be called from here, not from a use case.
	/// </para>
	/// <para>
	/// A concrete aggregate service therefore stays generic over the session as well:
	/// <c>UserAggregateService&lt;TSession&gt; : AggregateServiceBase&lt;User, UserId,
	/// IUserRepository&lt;TSession&gt;, OperateInfo, TSession&gt;</c>. Substituting the infrastructure
	/// session type here pins the whole aggregate to one store.
	/// </para>
	/// <para>
	/// <b>Derive an interface from this one, then implement it.</b> Declare the operations of the
	/// aggregate on an <c>IUserAggregateService&lt;TSession&gt; : IAggregateService&lt;User, UserId,
	/// IUserRepository&lt;TSession&gt;, OperateInfo, TSession&gt;</c> and write the implementation as a
	/// subclass of
	/// <see cref="AggregateServiceBase{TEntity, TEntityIdentifier, TRepository, TOperateInfo, TSession}"/>.
	/// The base class is an implementation detail, not the contract the layers above should see: a use
	/// case that takes the concrete service cannot be tested without constructing it — and therefore the
	/// repository implementation behind it — whereas one that takes the interface only needs a
	/// substitute. Register it as interface to implementation.
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
