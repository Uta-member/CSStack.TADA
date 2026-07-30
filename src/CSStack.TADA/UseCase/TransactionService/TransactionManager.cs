using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;

namespace CSStack.TADA
{
	/// <summary>
	/// Default <see cref="ITransactionManager"/> implementation, resolving
	/// <see cref="ITransactionService{TSession}"/> from an <see cref="IServiceProvider"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Register this type with a scoped lifetime.</b> It keeps the sessions of the transaction currently
	/// in flight in mutable state and is <b>not thread-safe</b>. A singleton registration makes concurrent
	/// requests share — and corrupt — each other's sessions. A single instance must also not be driven from
	/// several threads or from concurrent <c>Task</c>s at once.
	/// </para>
	/// <para>
	/// <b>This instance owns the sessions.</b> Every session it begins is disposed after commit, after
	/// rollback and on any error path. <see cref="ITransactionService{TSession}"/> implementations must not
	/// dispose sessions themselves.
	/// </para>
	/// <para>
	/// <b>Commits across multiple sessions are not atomic.</b> See <see cref="ITransactionManager"/>.
	/// </para>
	/// <para>
	/// <b>Transactions must not be nested.</b> Calling <see cref="ExecuteTransactionAsync(ImmutableList{Type}, Func{TransactionSessions, CancellationToken, ValueTask}, Func{Exception, ValueTask}, CancellationToken)"/>
	/// from the body of another one throws <see cref="NestedTransactionException"/>. Consecutive
	/// (non-nested) transactions on the same instance are fine.
	/// </para>
	/// </remarks>
	public sealed class TransactionManager : ITransactionManager
	{
		/// <summary>
		/// Whether an <see cref="ExecuteTransactionAsync(ImmutableList{Type}, Func{TransactionSessions, CancellationToken, ValueTask}, Func{Exception, ValueTask}, CancellationToken)"/>
		/// call is in flight. Set for the whole call, including the rollback, and always cleared afterwards.
		/// </summary>
		private bool _isExecutingTransaction;

		private readonly IServiceProvider _serviceProvider;

		/// <summary>
		/// Sessions that have begun, keyed by session type.
		/// </summary>
		private readonly Dictionary<Type, IDisposable> _sessions = new();

		/// <summary>
		/// The session types in the order they were begun. Commit follows this order, rollback and dispose reverse it.
		/// </summary>
		private readonly List<Type> _sessionOrder = new();

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="serviceProvider">The provider the <see cref="ITransactionService{TSession}"/> instances are resolved from</param>
		public TransactionManager(IServiceProvider serviceProvider)
		{
			ArgumentNullException.ThrowIfNull(serviceProvider);
			_serviceProvider = serviceProvider;
		}

		/// <inheritdoc/>
		public ValueTask BeginTransactionAsync<TSession>(CancellationToken cancellationToken = default)
			where TSession : IDisposable
		{
			return BeginTransactionAsync(typeof(TSession), cancellationToken);
		}

		/// <inheritdoc/>
		public async ValueTask BeginTransactionAsync(Type sessionType, CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(sessionType);
			if (_sessions.ContainsKey(sessionType))
			{
				return;
			}
			var transactionService = GetTransactionService(sessionType);
			var session = await transactionService.BeginAsync(cancellationToken).ConfigureAwait(false);
			_sessions.Add(sessionType, session);
			_sessionOrder.Add(sessionType);
		}

		/// <inheritdoc/>
		public async ValueTask BeginTransactionsAsync(
			ImmutableList<Type> sessionTypes,
			CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(sessionTypes);
			try
			{
				foreach (var sessionType in sessionTypes)
				{
					await BeginTransactionAsync(sessionType, cancellationToken).ConfigureAwait(false);
				}
			}
			catch (Exception exception)
			{
				var errors = new List<Exception> { exception };

				try
				{
					// The sessions begun before the failure must not leak. The token is deliberately not
					// forwarded: a cancelled token must not stop the cleanup.
					await RollbackTransactionsAsync(CancellationToken.None).ConfigureAwait(false);
				}
				catch (Exception rollbackException)
				{
					errors.Add(rollbackException);
				}

				if (errors.Count == 1)
				{
					throw;
				}
				throw new AggregateException(
					"Failed to begin the transactions and the rollback of the sessions already begun also failed. "
					+ "The first inner exception is the original failure.",
					errors);
			}
		}

