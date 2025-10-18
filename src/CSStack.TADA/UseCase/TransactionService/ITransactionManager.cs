using System.Collections.Immutable;

namespace CSStack.TADA
{
    /// <summary>
    /// Transaction management interface.
    /// </summary>
    public interface ITransactionManager
    {
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
    }
}
