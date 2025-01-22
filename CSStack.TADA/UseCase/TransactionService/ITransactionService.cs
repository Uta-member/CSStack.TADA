namespace CSStack.TADA
{
    /// <summary>
    /// トランザクションサービス（非同期）のインターフェース
    /// </summary>
    /// <typeparam name="TSession">トランザクションを維持するための因子</typeparam>
    public interface ITransactionService<TSession> where TSession : IDisposable
    {
        /// <summary>
        /// トランザクション開始(戻り値はusingの変数に入れてください)
        /// </summary>
        /// <returns>トランザクションを維持するための因子</returns>
        ValueTask<TSession> BeginAsync();

        /// <summary>
        /// コミット
        /// </summary>
        /// <param name="session">トランザクションを維持するための因子</param>
        /// <returns></returns>
        ValueTask CommitAsync(TSession session);

        /// <summary>
        /// ロールバック
        /// </summary>
        /// <param name="session">トランザクションを維持するための因子</param>
        /// <returns></returns>
        ValueTask RollbackAsync(TSession session);
    }
}