		/// <inheritdoc/>
		public async ValueTask CommitTransactionsAsync(CancellationToken cancellationToken = default)
		{
			var sessions = SnapshotSessions();
			var errors = new List<Exception>();

			for (var i = 0; i < sessions.Count; i++)
			{
				try
				{
					var transactionService = GetTransactionService(sessions[i].Key);
					await transactionService.CommitAsync(sessions[i].Value, cancellationToken).ConfigureAwait(false);
				}
				catch (Exception exception)
				{
					errors.Add(exception);

					// The sessions committed before this one cannot be undone. Roll back the rest — including the
					// one that just failed — in reverse order. The token is deliberately not forwarded: a cancelled
					// token must not stop the cleanup.
					var notCommitted = sessions.Skip(i).Reverse().ToList();
					errors.AddRange(await RollbackCoreAsync(notCommitted, CancellationToken.None).ConfigureAwait(false));
					break;
				}
			}

			errors.AddRange(DisposeSessionsCore());
			ThrowIfAny(errors, "Failed to commit the transactions.");
		}

		/// <inheritdoc/>
		public async ValueTask RollbackTransactionsAsync(CancellationToken cancellationToken = default)
		{
			var sessions = SnapshotSessions();
			sessions.Reverse();

			var errors = await RollbackCoreAsync(sessions, cancellationToken).ConfigureAwait(false);
			errors.AddRange(DisposeSessionsCore());
			ThrowIfAny(errors, "Failed to roll back the transactions.");
		}

