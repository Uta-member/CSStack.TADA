namespace CSStack.TADA
{
    /// <summary>
    /// Transaction service (async) interface.
    /// </summary>
    /// <typeparam name="TSession">A factor to keep the transaction</typeparam>
    public interface ITransactionService<TSession>
        where TSession : IDisposable
    {
        /// <summary>
        /// Begin a transaction (store the return value in a using variable).
        /// </summary>
        /// <returns>The factor to keep the transaction</returns>
        ValueTask<TSession> BeginAsync();

        /// <summary>
        /// Commit.
        /// </summary>
        /// <param name="session">The factor to keep the transaction</param>
        /// <returns></returns>
        ValueTask CommitAsync(TSession session);

        /// <summary>
        /// Rollback.
        /// </summary>
        /// <param name="session">The factor to keep the transaction</param>
        /// <returns></returns>
        ValueTask RollbackAsync(TSession session);
    }
}
