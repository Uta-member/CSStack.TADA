namespace CSStack.TADA
{
	/// <summary>
	/// Exception thrown when a transaction is started while another one is already running on the same
	/// <see cref="ITransactionManager"/> instance.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Transactions must not be nested.</b> The manager keeps the sessions of the transaction in flight in
	/// a single flat state, so an inner
	/// <see cref="ITransactionManager.ExecuteTransactionAsync(System.Collections.Immutable.ImmutableList{Type}, Func{TransactionSessions, CancellationToken, ValueTask}, Func{Exception, ValueTask}, CancellationToken)"/>
	/// would commit and dispose the outer transaction's sessions while the outer body is still running. The
	/// call is rejected instead of breaking the transaction boundary silently.
	/// </para>
	/// <para>
	/// <see cref="ITransactionManager"/> is registered with a scoped lifetime, so a command service that
	/// calls another command service reaches this state through the very same instance.
	/// </para>
	/// </remarks>
	public class NestedTransactionException : TADAException
	{
		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="innerException">Inner exception</param>
		public NestedTransactionException(Exception? innerException = null)
			: base(BuildMessage(), innerException)
		{
		}

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="message">Message</param>
		/// <param name="innerException">Inner exception</param>
		public NestedTransactionException(string message, Exception? innerException = null)
			: base(message, innerException)
		{
		}

		private static string BuildMessage()
		{
			return "A transaction is already running on this ITransactionManager instance. "
				+ "ExecuteTransactionAsync must not be nested: the inner call would commit and dispose the "
				+ "sessions of the outer transaction. The transaction boundary belongs to exactly one "
				+ "ICommandService. To run several operations as one transaction, extract them into a domain "
				+ "service or an aggregate service and call them from the body of the same "
				+ "ExecuteTransactionAsync, passing the session down.";
		}
	}
}
