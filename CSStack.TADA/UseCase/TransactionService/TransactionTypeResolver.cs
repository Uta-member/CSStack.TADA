namespace CSStack.TADA
{
    /// <summary>
    /// トランザクション因子の型を取得する汎用クラス
    /// </summary>
    /// <typeparam name="TSessionIdentifier"></typeparam>
    public sealed class TransactionTypeResolver<TSessionIdentifier> : ITransactionTypeResolver<TSessionIdentifier>
        where TSessionIdentifier : Enum
    {
        private readonly Dictionary<TSessionIdentifier, Type> _transactionTypeDictionary;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="transactionTypeDictionary"></param>
        public TransactionTypeResolver(Dictionary<TSessionIdentifier, Type> transactionTypeDictionary)
        {
            _transactionTypeDictionary = transactionTypeDictionary;
        }

        /// <inheritdoc/>
        public Type GetSessionType(TSessionIdentifier sessionIdentifier)
        {
            return _transactionTypeDictionary[sessionIdentifier];
        }
    }
}
