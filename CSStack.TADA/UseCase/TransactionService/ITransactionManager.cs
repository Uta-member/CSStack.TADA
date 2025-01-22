using System.Collections.Immutable;

namespace CSStack.TADA
{
    /// <summary>
    /// トランザクション管理インターフェース
    /// </summary>
    public interface ITransactionManager<TSessionIdentifier> where TSessionIdentifier : Enum
    {
        /// <summary>
        /// トランザクションを実行する
        /// </summary>
        /// <param name="transactionTargets"></param>
        /// <param name="transactionAsyncFunction"></param>
        /// <param name="rollbackHandler"></param>
        /// <returns></returns>
        ValueTask ExecuteTransactionAsync(
            ImmutableList<TSessionIdentifier> transactionTargets,
            Func<Dictionary<TSessionIdentifier, IDisposable>, ValueTask> transactionAsyncFunction,
            Func<Exception, ValueTask>? rollbackHandler = null);

        /// <summary>
        /// トランザクションサービスを取得する
        /// </summary>
        /// <returns></returns>
        ITransactionService<IDisposable> GetTransactionService(Type sessionType);
    }
}
