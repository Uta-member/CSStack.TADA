namespace CSStack.TADA
{
	/// <summary>
	/// Domain service interface (with request). Domain logic that belongs to no single aggregate.
	/// </summary>
	/// <typeparam name="TReq">Request type</typeparam>
	/// <remarks>
	/// <para>
	/// Use a domain service for a rule that spans aggregates, or that needs something an entity has no
	/// business holding — "this e-mail address is not taken by another user", "move this balance from one
	/// account to another". A rule that concerns one entity belongs on the entity; a rule about the one
	/// aggregate belongs on its
	/// <see cref="IAggregateService{TEntity, TEntityIdentifier, TRepository, TOperateInfo, TSession}"/>;
	/// orchestration, authorization and transactions belong to <see cref="ICommandService{TReq}"/>.
	/// </para>
	/// <para>
	/// <b>A domain service never begins a transaction.</b> It runs inside one that a command service
	/// already started, and since <see cref="ExecuteAsync"/> takes only the request, the session travels on
	/// the request DTO — that is what <see cref="IDomainServiceDTO"/> implementations are expected to
	/// carry. Do not inject <see cref="ITransactionManager"/> here.
	/// </para>
	/// <example>
	/// <code>
	/// public sealed record EnsureEmailIsUniqueRequest(MySession Session, Email Email) : IDomainServiceDTO;
	/// </code>
	/// </example>
	/// </remarks>
	public interface IDomainService<TReq> where TReq : IDomainServiceDTO
	{
		/// <summary>
		/// Execute.
		/// </summary>
		/// <param name="req">Request</param>
		/// <param name="cancellationToken">Cancellation token</param>
		ValueTask ExecuteAsync(TReq req, CancellationToken cancellationToken = default);
	}

	/// <summary>
	/// Domain service interface (with request and response).
	/// </summary>
	/// <typeparam name="TReq">Request type</typeparam>
	/// <typeparam name="TRes">Response type</typeparam>
	/// <remarks>
	/// Same contract as <see cref="IDomainService{TReq}"/>; see it for when to write a domain service and
	/// how the session reaches it.
	/// </remarks>
	public interface IDomainService<TReq, TRes> where TReq : IDomainServiceDTO where TRes : IDomainServiceDTO
	{
		/// <summary>
		/// Execute.
		/// </summary>
		/// <param name="req">Request</param>
		/// <param name="cancellationToken">Cancellation token</param>
		ValueTask<TRes> ExecuteAsync(TReq req, CancellationToken cancellationToken = default);
	}
}
