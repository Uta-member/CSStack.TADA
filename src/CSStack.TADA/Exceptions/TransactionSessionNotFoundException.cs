namespace CSStack.TADA
{
	/// <summary>
	/// Exception thrown when a transaction session is requested before it has been begun.
	/// </summary>
	/// <remarks>
	/// The usual cause is that the session type was not passed to
	/// <see cref="ITransactionManager.ExecuteTransactionAsync(System.Collections.Immutable.ImmutableList{Type}, Func{TransactionSessions, CancellationToken, ValueTask}, Func{Exception, ValueTask}, CancellationToken)"/>.
	/// </remarks>
	public class TransactionSessionNotFoundException : TADAException
	{
		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="sessionType">The session type that was requested but has not been begun</param>
		/// <param name="innerException">Inner exception</param>
		public TransactionSessionNotFoundException(Type sessionType, Exception? innerException = null)
			: base(BuildMessage(sessionType), innerException)
		{
			SessionType = sessionType;
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="message">Message</param>
		/// <param name="innerException">Inner exception</param>
		public TransactionSessionNotFoundException(string message, Exception? innerException = null)
			: base(message, innerException)
		{
		}

		/// <summary>
		/// The session type that was requested but has not been begun.
		/// <see langword="null"/> when the exception was created from a raw message.
		/// </summary>
		public Type? SessionType { get; }

		private static string BuildMessage(Type sessionType)
		{
			ArgumentNullException.ThrowIfNull(sessionType);
			return $"The transaction session '{sessionType.FullName}' has not been begun. "
				+ $"Pass typeof({sessionType.Name}) to the sessionTypes argument of ExecuteTransactionAsync, "
				+ $"use the generic overload ExecuteTransactionAsync<{sessionType.Name}>(...), "
				+ $"or call BeginTransactionAsync<{sessionType.Name}>() before accessing the session.";
		}
	}
}
