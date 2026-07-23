namespace CSStack.TADA
{
	/// <summary>
	/// Query service interface (request and response). One read-only use case.
	/// </summary>
	/// <typeparam name="TReq">Request type</typeparam>
	/// <typeparam name="TRes">Response type</typeparam>
	/// <remarks>
	/// <para>
	/// <b>A query service reads the store directly and returns a DTO.</b> It does not go through
	/// repositories, entities or aggregate services: those exist to keep writes correct, and rebuilding
	/// them only to flatten them into a list costs work and drags aggregate boundaries into screens that
	/// do not respect them. This is why
	/// <see cref="IRepository{TEntity, TEntityIdentifier, TOperateInfo, TSession}"/> has no listing or
	/// searching methods — those queries live here.
	/// </para>
	/// <para>
	/// Reads normally need no transaction, so a query service usually takes its own connection rather than
	/// an <see cref="ITransactionManager"/>. When a read has to see the uncommitted state of a transaction
	/// already in flight, take the session on the request DTO instead of beginning a second one.
	/// </para>
	/// </remarks>
	public interface IQueryService<TReq, TRes> where TReq : IQueryServiceDTO where TRes : IQueryServiceDTO
	{
		/// <summary>
		/// Execute the query service method.
		/// </summary>
		/// <param name="req">Request</param>
		/// <param name="cancellationToken">Cancellation token</param>
		/// <returns>Response</returns>
		ValueTask<TRes> ExecuteAsync(TReq req, CancellationToken cancellationToken = default);
	}

	/// <summary>
	/// Query service interface (response only). One read-only use case that takes no arguments.
	/// </summary>
	/// <typeparam name="TRes">
	/// <b>Response</b> type — not the request. A query with nothing to ask for still has something to
	/// return, so the single type parameter here is the response, whereas the single type parameter of
	/// <see cref="ICommandService{TReq}"/> and <see cref="IDomainService{TReq}"/> is the request.
	/// <c>IQueryService&lt;Foo&gt;</c> means "returns <c>Foo</c>"; <c>ICommandService&lt;Foo&gt;</c> means
	/// "takes <c>Foo</c>". Reach for <see cref="IQueryService{TReq, TRes}"/> whenever there is a request,
	/// and the ambiguity does not arise.
	/// </typeparam>
	/// <remarks>
	/// See <see cref="IQueryService{TReq, TRes}"/> for what a query service is allowed to do.
	/// </remarks>
	public interface IQueryService<TRes> where TRes : IQueryServiceDTO
	{
		/// <summary>
		/// Execute the query service method.
		/// </summary>
		/// <param name="cancellationToken">Cancellation token</param>
		/// <returns>Response</returns>
		ValueTask<TRes> ExecuteAsync(CancellationToken cancellationToken = default);
	}
}
