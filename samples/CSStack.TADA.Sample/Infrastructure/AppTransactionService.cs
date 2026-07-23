namespace CSStack.TADA.Sample
{
    /// <summary>
    /// <see cref="AppSession"/> のトランザクションサービス。
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>これを DI に登録しないと <see cref="TransactionManager"/> は即
    /// <see cref="InvalidOperationException"/> を投げる。</b>
    /// マネージャーはセッション型から <c>ITransactionService&lt;TSession&gt;</c> を
    /// <see cref="IServiceProvider"/> 経由で解決するため。
    /// </para>
    /// <para>
    /// <b>セッションを Dispose しない。</b> begin / commit / rollback だけを行う。
    /// 所有権は <see cref="ITransactionManager"/> にあり、commit 後・rollback 後・例外時の
    /// いずれの経路でもマネージャーが Dispose する。ここで Dispose すると二重解放になる。
    /// </para>
    /// </remarks>
    public sealed class AppTransactionService : ITransactionService<AppSession>
    {
        private readonly AppDatabase _database;

        /// <summary>
        /// コンストラクタ。
        /// </summary>
        public AppTransactionService(AppDatabase database)
        {
            _database = database;
        }

        /// <summary>
        /// トランザクションを開始する。
        /// </summary>
        public ValueTask<AppSession> BeginAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new AppSession(_database));
        }

        /// <summary>
        /// コミットする。<paramref name="session"/> を Dispose しないこと。
        /// </summary>
        public ValueTask CommitAsync(AppSession session, CancellationToken cancellationToken = default)
        {
            session.Commit();
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// ロールバックする。<paramref name="session"/> を Dispose しないこと。
        /// </summary>
        public ValueTask RollbackAsync(AppSession session, CancellationToken cancellationToken = default)
        {
            session.Rollback();
            return ValueTask.CompletedTask;
        }
    }
}
