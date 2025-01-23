namespace CSStack.TADA
{
    /// <summary>
    /// トランザクションセッション
    /// </summary>
    public sealed class TransactionSessions
    {
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="sessions"></param>
        public TransactionSessions(IReadOnlyDictionary<Type, dynamic> sessions)
        {
            Sessions = sessions;
        }

        /// <summary>
        /// セッション
        /// </summary>
        public IReadOnlyDictionary<Type, dynamic> Sessions { get; }

        /// <summary>
        /// セッションを取得する
        /// </summary>
        /// <typeparam name="TSession"></typeparam>
        /// <returns></returns>
        public TSession GetSession<TSession>()
        {
            return (TSession)Sessions[typeof(TSession)];
        }
    }
}
