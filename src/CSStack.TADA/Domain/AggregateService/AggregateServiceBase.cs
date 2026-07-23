namespace CSStack.TADA
{
	/// <summary>
	/// Base class for aggregate services. Implements
	/// <see cref="IAggregateService{TEntity, TEntityIdentifier, TRepository, TOperateInfo, TSession}.GetEntityByIdentifierAsync"/>
	/// by delegating to the repository, and exposes the repository to derived classes.
	/// </summary>
	/// <typeparam name="TEntity">Entity type. The single entity of this aggregate.</typeparam>
	/// <typeparam name="TEntityIdentifier">Entity identifier type</typeparam>
	/// <typeparam name="TRepository">Repository type. The single repository of this aggregate.</typeparam>
	/// <typeparam name="TOperateInfo">Operate info type. See <see cref="IRepository{TEntity, TEntityIdentifier, TOperateInfo, TSession}"/>.</typeparam>
	/// <typeparam name="TSession">Transaction session type</typeparam>
	/// <remarks>
	/// <para>
	/// Inherit this to write the operations of the aggregate — creating the entity, applying a change to
	/// it, saving it through <see cref="Repository"/>, refusing to continue when it is missing. This class
	/// wraps nothing else on purpose: an aggregate service that only forwarded to the repository would add
	/// a layer without adding a rule, and the rules are what the aggregate exists to hold.
	/// </para>
	/// <para>
	/// See <see cref="IAggregateService{TEntity, TEntityIdentifier, TRepository, TOperateInfo, TSession}"/>
	/// for why the five type parameters are spelled out.
	/// </para>
	/// </remarks>
	public class AggregateServiceBase<TEntity, TEntityIdentifier, TRepository, TOperateInfo, TSession>
		: IAggregateService<TEntity, TEntityIdentifier, TRepository, TOperateInfo, TSession>
		where TEntity : IEntity<TEntityIdentifier>
		where TRepository : IRepository<TEntity, TEntityIdentifier, TOperateInfo, TSession>
		where TEntityIdentifier : notnull
		where TSession : IDisposable
		where TOperateInfo : notnull
	{
		/// <summary>
		/// The repository of this aggregate. Derived classes save and delete through it.
		/// </summary>
		protected readonly TRepository Repository;

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="repository">The repository of this aggregate</param>
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
