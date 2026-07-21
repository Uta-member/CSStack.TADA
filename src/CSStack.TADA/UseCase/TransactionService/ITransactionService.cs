namespace CSStack.TADA
{
	/// <summary>
	/// Non-generic base of <see cref="ITransactionService{TSession}"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This interface exists so that <see cref="ITransactionManager"/> can drive transaction services
	/// whose session type is only known at run time, without resorting to <c>dynamic</c>.
	/// </para>
	/// <para>
	/// Do not implement this interface directly. Implement <see cref="ITransactionService{TSession}"/>:
	/// it supplies the explicit implementations of these members for you.
	/// </para>
	/// </remarks>
	public interface ITransactionService
	{
		/// <summary>
		/// Begin a transaction.
		/// </summary>
		/// <param name="cancellationToken">Cancellation token</param>
		/// <returns>The factor that keeps the transaction</returns>
		ValueTask<IDisposable> BeginAsync(CancellationToken cancellationToken = default);

		/// <summary>
		/// Commit.
		/// </summary>
		/// <param name="session">The factor that keeps the transaction</param>
		/// <param name="cancellationToken">Cancellation token</param>
		/// <returns></returns>
		ValueTask CommitAsync(IDisposable session, CancellationToken cancellationToken = default);

		/// <summary>
		/// Rollback.
		/// </summary>
		/// <param name="session">The factor that keeps the transaction</param>
		/// <param name="cancellationToken">Cancellation token</param>
		/// <returns></returns>
		ValueTask RollbackAsync(IDisposable session, CancellationToken cancellationToken = default);
	}

	/// <summary>
	/// Transaction service (async) interface.
	/// </summary>
	/// <typeparam name="TSession">A factor to keep the transaction</typeparam>
	/// <remarks>
	/// The session returned by <see cref="BeginAsync(CancellationToken)"/> is owned by
	/// <see cref="ITransactionManager"/>: the manager disposes it after commit, after rollback and on
	/// any error path. Implementations must not dispose the session inside
	/// <see cref="CommitAsync(TSession, CancellationToken)"/> or
	/// <see cref="RollbackAsync(TSession, CancellationToken)"/>.
	/// </remarks>
	public interface ITransactionService<TSession> : ITransactionService
		where TSession : IDisposable
	{
		/// <summary>
		/// Begin a transaction.
		/// </summary>
		/// <param name="cancellationToken">Cancellation token</param>
		/// <returns>The factor to keep the transaction. The caller (<see cref="ITransactionManager"/>) disposes it.</returns>
		new ValueTask<TSession> BeginAsync(CancellationToken cancellationToken = default);

		/// <summary>
		/// Commit.
		/// </summary>
		/// <param name="session">The factor to keep the transaction</param>
		/// <param name="cancellationToken">Cancellation token</param>
		/// <returns></returns>
		ValueTask CommitAsync(TSession session, CancellationToken cancellationToken = default);

		/// <summary>
		/// Rollback.
		/// </summary>
		/// <param name="session">The factor to keep the transaction</param>
		/// <param name="cancellationToken">Cancellation token</param>
		/// <returns></returns>
		ValueTask RollbackAsync(TSession session, CancellationToken cancellationToken = default);

		async ValueTask<IDisposable> ITransactionService.BeginAsync(CancellationToken cancellationToken)
		{
			return await BeginAsync(cancellationToken).ConfigureAwait(false);
		}

		ValueTask ITransactionService.CommitAsync(IDisposable session, CancellationToken cancellationToken)
		{
			return CommitAsync((TSession)session, cancellationToken);
		}

		ValueTask ITransactionService.RollbackAsync(IDisposable session, CancellationToken cancellationToken)
		{
			return RollbackAsync((TSession)session, cancellationToken);
		}
	}
}
