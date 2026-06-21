using System.Collections.Immutable;

namespace CSStack.TADA
{
	/// <summary>
	/// Transaction management interface.
	/// </summary>
	public interface ITransactionManager
	{
		/// <summary>
		/// Begin a transaction.
		/// </summary>
		/// <typeparam name="TSession"></typeparam>
		/// <returns></returns>
		ValueTask BeginTransactionAsync<TSession>() where TSession : IDisposable;

		/// <summary>
		/// Commit transactions.
		/// </summary>
		ValueTask CommitTransactionsAsync();

		/// <summary>
		/// Execute a transaction.
		/// </summary>
		/// <param name="sessionTypes"></param>
		/// <param name="transactionFunction"></param>
		/// <param name="beforeRollbackHandler"></param>
		/// <returns></returns>
		ValueTask ExecuteTransactionAsync(
			ImmutableList<Type> sessionTypes,
			Func<TransactionSessions, ValueTask> transactionFunction,
			Func<Exception, ValueTask>? beforeRollbackHandler = null);

		/// <summary>
		/// Get transaction session factor.
		/// </summary>
		/// <typeparam name="TSession"></typeparam>
		/// <returns></returns>
		TSession GetSession<TSession>() where TSession : IDisposable;

		/// <summary>
		/// Get transaction provider service
		/// </summary>
		/// <typeparam name="TSession"></typeparam>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException"></exception>
		ITransactionService<TSession> GetTransactionService<TSession>() where TSession : IDisposable;

		/// <summary>
		/// Rollback transactions.
		/// </summary>
		ValueTask RollbackTransactionsAsync();
	}
}
