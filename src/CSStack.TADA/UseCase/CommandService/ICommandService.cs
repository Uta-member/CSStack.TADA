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
	/// <para>
	/// <b>The session type stays a type parameter here too, and it is not called <c>TSession</c>.</b>
	/// A use case commonly spans several aggregates, and aggregates written to different stores have
	/// different session types, so the parameter is named after the aggregate — <c>TUserSession</c>,
	/// <c>TOrderSession</c> — which also reads correctly when they line up as
	/// <c>ExecuteTransactionAsync&lt;TUserSession, TOrderSession&gt;</c>. Do this from the start, even
	/// while only one aggregate is involved. The concrete types are chosen where the use case is bound
	/// to its repositories: the constructor call, or the DI registration in the composition root. See
	/// <see cref="IRepository{TEntity, TEntityIdentifier, TOperateInfo, TSession}"/>. The request and
	/// response DTOs carry no session and therefore take no type parameter.
	/// </para>
	/// <para>
	/// <b>Declare an interface per use case and derive it from this one — without a session type
	/// parameter — and nest the request and response in it as <c>Req</c> and <c>Res</c>.</b> The
	/// implementation is generic over the session, so resolving the use case through
	/// <c>ICommandService&lt;TReq, TRes&gt;</c> would make the presentation layer name
	/// <c>ChangeUserNameCommandService&lt;AppSession&gt;</c>. With an
	/// <c>IChangeUserNameCommandService : ICommandService&lt;IChangeUserNameCommandService.Req&gt;</c> in
	/// front of it, callers resolve that and the type argument appears in the DI registration alone;
	/// tests substitute the interface. See <see cref="ICommandServiceDTO"/> for why the DTOs belong
	/// inside it. Take the layers below as interfaces too — the aggregate service and the domain service
	/// each through their own.
	/// </para>
	/// <example>
	/// <code>
	/// public interface IChangeUserNameCommandService : ICommandService&lt;IChangeUserNameCommandService.Req&gt;
	/// {
	///     sealed record Req(Guid UserId, string NewName, OperateInfo OperateInfo) : ICommandServiceDTO;
	/// }
	///
	/// public sealed class ChangeUserNameCommandService&lt;TUserSession&gt;
	///     : IChangeUserNameCommandService
	///     where TUserSession : IDisposable
	/// {
	///     private readonly ITransactionManager _transactionManager;
	///     private readonly IUserAggregateService&lt;TUserSession&gt; _userAggregateService;
	///
	///     public ChangeUserNameCommandService(
	///         ITransactionManager transactionManager,
	///         IUserAggregateService&lt;TUserSession&gt; userAggregateService)
	///     {
	///         _transactionManager = transactionManager;
	///         _userAggregateService = userAggregateService;
	///     }
	///
	///     public ValueTask ExecuteAsync(
	///         IChangeUserNameCommandService.Req req,
	///         CancellationToken cancellationToken = default)
	///     {
	///         return _transactionManager.ExecuteTransactionAsync&lt;TUserSession&gt;(
	///             async (sessions, token) =&gt;
	///             {
	///                 var session = sessions.GetSession&lt;TUserSession&gt;();
	///                 await _userAggregateService.ChangeNameAsync(
	///                     session, UserId.Create(req.UserId), UserName.Create(req.NewName), req.OperateInfo, token);
	///             },
	///             cancellationToken: cancellationToken);
	///     }
	/// }
	///
	/// // Presentation layer — the only place the concrete session type appears
	/// services.AddScoped&lt;IUserAggregateService&lt;AppSession&gt;, UserAggregateService&lt;AppSession&gt;&gt;();
	/// services.AddScoped&lt;IChangeUserNameCommandService, ChangeUserNameCommandService&lt;AppSession&gt;&gt;();
	///
	/// // ... and the caller never names it
	/// var commandService = scope.ServiceProvider.GetRequiredService&lt;IChangeUserNameCommandService&gt;();
	/// await commandService.ExecuteAsync(new IChangeUserNameCommandService.Req(userId, "robert", operateInfo));
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
		/// <returns>A task that completes once the transaction opened by this use case has been
		/// committed</returns>
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
