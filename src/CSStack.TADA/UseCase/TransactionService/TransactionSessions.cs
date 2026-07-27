using System.Diagnostics.CodeAnalysis;

namespace CSStack.TADA
{
	/// <summary>
	/// The set of transaction sessions handed to the body of
	/// <see cref="ITransactionManager.ExecuteTransactionAsync(System.Collections.Immutable.ImmutableList{Type}, Func{TransactionSessions, CancellationToken, ValueTask}, Func{Exception, ValueTask}, CancellationToken)"/>.
	/// </summary>
	/// <remarks>
	/// Valid only for the duration of the call it was handed to. The sessions are owned and disposed by
	/// <see cref="ITransactionManager"/>, so this object must not be captured and used afterwards: it is a
	/// snapshot taken when the call started, and it keeps naming the sessions after they have been committed
	/// and disposed.
	/// </remarks>
	public sealed class TransactionSessions
	{
		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="sessions">Sessions keyed by session type</param>
		public TransactionSessions(IReadOnlyDictionary<Type, IDisposable> sessions)
		{
			ArgumentNullException.ThrowIfNull(sessions);
			Sessions = sessions;
		}

		/// <summary>
		/// Sessions keyed by session type.
		/// </summary>
		public IReadOnlyDictionary<Type, IDisposable> Sessions { get; }

		/// <summary>
		/// Get a session.
		/// </summary>
		/// <typeparam name="TSession">Session type</typeparam>
		/// <returns>The session of the requested type</returns>
		/// <exception cref="TransactionSessionNotFoundException">
		/// The transaction for <typeparamref name="TSession"/> has not been begun.
		/// </exception>
		public TSession GetSession<TSession>() where TSession : IDisposable
		{
			if (!Sessions.TryGetValue(typeof(TSession), out var session))
			{
				throw new TransactionSessionNotFoundException(typeof(TSession));
			}
			return (TSession)session;
		}

		/// <summary>
		/// Try to get a session.
		/// </summary>
		/// <typeparam name="TSession">Session type</typeparam>
		/// <param name="session">The session of the requested type, or the default value when it has not been begun</param>
		/// <returns><see langword="true"/> when the session has been begun</returns>
		public bool TryGetSession<TSession>([MaybeNullWhen(false)] out TSession session) where TSession : IDisposable
		{
			if (Sessions.TryGetValue(typeof(TSession), out var value) && value is TSession typedSession)
			{
				session = typedSession;
				return true;
			}
			session = default;
			return false;
		}
	}
}
