namespace CSStack.TADA
{
	/// <summary>
	/// Repository interface. Persists and restores the entity of one aggregate, by identifier.
	/// </summary>
	/// <typeparam name="TEntity">Entity type</typeparam>
	/// <typeparam name="TEntityIdentifier">Entity identifier type</typeparam>
	/// <typeparam name="TOperateInfo">
	/// Operation info type. The "who and when" recorded alongside a write — an operator id, a timestamp,
	/// a request id, or a record holding all of them. It is passed to every write so that persisting an
	/// entity and persisting the circumstances of that write cannot drift apart. Reads do not take it:
	/// reading changes nothing, so there is nothing to record. When a read has to be traced, do it in the
	/// use case layer rather than widening this signature.
	/// </typeparam>
	/// <typeparam name="TSession">
	/// Transaction session type. Passed in by the caller rather than held by the repository, so that
	/// several repositories can take part in one transaction started by
	/// <see cref="ITransactionManager"/>. Implementations must not begin, commit or dispose it.
	/// </typeparam>
	/// <remarks>
	/// <para>
	/// <b>There are deliberately no query methods here.</b> Listing, searching and paging belong to
	/// <see cref="IQueryService{TReq, TRes}"/>, which reads the store directly and returns a DTO shaped
	/// for the caller. A repository only restores whole entities so that they can be modified and saved
	/// again. Do not add <c>FindAllAsync</c> / <c>FindByConditionAsync</c> to a repository: an entity is
	/// the write model, and building a read model out of it is what makes aggregates leak.
	/// </para>
	/// <para>
	/// One aggregate has one entity, one repository and one aggregate service. A repository is therefore
	/// typed against a single entity type and does not span aggregates.
	/// </para>
	/// </remarks>
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
		/// Persist the entity. This is an <b>upsert</b>: it inserts when no record carries the entity's
		/// identifier and updates when one does.
		/// </summary>
		/// <remarks>
		/// <para>
		/// Because it is an upsert, it does not signal "already exists" or "not found" — those are not
		/// failures here, and a correct implementation throws neither
		/// <see cref="ObjectAlreadyExistException"/> nor <see cref="ObjectNotFoundException"/>.
		/// Whether an entity that is missing or already present is a problem depends on the operation
		/// being carried out, so that decision belongs to the aggregate service or the use case that
		/// looked the entity up — not to the repository. Failures reaching the caller are infrastructure
		/// failures (a broken connection, a violated constraint), and they roll the transaction back.
		/// </para>
		/// <para>
		/// The write takes effect when <paramref name="session"/> is committed by
		/// <see cref="ITransactionManager"/>, not when this method returns.
		/// </para>
		/// </remarks>
		/// <param name="session">Transaction factor</param>
		/// <param name="entity">Entity to insert or update</param>
		/// <param name="operateInfo">Operate info to record alongside the write</param>
		/// <param name="cancellationToken">Cancellation token</param>
		ValueTask SaveAsync(
			TSession session,
			TEntity entity,
			TOperateInfo operateInfo,
			CancellationToken cancellationToken = default);
	}
}
