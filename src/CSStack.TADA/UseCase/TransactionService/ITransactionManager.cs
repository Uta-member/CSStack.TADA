using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace CSStack.TADA
{
	/// <summary>
	/// Transaction management interface.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Multiple sessions are not committed atomically.</b> This is not a two-phase commit coordinator:
	/// sessions are committed one after another, so a failure on the second session leaves the first one
	/// already committed. The manager then tries to roll back every session that has not been committed yet,
	/// but the committed ones cannot be undone. Design your use cases so that at most one session needs to
	/// be durable, or make the operation idempotent and retryable.
	/// </para>
	/// <para>
	/// <b>Session ownership.</b> Sessions belong to the manager. It disposes every session it began
	/// after commit, after rollback and on any error path. <see cref="ITransactionService{TSession}"/>
	/// implementations must not dispose sessions themselves.
	/// </para>
	/// <para>
	/// <b>Lifetime.</b> Implementations hold the sessions of the transaction currently in flight and are
	/// not thread-safe. Register them with a <i>scoped</i> lifetime — a singleton registration mixes
	/// sessions across concurrent requests.
	/// </para>
	/// <para>
	/// <b>Transactions must not be nested.</b> The sessions in flight are held per manager, not per
	/// <see cref="ExecuteTransactionAsync(ImmutableList{Type}, Func{TransactionSessions, CancellationToken, ValueTask}, Func{Exception, ValueTask}, CancellationToken)"/>
	/// call, so an inner call would commit and dispose the outer transaction's sessions while the outer body
	/// is still running. It throws <see cref="NestedTransactionException"/> instead. Because the manager is
	/// scoped, a command service calling another command service hits exactly this. Extract the shared work
	/// into a domain service or an aggregate service and call it from the body of the same transaction.
	/// Consecutive (non-nested) transactions on the same instance are fine.
	/// </para>
	/// </remarks>
	public interface ITransactionManager
	{
		/// <summary>
		/// Begin a transaction. Does nothing when the transaction for <typeparamref name="TSession"/> has already begun.
		/// </summary>
		/// <typeparam name="TSession">Session type</typeparam>
		/// <param name="cancellationToken">Cancellation token</param>
		/// <returns>A task that completes once the session has begun and is retrievable with
		/// <see cref="GetSession{TSession}"/></returns>
		/// <remarks>
		/// A failure here leaves the sessions begun by earlier calls open. When driving the manager by hand
		/// rather than through
		/// <see cref="ExecuteTransactionAsync(ImmutableList{Type}, Func{TransactionSessions, CancellationToken, ValueTask}, Func{Exception, ValueTask}, CancellationToken)"/>,
		/// clean them up with <see cref="RollbackTransactionsAsync"/>.
		/// </remarks>
		/// <exception cref="InvalidOperationException">
		/// No <see cref="ITransactionService{TSession}"/> is registered for <typeparamref name="TSession"/>.
		/// </exception>
		ValueTask BeginTransactionAsync<TSession>(CancellationToken cancellationToken = default)
			where TSession : IDisposable;

		/// <summary>
		/// Begin a transaction. Does nothing when the transaction for <paramref name="sessionType"/> has already begun.
		/// </summary>
		/// <param name="sessionType">Session type. Must implement <see cref="IDisposable"/>.</param>
		/// <param name="cancellationToken">Cancellation token</param>
		/// <returns>A task that completes once the session has begun and is retrievable with
		/// <see cref="GetSession(Type)"/></returns>
		/// <remarks>
		/// A failure here leaves the sessions begun by earlier calls open. When driving the manager by hand
		/// rather than through
		/// <see cref="ExecuteTransactionAsync(ImmutableList{Type}, Func{TransactionSessions, CancellationToken, ValueTask}, Func{Exception, ValueTask}, CancellationToken)"/>,
		/// clean them up with <see cref="RollbackTransactionsAsync"/>.
		/// </remarks>
		/// <exception cref="ArgumentException"><paramref name="sessionType"/> does not implement <see cref="IDisposable"/>.</exception>
		/// <exception cref="InvalidOperationException">
		/// No <see cref="ITransactionService{TSession}"/> is registered for <paramref name="sessionType"/>.
		/// </exception>
		ValueTask BeginTransactionAsync(Type sessionType, CancellationToken cancellationToken = default);

		/// <summary>
		/// Begin multiple transactions, in the given order.
		/// </summary>
		/// <param name="sessionTypes">Session types. Each must implement <see cref="IDisposable"/>.</param>
		/// <param name="cancellationToken">Cancellation token</param>
		/// <returns>A task that completes once every session has begun</returns>
		/// <remarks>
		/// When one of them fails to begin, the sessions already begun — including those begun before this
		/// call — are rolled back and disposed before the failure is rethrown, so nothing is left open
		/// (the rollback is never cancelled, even when <paramref name="cancellationToken"/> is already
		/// cancelled).
		/// </remarks>
		/// <exception cref="AggregateException">
		/// A session failed to begin <i>and</i> the rollback failed as well. The begin failure is the first
		/// inner exception. When only the begin failed it is rethrown as-is.
		/// </exception>
		ValueTask BeginTransactionsAsync(ImmutableList<Type> sessionTypes, CancellationToken cancellationToken = default);

		/// <summary>
		/// Commit every session that has begun, in the order they were begun, then dispose them all.
		/// </summary>
		/// <param name="cancellationToken">Cancellation token</param>
		/// <returns>A task that completes once every session has been committed and disposed</returns>
		/// <remarks>
		/// Commits are not atomic across sessions. When one commit fails, the sessions committed before it stay
		/// committed, and the remaining sessions are rolled back on a best-effort basis (rollback is never
		/// cancelled, even when <paramref name="cancellationToken"/> is already cancelled).
		/// The sessions are disposed and forgotten either way.
		/// </remarks>
		/// <exception cref="AggregateException">
		/// More than one failure occurred (for example a commit failure plus a rollback failure).
		/// When only one failure occurred it is rethrown as-is.
		/// </exception>
		ValueTask CommitTransactionsAsync(CancellationToken cancellationToken = default);

		/// <summary>
		/// Roll back every session that has begun, in reverse order, then dispose them all.
		/// </summary>
		/// <param name="cancellationToken">Cancellation token</param>
		/// <returns>A task that completes once every session has been rolled back and disposed</returns>
		/// <remarks>
		/// A failure on one session does not stop the others: every session is always attempted, and the
		/// failures are reported together afterwards.
		/// </remarks>
		/// <exception cref="AggregateException">
		/// More than one session failed to roll back or to dispose. A single failure is rethrown as-is.
		/// </exception>
		ValueTask RollbackTransactionsAsync(CancellationToken cancellationToken = default);

		/// <summary>
		/// Begin the given transactions, run <paramref name="transactionFunction"/>, then commit.
		/// Rolls back and rethrows when anything fails.
		/// </summary>
		/// <param name="sessionTypes">
		/// Session types to begin, in this order. Each must implement <see cref="IDisposable"/>. The
		/// <see cref="TransactionSessions"/> handed to <paramref name="transactionFunction"/> is a snapshot
		/// taken once these have begun, so asking it for a session that is not among them throws
		/// <see cref="TransactionSessionNotFoundException"/> — that stays true even when the body begins
		/// another session through the manager afterwards.
		/// </param>
		/// <param name="transactionFunction">
		/// The body of the transaction. It receives the sessions that were begun and the cancellation token.
		/// Everything it does succeeds or fails as one unit — the commit happens after it returns, and any
		/// exception it throws triggers the rollback. It must not commit, roll back or dispose the sessions
		/// itself.
		/// </param>
		/// <param name="beforeRollbackHandler">
		/// Invoked with the failure. Use it to capture diagnostics; do not use it to undo work, and note the
		/// transaction is about to be rolled back — or has already been rolled back — regardless of what it
		/// does. A handler that throws does <b>not</b> prevent the rollback: its exception is collected and
		/// reported alongside the original failure.
		/// <para>
		/// <b>When it runs depends on what failed.</b> When the failure came from
		/// <paramref name="transactionFunction"/> (or from beginning the sessions), it runs <i>before</i> the
		/// rollback, so it can still observe the transaction through the sessions. When the body succeeded
		/// and the <b>commit</b> failed, it runs <i>after</i> the rollback, because
		/// <see cref="CommitTransactionsAsync"/> rolls back and disposes the sessions itself before it
		/// throws — the sessions are already disposed by then, so a handler that touches them must be
		/// prepared for that.
		/// </para>
		/// </param>
		/// <param name="cancellationToken">Cancellation token</param>
		/// <returns>A task that completes once the body has run and every session has been committed</returns>
		/// <remarks>
		/// The rollback is always attempted with an uncancelled token, so cancelling
		/// <paramref name="cancellationToken"/> does not leave transactions open.
		/// Sessions are disposed on every path.
		/// <para>
		/// <b>Must not be nested.</b> Calling this from the body (or from
		/// <paramref name="beforeRollbackHandler"/>) of another transaction on the same manager throws
		/// <see cref="NestedTransactionException"/>. Consecutive calls are fine.
		/// </para>
		/// </remarks>
		/// <exception cref="NestedTransactionException">
		/// A transaction is already running on this manager instance.
		/// </exception>
		/// <exception cref="AggregateException">
		/// The body failed <i>and</i> <paramref name="beforeRollbackHandler"/> or the rollback failed as well.
		/// The body's exception is the first inner exception. When only the body failed it is rethrown as-is.
		/// </exception>
		ValueTask ExecuteTransactionAsync(
			ImmutableList<Type> sessionTypes,
			Func<TransactionSessions, CancellationToken, ValueTask> transactionFunction,
			Func<Exception, ValueTask>? beforeRollbackHandler = null,
			CancellationToken cancellationToken = default);

		/// <inheritdoc cref="ExecuteTransactionAsync(ImmutableList{Type}, Func{TransactionSessions, CancellationToken, ValueTask}, Func{Exception, ValueTask}, CancellationToken)"/>
		/// <typeparam name="TSession1">Session type to begin</typeparam>
		ValueTask ExecuteTransactionAsync<TSession1>(
			Func<TransactionSessions, CancellationToken, ValueTask> transactionFunction,
			Func<Exception, ValueTask>? beforeRollbackHandler = null,
			CancellationToken cancellationToken = default)
			where TSession1 : IDisposable;

		/// <inheritdoc cref="ExecuteTransactionAsync{TSession1}(Func{TransactionSessions, CancellationToken, ValueTask}, Func{Exception, ValueTask}, CancellationToken)"/>
		/// <typeparam name="TSession1">First session type to begin</typeparam>
		/// <typeparam name="TSession2">Second session type to begin</typeparam>
		ValueTask ExecuteTransactionAsync<TSession1, TSession2>(
			Func<TransactionSessions, CancellationToken, ValueTask> transactionFunction,
			Func<Exception, ValueTask>? beforeRollbackHandler = null,
			CancellationToken cancellationToken = default)
			where TSession1 : IDisposable
			where TSession2 : IDisposable;

		/// <inheritdoc cref="ExecuteTransactionAsync{TSession1}(Func{TransactionSessions, CancellationToken, ValueTask}, Func{Exception, ValueTask}, CancellationToken)"/>
		/// <typeparam name="TSession1">First session type to begin</typeparam>
		/// <typeparam name="TSession2">Second session type to begin</typeparam>
		/// <typeparam name="TSession3">Third session type to begin</typeparam>
		ValueTask ExecuteTransactionAsync<TSession1, TSession2, TSession3>(
			Func<TransactionSessions, CancellationToken, ValueTask> transactionFunction,
			Func<Exception, ValueTask>? beforeRollbackHandler = null,
			CancellationToken cancellationToken = default)
			where TSession1 : IDisposable
			where TSession2 : IDisposable
			where TSession3 : IDisposable;

		/// <summary>
		/// Get a session that has begun.
		/// </summary>
		/// <typeparam name="TSession">Session type</typeparam>
		/// <returns>The session of the requested type</returns>
		/// <exception cref="TransactionSessionNotFoundException">
		/// The transaction for <typeparamref name="TSession"/> has not been begun.
		/// </exception>
		TSession GetSession<TSession>() where TSession : IDisposable;

		/// <summary>
		/// Get a session that has begun.
		/// </summary>
		/// <param name="sessionType">Session type</param>
		/// <returns>The session of the requested type</returns>
		/// <exception cref="TransactionSessionNotFoundException">
		/// The transaction for <paramref name="sessionType"/> has not been begun.
		/// </exception>
		IDisposable GetSession(Type sessionType);

		/// <summary>
		/// Try to get a session that has begun.
		/// </summary>
		/// <typeparam name="TSession">Session type</typeparam>
		/// <param name="session">The session of the requested type, or the default value when it has not been begun</param>
		/// <returns><see langword="true"/> when the session has been begun</returns>
		bool TryGetSession<TSession>([MaybeNullWhen(false)] out TSession session) where TSession : IDisposable;

		/// <summary>
		/// Get the transaction service registered for the session type.
		/// </summary>
		/// <typeparam name="TSession">Session type</typeparam>
		/// <returns>The registered transaction service</returns>
		/// <exception cref="InvalidOperationException">
		/// No <see cref="ITransactionService{TSession}"/> is registered for <typeparamref name="TSession"/>.
		/// </exception>
		ITransactionService<TSession> GetTransactionService<TSession>() where TSession : IDisposable;

		/// <summary>
		/// Get the transaction service registered for the session type.
		/// </summary>
		/// <param name="sessionType">Session type. Must implement <see cref="IDisposable"/>.</param>
		/// <returns>The registered transaction service</returns>
		/// <exception cref="ArgumentException"><paramref name="sessionType"/> does not implement <see cref="IDisposable"/>.</exception>
		/// <exception cref="InvalidOperationException">
		/// No <see cref="ITransactionService{TSession}"/> is registered for <paramref name="sessionType"/>.
		/// </exception>
		ITransactionService GetTransactionService(Type sessionType);
	}
}
