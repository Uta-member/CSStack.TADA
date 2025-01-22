namespace CSStack.TADA
{
    /// <summary>
    /// トランザクション因子の型を取得するインターフェース
    /// </summary>
    /// <typeparam name="TSessionIdentifier"></typeparam>
    public interface ITransactionTypeResolver<TSessionIdentifier> where TSessionIdentifier : Enum
    {
        /// <summary>
        /// トランザクション因子の型を取得する
        /// </summary>
        /// <param name="sessionIdentifier"></param>
        /// <returns></returns>
        Type GetSessionType(TSessionIdentifier sessionIdentifier);
    }
}
