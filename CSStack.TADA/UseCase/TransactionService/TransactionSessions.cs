namespace CSStack.TADA
{
    /// <summary>
    /// Transaction sessions.
    /// </summary>
    public sealed class TransactionSessions
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="sessions"></param>
        public TransactionSessions(IReadOnlyDictionary<Type, dynamic> sessions)
        {
            Sessions = sessions;
        }

        /// <summary>
        /// Get a session.
        /// </summary>
        /// <typeparam name="TSession">Session type</typeparam>
        /// <returns></returns>
        public TSession GetSession<TSession>()
            where TSession : IDisposable
        {
            return (TSession)Sessions[typeof(TSession)];
        }

        /// <summary>
        /// Sessions.
        /// </summary>
        public IReadOnlyDictionary<Type, dynamic> Sessions { get; }
    }
}
