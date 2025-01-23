using System.Collections.Immutable;

namespace CSStack.TADA
{
    /// <summary>
    /// トランザクション管理インターフェース
    /// </summary>
    public interface ITransactionManager
    {
        /// <summary>
        /// トランザクション実行
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
