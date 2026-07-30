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
	/// <typeparam name="TSession">
	/// Transaction session type. Keep it as a type parameter on the derived service — see
	/// <see cref="IRepository{TEntity, TEntityIdentifier, TOperateInfo, TSession}"/>.
	/// </typeparam>
	/// <remarks>
	/// <para>
	/// Inherit this to write the operations of the aggregate — creating the entity, applying a change to
	/// it, writing it back through <see cref="Repository"/>, refusing to continue when it is missing. This
	/// class wraps nothing else on purpose: an aggregate service that only forwarded to the repository
	/// would add a layer without adding a rule, and the rules are what the aggregate exists to hold. Each
	/// operation is named after what the domain does and keeps the read and the write to itself; a
	/// <c>SaveAsync</c> that takes an entity from the caller is not one of them — see
	/// <see cref="IAggregateService{TEntity, TEntityIdentifier, TRepository, TOperateInfo, TSession}"/>.
	/// A helper that turns a missing entity into <see cref="ObjectNotFoundException"/> is useful here, but
	/// keep it <c>private</c>: handing the entity up to the use case gives it something it can change with
	/// no way to persist.
	/// </para>
	/// <para>
	/// <b>This class is an implementation detail; do not hand it to the layers above.</b> Declare the
	/// operations of the aggregate on an interface derived from
	/// <see cref="IAggregateService{TEntity, TEntityIdentifier, TRepository, TOperateInfo, TSession}"/>,
	/// implement that interface here, and inject the interface. Injecting the subclass makes every use
	/// case test construct it, along with the repository implementation it requires.
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
		/// <exception cref="ArgumentNullException"><paramref name="repository"/> is null.</exception>
		public AggregateServiceBase(TRepository repository)
		{
			ArgumentNullException.ThrowIfNull(repository);
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