		/// <inheritdoc/>
		public async ValueTask ExecuteTransactionAsync(
			ImmutableList<Type> sessionTypes,
			Func<TransactionSessions, CancellationToken, ValueTask> transactionFunction,
			Func<Exception, ValueTask>? beforeRollbackHandler = null,
			CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(sessionTypes);
			ArgumentNullException.ThrowIfNull(transactionFunction);

			// Before the try: the nested call must not run the cleanup of the transaction already in flight.
			if (_isExecutingTransaction)
			{
				throw new NestedTransactionException();
			}
			_isExecutingTransaction = true;

			try
			{
				await BeginTransactionsAsync(sessionTypes, cancellationToken).ConfigureAwait(false);

				// A copy, not the live dictionary: what the body can reach must not change when it begins
				// further sessions, and must not empty out when the sessions are committed.
				await transactionFunction
					.Invoke(new TransactionSessions(new Dictionary<Type, IDisposable>(_sessions)), cancellationToken)
					.ConfigureAwait(false);
				await CommitTransactionsAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (Exception exception)
			{
				var errors = new List<Exception> { exception };

				if (beforeRollbackHandler is not null)
				{
					try
					{
						await beforeRollbackHandler.Invoke(exception).ConfigureAwait(false);
					}
					catch (Exception handlerException)
					{
						// A failing handler must not prevent the rollback.
						errors.Add(handlerException);
					}
				}

				try
				{
					// Not forwarding the token: a cancelled operation must still be rolled back.
					// A no-op when the failure came from CommitTransactionsAsync, which already cleaned up.
					await RollbackTransactionsAsync(CancellationToken.None).ConfigureAwait(false);
				}
				catch (Exception rollbackException)
				{
					errors.Add(rollbackException);
				}

				if (errors.Count == 1)
				{
					throw;
				}
				throw new AggregateException(
					"The transaction failed and the recovery also failed. The first inner exception is the original failure.",
					errors);
			}
			finally
			{
				// Safety net for paths that reached neither commit nor rollback.
				DisposeSessionsCore();
				_isExecutingTransaction = false;
			}
		}

		/// <inheritdoc/>
		public ValueTask ExecuteTransactionAsync<TSession1>(
			Func<TransactionSessions, CancellationToken, ValueTask> transactionFunction,
			Func<Exception, ValueTask>? beforeRollbackHandler = null,
			CancellationToken cancellationToken = default)
			where TSession1 : IDisposable
		{
			return ExecuteTransactionAsync(
				ImmutableList.Create(typeof(TSession1)),
				transactionFunction,
				beforeRollbackHandler,
				cancellationToken);
		}

		/// <inheritdoc/>
		public ValueTask ExecuteTransactionAsync<TSession1, TSession2>(
			Func<TransactionSessions, CancellationToken, ValueTask> transactionFunction,
			Func<Exception, ValueTask>? beforeRollbackHandler = null,
			CancellationToken cancellationToken = default)
			where TSession1 : IDisposable
			where TSession2 : IDisposable
		{
			return ExecuteTransactionAsync(
				ImmutableList.Create(typeof(TSession1), typeof(TSession2)),
				transactionFunction,
				beforeRollbackHandler,
				cancellationToken);
		}

		/// <inheritdoc/>
		public ValueTask ExecuteTransactionAsync<TSession1, TSession2, TSession3>(
			Func<TransactionSessions, CancellationToken, ValueTask> transactionFunction,
			Func<Exception, ValueTask>? beforeRollbackHandler = null,
			CancellationToken cancellationToken = default)
			where TSession1 : IDisposable
			where TSession2 : IDisposable
			where TSession3 : IDisposable
		{
			return ExecuteTransactionAsync(
				ImmutableList.Create(typeof(TSession1), typeof(TSession2), typeof(TSession3)),
				transactionFunction,
				beforeRollbackHandler,
				cancellationToken);
		}

		/// <inheritdoc/>
		public TSession GetSession<TSession>() where TSession : IDisposable
		{
			return (TSession)GetSession(typeof(TSession));
		}

		/// <inheritdoc/>
		public IDisposable GetSession(Type sessionType)
		{
			ArgumentNullException.ThrowIfNull(sessionType);
			if (!_sessions.TryGetValue(sessionType, out var session))
			{
				throw new TransactionSessionNotFoundException(sessionType);
			}
			return session;
		}

		/// <inheritdoc/>
		public bool TryGetSession<TSession>([MaybeNullWhen(false)] out TSession session) where TSession : IDisposable
		{
			if (_sessions.TryGetValue(typeof(TSession), out var value) && value is TSession typedSession)
			{
				session = typedSession;
				return true;
			}
			session = default;
			return false;
		}

		/// <inheritdoc/>
		public ITransactionService<TSession> GetTransactionService<TSession>() where TSession : IDisposable
		{
			return (ITransactionService<TSession>)GetTransactionService(typeof(TSession));
		}

		/// <inheritdoc/>
		public ITransactionService GetTransactionService(Type sessionType)
		{
			ArgumentNullException.ThrowIfNull(sessionType);
			if (!typeof(IDisposable).IsAssignableFrom(sessionType))
			{
				throw new ArgumentException(
					$"The session type '{sessionType.FullName}' must implement {nameof(IDisposable)}.",
					nameof(sessionType));
			}

			var serviceType = typeof(ITransactionService<>).MakeGenericType(sessionType);
			var service = _serviceProvider.GetService(serviceType);
			if (service is null)
			{
				throw new InvalidOperationException(
					$"No transaction service is registered for the session type '{sessionType.FullName}'. "
					+ $"Register ITransactionService<{sessionType.Name}> with the service provider.");
			}
			return (ITransactionService)service;
		}

		/// <summary>
		/// The sessions that have begun, in the order they were begun.
		/// </summary>
		private List<KeyValuePair<Type, IDisposable>> SnapshotSessions()
		{
			var snapshot = new List<KeyValuePair<Type, IDisposable>>(_sessionOrder.Count);
			foreach (var sessionType in _sessionOrder)
			{
				snapshot.Add(new KeyValuePair<Type, IDisposable>(sessionType, _sessions[sessionType]));
			}
			return snapshot;
		}

		/// <summary>
		/// Roll back every given session. One failure never stops the others; all failures are returned.
		/// </summary>
		private async ValueTask<List<Exception>> RollbackCoreAsync(
			IReadOnlyList<KeyValuePair<Type, IDisposable>> sessions,
			CancellationToken cancellationToken)
		{
			var exceptions = new List<Exception>();
			foreach (var session in sessions)
			{
				try
				{
					var transactionService = GetTransactionService(session.Key);
					await transactionService.RollbackAsync(session.Value, cancellationToken).ConfigureAwait(false);
				}
				catch (Exception exception)
				{
					exceptions.Add(exception);
				}
			}
			return exceptions;
		}

		/// <summary>
		/// Dispose every session held, in reverse order of begin, and forget them all.
		/// Always empties the state, even when a <see cref="IDisposable.Dispose"/> throws.
		/// </summary>
		private List<Exception> DisposeSessionsCore()
		{
			var exceptions = new List<Exception>();
			for (var i = _sessionOrder.Count - 1; i >= 0; i--)
			{
				if (!_sessions.TryGetValue(_sessionOrder[i], out var session))
				{
					continue;
				}
				try
				{
					session.Dispose();
				}
				catch (Exception exception)
				{
					exceptions.Add(exception);
				}
			}
			_sessions.Clear();
			_sessionOrder.Clear();
			return exceptions;
		}

		/// <summary>
		/// Rethrow a lone failure as-is, or bundle several into an <see cref="AggregateException"/>.
		/// </summary>
		private static void ThrowIfAny(List<Exception> exceptions, string message)
		{
			if (exceptions.Count == 0)
			{
				return;
			}
			if (exceptions.Count == 1)
			{
				ExceptionDispatchInfo.Capture(exceptions[0]).Throw();
			}
			throw new AggregateException(message, exceptions);
		}
	}
}
