namespace CSStack.TADA
{
	/// <summary>
	/// Use case command interface (request only). One state-changing use case.
	/// </summary>
	/// <typeparam name="TReq">Use case request type</typeparam>
	/// <remarks>
	/// <para>
	/// <b>This is where the transaction begins and ends.</b> A command service is the outermost layer that
	/// knows about transactions: it takes an <see cref="ITransactionManager"/> in its constructor and runs
	/// its work inside
	/// <see cref="ITransactionManager.ExecuteTransactionAsync{TSession1}(Func{TransactionSessions, CancellationToken, ValueTask}, Func{Exception, ValueTask}, CancellationToken)"/>,
	/// pulling the session out of <see cref="TransactionSessions"/> and handing it down to the aggregate
	/// services and repositories it calls. Nothing below this layer begins, commits or rolls back a
	/// transaction. Register the command service and the transaction manager as <i>scoped</i>.
	/// </para>
	/// <para>
	/// It is a convention, not a constraint: no base class forces it, so a use case that needs two
	/// transactions in sequence, or none at all, can still be written as a command service. But the plain
	/// case is the one below, and a command service that never touches
	/// <see cref="ITransactionManager"/> is nearly always a mistake.
	/// </para>
	/// <example>
	/// <code>
	/// public sealed class ChangeUserNameCommandService : ICommandService&lt;ChangeUserNameRequest&gt;
	/// {
	///     private readonly ITransactionManager _transactionManager;
	///     private readonly UserAggregateService _userAggregateService;
	///
	///     public ChangeUserNameCommandService(
	///         ITransactionManager transactionManager,
	///         UserAggregateService userAggregateService)
	///     {
	///         _transactionManager = transactionManager;
	///         _userAggregateService = userAggregateService;
	///     }
	///
	///     public ValueTask ExecuteAsync(ChangeUserNameRequest req, CancellationToken cancellationToken = default)
	///     {
	///         return _transactionManager.ExecuteTransactionAsync&lt;MySession&gt;(
	///             async (sessions, token) =&gt;
	///             {
	///                 var session = sessions.GetSession&lt;MySession&gt;();
	///                 await _userAggregateService.ChangeNameAsync(session, req.UserId, req.NewName, req.OperateInfo, token);
	///             },
	///             cancellationToken: cancellationToken);
	///     }
	/// }
	/// </code>
	/// </example>
	/// </remarks>
	public interface ICommandService<TReq> where TReq : ICommandServiceDTO
	{
		/// <summary>
		/// Execute the use case command.
		/// </summary>
		/// <param name="req">Request</param>
		/// <param name="cancellationToken">Cancellation token</param>
		/// <returns></returns>
		ValueTask ExecuteAsync(TReq req, CancellationToken cancellationToken = default);
	}

	/// <summary>
	/// Use case command interface (request and response). One state-changing use case that reports
	/// something back — a generated identifier, for instance.
	/// </summary>
	/// <typeparam name="TReq">Request type</typeparam>
	/// <typeparam name="TRes">Response type</typeparam>
	/// <remarks>
	/// Same contract as <see cref="ICommandService{TReq}"/>; see it for how the transaction is run.
	/// Return a DTO, never an entity: entities belong to the transaction they were read in, and the
	/// session is disposed by the time the caller sees the response.
	/// </remarks>
	public interface ICommandService<TReq, TRes> where TReq : ICommandServiceDTO where TRes : ICommandServiceDTO
	{
		/// <summary>
		/// Execute the use case command.
		/// </summary>
		/// <param name="req">Request</param>
		/// <param name="cancellationToken">Cancellation token</param>
		/// <returns>Response</returns>
		ValueTask<TRes> ExecuteAsync(TReq req, CancellationToken cancellationToken = default);
	}
}